using Kaspadotnet.Clients.Shared;

namespace Kaspadotnet.Examples;

internal static class ValidateAddressDemo
{
    public static int Run(string[] args)
    {
        var address = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1]
            : "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";

        IKaspaAddressValidator validator = new KaspaAddressValidator();
        if (!validator.TryParse(address, out var parsed))
        {
            Console.WriteLine("invalid");
            Console.WriteLine("Expected {prefix}:{payload} with prefix kaspa, kaspatest, kaspasim, or kaspadev.");
            return 1;
        }

        Console.WriteLine("valid");
        Console.WriteLine($"prefix={parsed.Prefix} network={parsed.Network} payloadLength={parsed.Payload.Length}");
        Console.WriteLine(parsed.ToString());
        return 0;
    }
}
