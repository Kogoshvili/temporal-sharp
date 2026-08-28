using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Converters;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class DataConverterTests
{
    private const string Key = "test-key-test-key-test-key-test!";

    [Fact]
    public void AddTemporal_EncryptionEnabled_ConfiguresClientAndRegistersCodec()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions
        {
            DataConverter = new TemporalDataConverterOptions
            {
                Encryption = new TemporalEncryptionCodecOptions { Enabled = true, Key = Key },
            },
        });

        Assert.Contains(services, d => d.ServiceType == typeof(IPayloadCodec) && d.Lifetime == ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();
        var connectOptions = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;

        Assert.NotNull(connectOptions.DataConverter.PayloadCodec);
        Assert.NotNull(provider.GetService<IPayloadCodec>());
    }

    [Fact]
    public void AddTemporal_NoCodec_UsesDefaultDataConverter()
    {
        var services = new ServiceCollection();
        services.AddTemporal();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IPayloadCodec));

        using var provider = services.BuildServiceProvider();
        var connectOptions = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;

        Assert.Null(connectOptions.DataConverter.PayloadCodec);
    }

    [Fact]
    public void AddTemporal_TestServer_EncryptionEnabled_AppliesDataConverterToSharedOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal(new TemporalOptions
        {
            TestServer = new TemporalTestServerOptions { Enabled = true },
            DataConverter = new TemporalDataConverterOptions
            {
                Encryption = new TemporalEncryptionCodecOptions { Enabled = true, Key = Key },
            },
        });

        using var provider = services.BuildServiceProvider();
        var connectOptions = provider.GetRequiredService<TemporalClientConnectOptions>();

        Assert.NotNull(connectOptions.DataConverter.PayloadCodec);
    }

    [Fact]
    public void AddTemporal_EncryptionWithoutKey_Throws()
    {
        var services = new ServiceCollection();
        var options = new TemporalOptions
        {
            DataConverter = new TemporalDataConverterOptions
            {
                Encryption = new TemporalEncryptionCodecOptions { Enabled = true },
            },
        };

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(options));
    }

    [Fact]
    public void AddTemporal_EncryptionWithInvalidKeyLength_Throws()
    {
        var services = new ServiceCollection();
        var options = new TemporalOptions
        {
            DataConverter = new TemporalDataConverterOptions
            {
                Encryption = new TemporalEncryptionCodecOptions { Enabled = true, Key = "too-short" },
            },
        };

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(options));
    }

    [Fact]
    public void AddTemporal_EncryptionVaultWithoutSecretId_Throws()
    {
        var services = new ServiceCollection();
        var options = new TemporalOptions
        {
            DataConverter = new TemporalDataConverterOptions
            {
                Encryption = new TemporalEncryptionCodecOptions { Enabled = true, Source = "azureKeyVault" },
            },
        };

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(options));
    }

    [Fact]
    public void AddTemporal_EncryptionVaultWithInvalidEncoding_Throws()
    {
        var services = new ServiceCollection();
        var options = new TemporalOptions
        {
            DataConverter = new TemporalDataConverterOptions
            {
                Encryption = new TemporalEncryptionCodecOptions
                {
                    Enabled = true,
                    Source = "azureKeyVault",
                    SecretId = "my-key",
                    Encoding = "base32",
                },
            },
        };

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(options));
    }

    [Fact]
    public void AddTemporal_EncryptionVault_DoesNotRequireInlineKey()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions
        {
            DataConverter = new TemporalDataConverterOptions
            {
                Encryption = new TemporalEncryptionCodecOptions
                {
                    Enabled = true,
                    Source = "azureKeyVault",
                    SecretId = "my-key",
                },
            },
        });

        using var provider = services.BuildServiceProvider();
        var connectOptions = provider.GetRequiredService<TemporalClientConnectOptions>();

        Assert.Null(connectOptions.DataConverter.PayloadCodec);
    }

    [Fact]
    public void AddTemporal_CloudClaimCheckWithoutRegion_Throws()
    {
        var services = new ServiceCollection();
        var options = new TemporalOptions
        {
            DataConverter = new TemporalDataConverterOptions
            {
                ClaimCheck = new TemporalClaimCheckCodecOptions { Enabled = true, Store = "s3", BucketName = "my-bucket" },
            },
        };

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(options));
    }

    [Theory]
    [InlineData("raw", "abcdefghijklmnopqrstuvwxyz123456")]
    [InlineData("base64", "YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXoxMjM0NTY=")]
    [InlineData("hex", "6162636465666768696a6b6c6d6e6f707172737475767778797a313233343536")]
    public void DecodeKey_DecodesPerEncoding(string encoding, string secret)
    {
        var expected = System.Text.Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyz123456");

        Assert.Equal(expected, TemporalDataConverterFactory.DecodeKey(secret, encoding));
    }
}
