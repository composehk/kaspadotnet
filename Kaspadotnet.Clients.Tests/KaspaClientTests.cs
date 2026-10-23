using System.Net;
using System.Text;
using Kaspadotnet.Clients.Shared;
using Xunit;

namespace Kaspadotnet.Clients.Tests;

public class KaspaClientTests
{
    private const string Address =
        "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";

    [Fact]
    public void Rpc_client_defaults_to_local_mainnet_and_does_not_require_a_node_to_construct()
    {
        using var client = new KaspaRpcClient();

        Assert.Equal(new Uri("http://localhost:16110"), client.Address);
        Assert.Equal(16110, KaspaRpcClient.DefaultMainnetPort);
        client.Dispose();
    }

    [Fact]
    public void Rpc_client_accepts_url_host_and_port()
    {
        using var fromUrl = new KaspaRpcClient("http://127.0.0.1:16210");
        using var fromParts = new KaspaRpcClient("kaspad.local", KaspaRpcClient.DefaultTestnetPort);
        using var fromOptions = new KaspaRpcClient(new KaspaRpcClientOptions
        {
            Host = "10.0.0.8",
            Port = KaspaRpcClient.DefaultDevnetPort,
            UseTls = true,
        });

        Assert.Equal(new Uri("http://127.0.0.1:16210"), fromUrl.Address);
        Assert.Equal("kaspad.local", fromParts.Address.Host);
        Assert.Equal(16210, fromParts.Address.Port);
        Assert.Equal(new Uri("https://10.0.0.8:16610"), fromOptions.Address);
    }

    [Fact]
    public async Task Rpc_balance_rejects_a_bad_address_before_dialing()
    {
        using var client = new KaspaRpcClient("http://127.0.0.1:1");

        await Assert.ThrowsAsync<KaspaAddressException>(() => client.GetBalanceAsync("not-an-address"));
        await Assert.ThrowsAsync<KaspaAddressException>(() =>
            client.GetUtxosByAddressesAsync(new[] { "kaspa:not-valid" }));
    }

    [Fact]
    public void Rest_client_defaults_to_public_api()
    {
        using var client = new KaspaPublicRestClient();

        Assert.Equal(new Uri(KaspaPublicRestClient.DefaultBaseAddressText), client.BaseAddress);
        client.Dispose();
    }

    [Fact]
    public void Rest_client_can_be_constructed_around_a_caller_owned_http_client()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://rest.test/") };
        using var client = new KaspaPublicRestClient(http);

