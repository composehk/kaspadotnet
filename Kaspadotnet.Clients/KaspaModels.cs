namespace Kaspadotnet.Clients;

/// <summary>
/// Balance in sompi. 1 KAS = <see cref="SompiPerKas"/> sompi, the same unit
/// composehk/kaswallet uses for REST amounts.
/// </summary>
public sealed record KaspaBalance(string Address, ulong Sompi)
{
    public const ulong SompiPerKas = 100_000_000;

    public decimal Kas => Sompi / (decimal)SompiPerKas;
}

/// <summary>One unspent output. Amounts are sompi.</summary>
public sealed record KaspaUtxo(
    string Address,
    string TransactionId,
    uint OutputIndex,
    ulong AmountSompi,
    string ScriptPublicKey,
    ulong BlockDaaScore,
    bool IsCoinbase);

/// <summary>Subset of kaspad <c>GetInfo</c> / <c>GET /info/kaspad</c>.</summary>
/// <param name="P2pId">
/// Raw p2p id from gRPC, or the SHA-256 hex published by api.kaspa.org when
/// <see cref="P2pIdIsHashed"/> is true.
/// </param>
public sealed record KaspaNodeInfo(
    string? ServerVersion,
    ulong? MempoolSize,
    string? P2pId,
    bool? IsUtxoIndexed,
    bool? IsSynced)
{
    public bool P2pIdIsHashed { get; init; }
}

/// <summary>Subset of kaspad <c>GetBlockDagInfo</c> / <c>GET /info/blockdag</c>.</summary>
public sealed record KaspaBlockDagInfo(
    string? NetworkName,
    ulong BlockCount,
    ulong HeaderCount,
    IReadOnlyList<string> TipHashes,
    double Difficulty,
    long PastMedianTime,
    IReadOnlyList<string> VirtualParentHashes,
    string? PruningPointHash,
    ulong VirtualDaaScore,
    string? Sink);

/// <summary>kaspad gRPC or the public REST API returned an error.</summary>
public sealed class KaspaRpcException : Exception
{
    public KaspaRpcException(string message) : base(message)
    {
    }

    public KaspaRpcException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
