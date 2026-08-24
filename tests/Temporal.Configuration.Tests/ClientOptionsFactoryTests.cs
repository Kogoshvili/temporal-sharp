using Kogoshvili.Temporal.Configuration;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Configuration.Tests;

public class ClientOptionsFactoryTests
{
    [Fact]
    public void Apply_MapsHost_Namespace_ApiKey_AndTls()
    {
        var certPath = Path.Combine(Path.GetTempPath(), $"temporal-ca-{Guid.NewGuid():N}.pem");
        File.WriteAllText(certPath, "CA-DATA");
        try
        {
            var options = new TemporalConnectionOptions
            {
                TargetHost = "host:7233",
                Namespace = "ns",
                ApiKey = "key",
                Tls = new TemporalTlsOptions { ServerRootCACertPath = certPath },
            };

            var connect = new TemporalClientConnectOptions();
            ClientOptionsFactory.Apply(connect, options);

            Assert.Equal("host:7233", connect.TargetHost);
            Assert.Equal("ns", connect.Namespace);
            Assert.Equal("key", connect.ApiKey);
            Assert.NotNull(connect.Tls);
            Assert.Equal("CA-DATA"u8.ToArray(), connect.Tls!.ServerRootCACert);
        }
        finally
        {
            File.Delete(certPath);
        }
    }

    [Fact]
    public void Apply_LeavesTlsNull_WhenNotConfigured()
    {
        var connect = new TemporalClientConnectOptions();
        ClientOptionsFactory.Apply(connect, new TemporalConnectionOptions());

        Assert.Null(connect.Tls);
    }
}