        Assert.Equal(new Uri("http://rest.test/"), client.BaseAddress);
    }

    [Fact]
    public async Task Rest_balance_reads_sompi_offline()
    {
        var handler = new RecordingHandler(
            """{"address":"kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e","balance":42}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://rest.test/") };
        using var client = new KaspaPublicRestClient(http);

        var balance = await client.GetBalanceAsync(Address);

        Assert.Equal(42UL, balance.Sompi);
        Assert.Equal(Address, balance.Address);
        Assert.Equal(0.00000042m, balance.Kas);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("GET", handler.Method);
        Assert.Contains("/addresses/", handler.PathAndQuery, StringComparison.Ordinal);
        Assert.Contains("%3A", handler.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rest_balance_rejects_bad_prefix_without_http()
    {
        var handler = new RecordingHandler("{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://rest.test/") };
        using var client = new KaspaPublicRestClient(http);

        await Assert.ThrowsAsync<KaspaAddressException>(() => client.GetBalanceAsync("btc:qqqq"));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Rest_utxos_map_string_amounts_and_post_many_addresses()
    {
        const string second =
            "kaspa:qp0l70zd5x85ttwd6jv7g3s3a8llzj96d8dncn4zmhv4tlzx5k2jyqh70xmfj";
        var handler = new RecordingHandler(
            """
            [{
              "address": "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
              "outpoint": {"transactionId": "ef62efbc2825d3ef9ec1cf9b80506876ac077b64b11a39c8ef5e028415444dc9", "index": 3},
              "utxoEntry": {
                "amount": "11501593788",
                "scriptPublicKey": {"scriptPublicKey": "20aa"},
                "blockDaaScore": "18867232",
                "isCoinbase": true
              }
            }]
            """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://rest.test/") };
        using var client = new KaspaPublicRestClient(http);

        var one = await client.GetUtxosByAddressesAsync(new[] { Address });
        Assert.Equal("GET", handler.Method);
        var many = await client.GetUtxosByAddressesAsync(new[] { Address, second });

        var utxo = Assert.Single(one);
        Assert.Equal(Address, utxo.Address);
        Assert.Equal("ef62efbc2825d3ef9ec1cf9b80506876ac077b64b11a39c8ef5e028415444dc9", utxo.TransactionId);
        Assert.Equal(3u, utxo.OutputIndex);
        Assert.Equal(11501593788UL, utxo.AmountSompi);
        Assert.Equal("20aa", utxo.ScriptPublicKey);
        Assert.Equal(18867232UL, utxo.BlockDaaScore);
        Assert.True(utxo.IsCoinbase);

        Assert.Equal("POST", handler.Method);
        Assert.Contains("addresses/utxos", handler.PathAndQuery, StringComparison.Ordinal);
        Assert.Contains(second, handler.RequestBody, StringComparison.Ordinal);
        Assert.Single(many);
    }

    [Fact]
    public async Task Rest_info_and_blockdag_parse_public_api_shapes()
    {
        var handler = new RecordingHandler(string.Empty);
        handler.BodyForPath = path =>
        {
            if (path.Contains("blockdag", StringComparison.Ordinal))
            {
                return """
                    {
                      "networkName": "kaspa-mainnet",
                      "blockCount": "260890",
                      "headerCount": "2131312",
                      "tipHashes": ["aa"],
                      "difficulty": 3870677677777.2,
                      "pastMedianTime": "1656455670700",
                      "virtualParentHashes": ["bb"],
                      "pruningPointHash": "cc",
                      "virtualDaaScore": "19989141",
                      "sink": "dd"
                    }
                    """;
            }

            return """
                {
                  "mempoolSize": "7",
                  "serverVersion": "0.12.2",
                  "isUtxoIndexed": true,
                  "isSynced": false,
                  "p2pIdHashed": "36a17cd8644eef34fc7fe4719655e06dbdf117008900c46975e66c35acd09b01"
                }
                """;
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://rest.test/") };
        using var client = new KaspaPublicRestClient(http);

        var info = await client.GetInfoAsync();
        var dag = await client.GetBlockDagInfoAsync();

        Assert.Equal("0.12.2", info.ServerVersion);
        Assert.Equal(7UL, info.MempoolSize);
        Assert.True(info.IsUtxoIndexed);
        Assert.False(info.IsSynced);
        Assert.True(info.P2pIdIsHashed);
        Assert.Equal("36a17cd8644eef34fc7fe4719655e06dbdf117008900c46975e66c35acd09b01", info.P2pId);
        Assert.Equal("kaspa-mainnet", dag.NetworkName);
        Assert.Equal(260890UL, dag.BlockCount);
        Assert.Equal(2131312UL, dag.HeaderCount);
        Assert.Equal(new[] { "aa" }, dag.TipHashes);
        Assert.Equal(3870677677777.2, dag.Difficulty);
        Assert.Equal(1656455670700L, dag.PastMedianTime);
        Assert.Equal("cc", dag.PruningPointHash);
        Assert.Equal(19989141UL, dag.VirtualDaaScore);
        Assert.Equal("dd", dag.Sink);
    }

    [Fact]
    public async Task Rest_http_error_is_a_rpc_exception()
    {
        var handler = new RecordingHandler("nope", HttpStatusCode.BadGateway);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://rest.test/") };
        using var client = new KaspaPublicRestClient(http);

        var error = await Assert.ThrowsAsync<KaspaRpcException>(() => client.GetInfoAsync());

        Assert.Contains("502", error.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        public int Calls { get; private set; }
        public string? Method { get; private set; }
        public string? PathAndQuery { get; private set; }
        public string? RequestBody { get; private set; }
        public Func<string, string>? BodyForPath { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method.Method;
            PathAndQuery = request.RequestUri?.PathAndQuery;
            if (request.Content != null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var body = BodyForPath is null ? _body : BodyForPath(PathAndQuery ?? string.Empty);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }
}
