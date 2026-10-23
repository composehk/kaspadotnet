using Grpc.Core;
using Grpc.Net.Client;
using Kaspadotnet.Clients.Shared;
using Protowire;

namespace Kaspadotnet.Clients;

/// <summary>
/// Options for <see cref="KaspaRpcClient"/>.
/// </summary>
/// <remarks>
/// Public DNS seeders discover peers. They are not RPC servers. Do not pass a
/// seeder hostname here unless you know that host actually serves gRPC.
/// Mainnet seeders (rusty-kaspa <c>MAINNET_PARAMS</c>):
/// <c>mainnet-dnsseed-1.kaspanet.org</c>, <c>mainnet-dnsseed-2.kaspanet.org</c>,
/// <c>seeder1.kaspad.net</c>, <c>seeder2.kaspad.net</c>, <c>seeder3.kaspad.net</c>,
/// <c>seeder4.kaspad.net</c>, <c>kaspadns.kaspacalc.net</c>, <c>n-mainnet.kaspa.ws</c>,
/// <c>dnsseeder-kaspa-mainnet.x-con.at</c>.
/// Testnet-10 seeders: <c>seeder1-tn.kaspad.net</c>,
/// <c>dnsseeder-kaspa-testnet.x-con.at</c>, <c>n-testnet-10.kaspa.ws</c>.
/// Default gRPC ports when a node is running locally: mainnet 16110, testnet 16210,
/// simnet 16510, devnet 16610. Balance and UTXO RPCs need <c>--utxoindex</c>.
/// </remarks>
public sealed class KaspaRpcClientOptions
{
    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = KaspaRpcClient.DefaultMainnetPort;

    /// <summary>Use <c>https</c> when the node terminates TLS. Local kaspad is cleartext HTTP/2.</summary>
    public bool UseTls { get; init; }

    public Uri Address
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                throw new ArgumentException("Host is required.", nameof(Host));
            }

            if (Port is < 1 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(Port), Port, "Port must be between 1 and 65535.");
            }

            var scheme = UseTls ? "https" : "http";
            return new Uri($"{scheme}://{Host}:{Port}");
        }
    }
}

/// <summary>
/// High-level wrapper over kaspad's legacy protowire gRPC <c>MessageStream</c>.
/// </summary>
/// <remarks>
/// Status: legacy / WIP relative to current rusty-kaspa. The protos shipped in
/// this repo are a protowire snapshot. They still compile and can call
/// <c>GetBalanceByAddress</c>, <c>GetUtxosByAddresses</c>, <c>GetInfo</c>, and
/// <c>GetBlockDagInfo</c> on a node that serves a compatible gRPC port
/// (default <c>localhost:16110</c>). rusty-kaspa's primary public API is wRPC,
/// and this snapshot is not a full current schema.
/// For balance and UTXOs without a local node, use <see cref="KaspaPublicRestClient"/>
/// (<c>https://api.kaspa.org</c>). That path matches composehk/kaswallet's REST usage.
/// .NET 8 speaks cleartext HTTP/2 without the old
/// <c>Http2UnencryptedSupport</c> switch.
/// </remarks>
public sealed class KaspaRpcClient : IDisposable
{
    public const int DefaultMainnetPort = 16110;
    public const int DefaultTestnetPort = 16210;
    public const int DefaultSimnetPort = 16510;
    public const int DefaultDevnetPort = 16610;

    private readonly GrpcChannel _channel;
    private readonly RPC.RPCClient _rpc;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AsyncDuplexStreamingCall<KaspadMessage, KaspadMessage>? _stream;
    private int _disposed;

    public KaspaRpcClient()
        : this(new KaspaRpcClientOptions())
    {
    }

    public KaspaRpcClient(KaspaRpcClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Address = options.Address;
        _channel = GrpcChannel.ForAddress(Address);
        _rpc = new RPC.RPCClient(_channel);
    }

    /// <summary>Connects to an absolute URL such as <c>http://localhost:16110</c>.</summary>
    public KaspaRpcClient(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("gRPC URL is required.", nameof(url));
        }

        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "http://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Port <= 0)
        {
            throw new ArgumentException($"'{url}' is not an absolute kaspad gRPC URL.", nameof(url));
        }

