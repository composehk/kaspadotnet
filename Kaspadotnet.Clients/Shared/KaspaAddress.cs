using System.Diagnostics.CodeAnalysis;

namespace Kaspadotnet.Clients.Shared;

/// <summary>
/// Textual Kaspa address check shared in spirit with composehk/kaswallet.
/// </summary>
/// <remarks>
/// kaswallet owns mnemonics, keys, and signing. This helper only recognizes the
/// same string form those tools already pass around: <c>{prefix}:{payload}</c>.
/// Prefixes are <c>kaspa</c> (mainnet), <c>kaspatest</c>, <c>kaspasim</c>, and
/// <c>kaspadev</c>. The payload uses Kaspa's bech32 charset
/// <c>qpzry9x8gf2tvdw0s3jn54khce6mua7l</c> and the CashAddr polymod from
/// rusty-kaspa <c>crypto/addresses</c> (not Bitcoin BIP-173).
/// Current wallet payloads are 61 characters (Schnorr or script hash) or 63
/// (ECDSA). Length is not hard-coded so a future version still checksums.
/// Do not copy key-to-script rules here; keep those in kaswallet and compare
/// behavior when either side changes.
/// </remarks>
public interface IKaspaAddressValidator
{
    /// <summary>True when <paramref name="address"/> has a known prefix, charset, and checksum.</summary>
    bool IsValid(string? address);

    /// <summary>Parses <paramref name="address"/> without throwing.</summary>
    bool TryParse(string? address, [NotNullWhen(true)] out KaspaAddress? parsed);
}

/// <summary>Network named by a Kaspa address prefix. Matches kaswallet's <c>kaspa:</c> family.</summary>
public enum KaspaNetwork
{
    Mainnet,
    Testnet,
    Simnet,
    Devnet,
}

/// <summary>A Kaspa address string that passed prefix, charset, and checksum checks.</summary>
public sealed class KaspaAddress
{
    /// <summary>Kaspa bech32 alphabet. The characters 1, b, i, and o are intentionally absent.</summary>
    public const string Bech32Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    private const int ChecksumLength = 8;

    /// <summary>Prefixes accepted by this helper and by kaswallet-style address strings.</summary>
    public static readonly IReadOnlyList<string> Prefixes = new[]
    {
        "kaspa",
        "kaspatest",
        "kaspasim",
        "kaspadev",
    };

    private static readonly sbyte[] CharsetIndex = BuildCharsetIndex();

    private KaspaAddress(string prefix, string payload, KaspaNetwork network)
    {
        Prefix = prefix;
        Payload = payload;
        Network = network;
    }

    /// <summary>Lowercase prefix without the colon, for example <c>kaspa</c>.</summary>
    public string Prefix { get; }

    /// <summary>Lowercase bech32 payload, including the 8-character checksum.</summary>
    public string Payload { get; }

    public KaspaNetwork Network { get; }

    public static bool IsValid(string? address) => TryParse(address, out _);

    public static bool TryParse(string? address, [NotNullWhen(true)] out KaspaAddress? parsed)
    {
        try
        {
            parsed = Parse(address);
            return true;
        }
        catch (KaspaAddressException)
        {
            parsed = null;
            return false;
        }
        catch (ArgumentNullException)
        {
            parsed = null;
            return false;
        }
    }

    public static KaspaAddress Parse(string? address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        var text = address.Trim().ToLowerInvariant();
        var colon = text.IndexOf(':');
        if (colon <= 0 || colon != text.LastIndexOf(':'))
        {
            throw new KaspaAddressException("A Kaspa address must look like {prefix}:{payload}, for example kaspa:qqqq...");
        }

        var prefix = text[..colon];
        var payload = text[(colon + 1)..];
        if (!IsKnownPrefix(prefix))
        {
            throw new KaspaAddressException(
                $"Unknown Kaspa prefix '{prefix}'. Expected one of: {string.Join(", ", Prefixes)}.");
        }

        if (payload.Length <= ChecksumLength)
        {
            throw new KaspaAddressException("Kaspa address payload is too short to include a checksum.");
        }

        var data = new byte[payload.Length];
        for (var i = 0; i < payload.Length; i++)
        {
            var symbol = payload[i];
            var value = symbol < CharsetIndex.Length ? CharsetIndex[symbol] : (sbyte)-1;
            if (value < 0)
            {
                throw new KaspaAddressException(
                    $"Character '{symbol}' is not in the Kaspa bech32 charset ({Bech32Charset}).");
            }

            data[i] = (byte)value;
        }

        var split = payload.Length - ChecksumLength;
        var expected = PayloadChecksum(prefix, data.AsSpan(0, split));
        var actual = ReadChecksum(data.AsSpan(split));
        if (expected != actual)
        {
            throw new KaspaAddressException("Kaspa address checksum does not match the prefix and payload.");
        }

        return new KaspaAddress(prefix, payload, NetworkOf(prefix));
    }

