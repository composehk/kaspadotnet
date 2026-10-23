using Kaspadotnet.Examples;

if (args.Length == 0 || args[0] is "-h" or "--help" or "list" or "--list")
{
    DemoCatalog.Print();
    return;
}

try
{
    Environment.ExitCode = await DemoCatalog.RunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

internal static class DemoCatalog
{
    public static void Print()
    {
        Console.WriteLine("""
            kaspadotnet examples

            Usage:
              dotnet run --project Kaspadotnet.Examples -- <demo> [args]

            Works today without a local node (https://api.kaspa.org and offline checks):
              address [kaspa:...]     Validate prefix, bech32 charset, and checksum
              rest-balance [address]  Balance in sompi via KaspaPublicRestClient
              rest-utxos [address]    UTXOs via KaspaPublicRestClient
              rest-info               kaspad info and blockDAG via the public REST API

            Legacy gRPC (local kaspad / kaspawalletd; protowire snapshot, WIP vs wRPC):
              grpc-info [url]         GetInfo + GetBlockDagInfo (default http://localhost:16110)
              new-address             kaspawalletd NewAddress (http://localhost:8082)
              wallet-balance          kaspawalletd GetBalance
              utxos                   kaspad GetUtxosByAddresses (sample uses devnet :16610)
              send [password]         kaspawalletd Send
              send-steps [password]   CreateUnsigned + Sign + Broadcast
              subscribe               UTXO notifications (blocks until the process is stopped)
              follow                  Block and virtual-chain follower (blocks)

            Run with no arguments to print this list.
            """);
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var examples = new RpcExamples();
        switch (args[0].ToLowerInvariant())
        {
            case "address":
                return ValidateAddressDemo.Run(args);
            case "rest-balance":
                await PublicRestDemo.BalanceAsync(args);
                return 0;
            case "rest-utxos":
                await PublicRestDemo.UtxosAsync(args);
                return 0;
            case "rest-info":
                await PublicRestDemo.InfoAsync();
                return 0;
            case "grpc-info":
                await GrpcNodeDemo.InfoAsync(args);
                return 0;
            case "new-address":
                await examples.NewAddressExample();
                return 0;
            case "wallet-balance":
                await examples.GetBalanceExample();
                return 0;
            case "utxos":
                await examples.GetUTXOsByAddressExample();
                return 0;
            case "send":
                await examples.SendTransaction(Password(args));
                return 0;
            case "send-steps":
                await examples.SendTransactionStepByStepExample(Password(args));
                return 0;
            case "subscribe":
                await examples.SubscribeToUTXOUpdatesExample();
                return 0;
            case "follow":
                await examples.FollowTransactionsExample();
                return 0;
            default:
                Console.Error.WriteLine($"Unknown demo '{args[0]}'.");
                Print();
                return 1;
        }
    }

    private static string Password(string[] args)
    {
        if (args.Length > 1 && !string.IsNullOrEmpty(args[1]))
        {
            return args[1];
        }

        Console.Write("Wallet password: ");
        return Console.ReadLine() ?? string.Empty;
    }
}