        Address = uri;
        _channel = GrpcChannel.ForAddress(Address);
        _rpc = new RPC.RPCClient(_channel);
    }

    public KaspaRpcClient(string host, int port, bool useTls = false)
        : this(new KaspaRpcClientOptions { Host = host, Port = port, UseTls = useTls })
    {
    }

    /// <summary>gRPC address. Nothing is dialed until the first RPC.</summary>
    public Uri Address { get; }

    public async Task<KaspaBalance> GetBalanceAsync(string address, CancellationToken cancellationToken = default)
    {
        var parsed = KaspaAddress.Parse(address);
        var response = await CallAsync(
            new KaspadMessage
            {
                GetBalanceByAddressRequest = new GetBalanceByAddressRequestMessage
                {
                    Address = parsed.ToString(),
                },
            },
            KaspadMessage.PayloadOneofCase.GetBalanceByAddressResponse,
            message => message.GetBalanceByAddressResponse,
            body => body.Error,
            cancellationToken).ConfigureAwait(false);

        return new KaspaBalance(parsed.ToString(), response.Balance);
    }

    public async Task<IReadOnlyList<KaspaUtxo>> GetUtxosByAddressesAsync(
        IEnumerable<string> addresses,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(addresses);
        var request = new GetUtxosByAddressesRequestMessage();
        request.Addresses.AddRange(normalized);
        var response = await CallAsync(
            new KaspadMessage { GetUtxosByAddressesRequest = request },
            KaspadMessage.PayloadOneofCase.GetUtxosByAddressesResponse,
            message => message.GetUtxosByAddressesResponse,
            body => body.Error,
            cancellationToken).ConfigureAwait(false);

        var utxos = new List<KaspaUtxo>(response.Entries.Count);
        foreach (var entry in response.Entries)
        {
            utxos.Add(new KaspaUtxo(
                entry.Address,
                entry.Outpoint?.TransactionId ?? string.Empty,
                entry.Outpoint?.Index ?? 0,
                entry.UtxoEntry?.Amount ?? 0,
                entry.UtxoEntry?.ScriptPublicKey?.ScriptPublicKey ?? string.Empty,
                entry.UtxoEntry?.BlockDaaScore ?? 0,
                entry.UtxoEntry?.IsCoinbase ?? false));
        }

        return utxos;
    }

    public async Task<KaspaNodeInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await CallAsync(
            new KaspadMessage { GetInfoRequest = new GetInfoRequestMessage() },
            KaspadMessage.PayloadOneofCase.GetInfoResponse,
            message => message.GetInfoResponse,
            body => body.Error,
            cancellationToken).ConfigureAwait(false);

        return new KaspaNodeInfo(
            response.ServerVersion,
            response.MempoolSize,
            response.P2PId,
            IsUtxoIndexed: null,
            IsSynced: null);
    }

    public async Task<KaspaBlockDagInfo> GetBlockDagInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await CallAsync(
            new KaspadMessage { GetBlockDagInfoRequest = new GetBlockDagInfoRequestMessage() },
            KaspadMessage.PayloadOneofCase.GetBlockDagInfoResponse,
            message => message.GetBlockDagInfoResponse,
            body => body.Error,
            cancellationToken).ConfigureAwait(false);

        return new KaspaBlockDagInfo(
            response.NetworkName,
            response.BlockCount,
            response.HeaderCount,
            response.TipHashes.ToArray(),
            response.Difficulty,
            response.PastMedianTime,
            response.VirtualParentHashes.ToArray(),
            response.PruningPointHash,
            response.VirtualDaaScore,
            Sink: null);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        ResetStream();
        _channel.Dispose();
        _gate.Dispose();
    }

    private async Task<T> CallAsync<T>(
        KaspadMessage request,
        KaspadMessage.PayloadOneofCase expected,
        Func<KaspadMessage, T> project,
        Func<T, RPCError?> errorOf,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            KaspadMessage message;
            try
            {
                var stream = _stream ??= _rpc.MessageStream();
                await stream.RequestStream.WriteAsync(request, cancellationToken).ConfigureAwait(false);
                if (!await stream.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
                {
                    ResetStream();
                    throw new KaspaRpcException("kaspad closed the RPC stream.");
                }

                message = stream.ResponseStream.Current;
            }
            catch (Exception ex) when (ex is not KaspaRpcException)
            {
                ResetStream();
                if (ex is RpcException rpc)
                {
                    throw new KaspaRpcException(string.IsNullOrEmpty(rpc.Status.Detail) ? rpc.Status.ToString() : rpc.Status.Detail, rpc);
                }

                throw;
            }

            if (message.PayloadCase != expected)
            {
                ResetStream();
                throw new KaspaRpcException($"Expected {expected} but kaspad sent {message.PayloadCase}.");
            }

            var body = project(message);
            var error = errorOf(body);
            if (error != null && !string.IsNullOrEmpty(error.Message))
            {
                throw new KaspaRpcException(error.Message);
            }

            return body;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var normalized = new List<string>();
        foreach (var address in addresses)
        {
            normalized.Add(KaspaAddress.Parse(address).ToString());
        }

        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one Kaspa address is required.", nameof(addresses));
        }

        return normalized;
    }

    private void ResetStream()
    {
        var stream = _stream;
        _stream = null;
        stream?.Dispose();
    }
}
