using System.Globalization;
using Kaspadotnet.Clients;

namespace Kaspadotnet.Examples;

internal static class PublicRestDemo
{
    public static async Task BalanceAsync(string[] args)
    {
        var address = AddressOrSample(args);
        using var client = new KaspaPublicRestClient();
        var balance = await client.GetBalanceAsync(address);
        Console.WriteLine($"{balance.Address}");
        Console.WriteLine($"sompi={balance.Sompi}");
        Console.WriteLine($"KAS={balance.Kas.ToString("0.########", CultureInfo.InvariantCulture)}");
    }

    public static async Task UtxosAsync(string[] args)
    {
        var address = AddressOrSample(args);
        using var client = new KaspaPublicRestClient();
        var utxos = await client.GetUtxosByAddressesAsync(new[] { address });
        Console.WriteLine($"{utxos.Count} UTXO(s) for {address}");
        foreach (var utxo in utxos.Take(5))
        {
            Console.WriteLine($"{utxo.TransactionId}:{utxo.OutputIndex} sompi={utxo.AmountSompi} coinbase={utxo.IsCoinbase}");
        }
    }

    public static async Task InfoAsync()
    {
        using var client = new KaspaPublicRestClient();
        var info = await client.GetInfoAsync();
        var dag = await client.GetBlockDagInfoAsync();
        Console.WriteLine($"server={info.ServerVersion} mempool={info.MempoolSize} synced={info.IsSynced} utxoIndex={info.IsUtxoIndexed}");
        Console.WriteLine($"network={dag.NetworkName} blocks={dag.BlockCount} headers={dag.HeaderCount} daa={dag.VirtualDaaScore}");
        Console.WriteLine($"difficulty={dag.Difficulty.ToString(CultureInfo.InvariantCulture)} tips={dag.TipHashes.Count}");
    }

    private static string AddressOrSample(string[] args)
    {
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
        {
            return args[1];
        }

        const string sample = "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";
        Console.WriteLine($"No address given. Using {sample}");
        return sample;
    }
}
