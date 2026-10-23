using Kaspadotnet.Clients.Shared;
using Xunit;

namespace Kaspadotnet.Clients.Tests;

public class KaspaAddressTests
{
    // rusty-kaspa crypto/addresses test vector: version PubKey, 32 zero bytes, mainnet.
    private const string ZeroPubKey =
        "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";

    // rusty-kaspa vector for a non-zero Schnorr payload.
    private const string NonZeroPubKey =
        "kaspa:qp0l70zd5x85ttwd6jv7g3s3a8llzj96d8dncn4zmhv4tlzx5k2jyqh70xmfj";

    [Theory]
    [InlineData(ZeroPubKey)]
    [InlineData(NonZeroPubKey)]
    public void Accepts_rusty_kaspa_vectors(string address)
    {
        var parsed = KaspaAddress.Parse(address);

        Assert.Equal("kaspa", parsed.Prefix);
        Assert.Equal(KaspaNetwork.Mainnet, parsed.Network);
        Assert.Equal(address, parsed.ToString());
        Assert.True(KaspaAddress.IsValid(address));
    }

    [Fact]
    public void Accepts_uppercase_prefix_and_payload()
    {
        var parsed = KaspaAddress.Parse(ZeroPubKey.ToUpperInvariant());

        Assert.Equal(ZeroPubKey, parsed.ToString());
        Assert.Equal(KaspaNetwork.Mainnet, parsed.Network);
    }

    [Fact]
    public void Validator_interface_matches_static_helper()
    {
        IKaspaAddressValidator validator = new KaspaAddressValidator();

        Assert.True(validator.IsValid(ZeroPubKey));
        Assert.True(validator.TryParse(NonZeroPubKey, out var parsed));
        Assert.Equal(KaspaNetwork.Mainnet, parsed!.Network);
        Assert.Contains("kaspa", KaspaAddress.Prefixes);
        Assert.Contains("kaspatest", KaspaAddress.Prefixes);
        Assert.Contains("kaspasim", KaspaAddress.Prefixes);
        Assert.Contains("kaspadev", KaspaAddress.Prefixes);
    }

    [Theory]
    [InlineData("kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4l")]
    [InlineData("kaspa:qqqqqqqq")]
    [InlineData("kaspa:qqqqqqqqqqqqq1qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e")]
    [InlineData("qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e")]
    [InlineData("kaspa1:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e")]
    [InlineData("btc:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_bad_prefix_charset_or_checksum(string? address)
    {
        Assert.False(KaspaAddress.IsValid(address));
        Assert.False(KaspaAddress.TryParse(address, out _));
    }

    [Fact]
    public void Parse_of_bad_checksum_throws()
    {
        var broken = ZeroPubKey[..^1] + "l";

        var error = Assert.Throws<KaspaAddressException>(() => KaspaAddress.Parse(broken));

        Assert.Contains("checksum", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mainnet_checksum_does_not_validate_under_another_prefix()
    {
        var payload = ZeroPubKey.Split(':')[1];

        Assert.False(KaspaAddress.IsValid("kaspatest:" + payload));
        Assert.False(KaspaAddress.IsValid("kaspadev:" + payload));
        Assert.False(KaspaAddress.IsValid("kaspasim:" + payload));
    }
}
