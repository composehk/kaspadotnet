using Kaspadotnet.Clients;

namespace Kaspadotnet.Examples;

internal static class GrpcNodeDemo
{
    public static async Task InfoAsync(string[] args)
    {
        var url = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1]
            : "http://localhost:16110";
        Console.WriteLine($"Legacy gRPC {url} (needs a kaspad that still serves protowire).");
        using var client = new KaspaRpcClient(url);
        var info = await client.GetInfoAsync();
        var dag = await client.GetBlockDagInfoAsync();
        Console.WriteLine($"server={info.ServerVersion} mempool={info.MempoolSize} p2p={info.P2pId}");
        Console.WriteLine($"network={dag.NetworkName} blocks={dag.BlockCount} headers={dag.HeaderCount} daa={dag.VirtualDaaScore}");
    }
}