    public override string ToString() => $"{Prefix}:{Payload}";

    private static bool IsKnownPrefix(string prefix)
    {
        foreach (var known in Prefixes)
        {
            if (known == prefix)
            {
                return true;
            }
        }

        return false;
    }

    private static KaspaNetwork NetworkOf(string prefix) => prefix switch
    {
        "kaspa" => KaspaNetwork.Mainnet,
        "kaspatest" => KaspaNetwork.Testnet,
        "kaspasim" => KaspaNetwork.Simnet,
        "kaspadev" => KaspaNetwork.Devnet,
        _ => throw new KaspaAddressException($"Unknown Kaspa prefix '{prefix}'."),
    };

    private static sbyte[] BuildCharsetIndex()
    {
        var index = new sbyte[128];
        Array.Fill(index, (sbyte)-1);
        for (var i = 0; i < Bech32Charset.Length; i++)
        {
            index[Bech32Charset[i]] = (sbyte)i;
        }

        return index;
    }

    /// <summary>
    /// CashAddr polymod used by Kaspa. Constants match rusty-kaspa
    /// <c>crypto/addresses/src/bech32.rs</c>.
    /// </summary>
    private static ulong PayloadChecksum(string prefix, ReadOnlySpan<byte> payloadU5)
    {
        var c = 1UL;
        foreach (var ch in prefix)
        {
            c = PolymodStep(c, (byte)(ch & 0x1F));
        }

        c = PolymodStep(c, 0);
        foreach (var value in payloadU5)
        {
            c = PolymodStep(c, value);
        }

        for (var i = 0; i < ChecksumLength; i++)
        {
            c = PolymodStep(c, 0);
        }

        return c ^ 1;
    }

    private static ulong PolymodStep(ulong c, byte d)
    {
        var c0 = c >> 35;
        c = ((c & 0x07ffffffffUL) << 5) ^ d;
        if ((c0 & 0x01) != 0)
        {
            c ^= 0x98f2bc8e61UL;
        }

        if ((c0 & 0x02) != 0)
        {
            c ^= 0x79b76d99e2UL;
        }

        if ((c0 & 0x04) != 0)
        {
            c ^= 0xf33e5fb3c4UL;
        }

        if ((c0 & 0x08) != 0)
        {
            c ^= 0xae2eabe2a8UL;
        }

        if ((c0 & 0x10) != 0)
        {
            c ^= 0x1e4f43e470UL;
        }

        return c;
    }

    private static ulong ReadChecksum(ReadOnlySpan<byte> checksumU5)
    {
        var buffer = 0;
        var bits = 0;
        var bytes = new byte[5];
        var index = 0;
        foreach (var value in checksumU5)
        {
            buffer = (buffer << 5) | value;
            bits += 5;
            while (bits >= 8)
            {
                bits -= 8;
                if (index < bytes.Length)
                {
                    bytes[index] = (byte)((buffer >> bits) & 0xFF);
                }

                index++;
                buffer &= (1 << bits) - 1;
            }
        }

        if (index != bytes.Length)
        {
            return ulong.MaxValue;
        }

        var result = 0UL;
        foreach (var part in bytes)
        {
            result = (result << 8) | part;
        }

        return result;
    }
}

/// <summary>Default <see cref="IKaspaAddressValidator"/>. Stateless and safe to share.</summary>
public sealed class KaspaAddressValidator : IKaspaAddressValidator
{
    public bool IsValid(string? address) => KaspaAddress.IsValid(address);

    public bool TryParse(string? address, [NotNullWhen(true)] out KaspaAddress? parsed) =>
        KaspaAddress.TryParse(address, out parsed);
}

/// <summary>An address string failed prefix, charset, or checksum checks.</summary>
public sealed class KaspaAddressException : FormatException
{
    public KaspaAddressException(string message) : base(message)
    {
    }
}
