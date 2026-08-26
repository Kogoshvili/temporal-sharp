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

    [Fact]
    public void Apply_MapsRpcRetry_WhenConfigured()
    {
        var options = new TemporalConnectionOptions
        {
            RpcRetry = new TemporalRpcRetryOptions
            {
                InitialInterval = TimeSpan.FromSeconds(2),
                MaxInterval = TimeSpan.FromSeconds(30),
                MaxElapsedTime = TimeSpan.FromMinutes(5),
                MaxRetries = 7,
            },
        };

        var connect = new TemporalClientConnectOptions();
        ClientOptionsFactory.Apply(connect, options);

        Assert.NotNull(connect.RpcRetry);
        Assert.Equal(TimeSpan.FromSeconds(2), connect.RpcRetry!.InitialInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), connect.RpcRetry.MaxInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), connect.RpcRetry.MaxElapsedTime);
        Assert.Equal(7, connect.RpcRetry.MaxRetries);
    }

    [Fact]
    public void Apply_LeavesRpcRetryNull_WhenNotConfigured()
    {
        var connect = new TemporalClientConnectOptions();
        ClientOptionsFactory.Apply(connect, new TemporalConnectionOptions());

        Assert.Null(connect.RpcRetry);
    }

    [Fact]
    public void Apply_MapsConnectionTransportOptions_WhenConfigured()
    {
        var options = new TemporalConnectionOptions
        {
            KeepAlive = new TemporalKeepAliveOptions
            {
                Interval = TimeSpan.FromSeconds(45),
                Timeout = TimeSpan.FromSeconds(10),
            },
            HttpConnectProxy = new TemporalHttpConnectProxyOptions
            {
                TargetHost = "proxy:8080",
                Username = "user",
                Password = "pass",
            },
            DnsLoadBalancing = new TemporalDnsLoadBalancingOptions
            {
                ResolutionInterval = TimeSpan.FromSeconds(60),
            },
            GrpcCompression = new TemporalGrpcCompressionOptions { Mode = TemporalGrpcCompressionOptions.None },
        };

        var connect = new TemporalClientConnectOptions();
        ClientOptionsFactory.Apply(connect, options);

        Assert.NotNull(connect.KeepAlive);
        Assert.Equal(TimeSpan.FromSeconds(45), connect.KeepAlive!.Interval);
        Assert.Equal(TimeSpan.FromSeconds(10), connect.KeepAlive.Timeout);
        Assert.NotNull(connect.HttpConnectProxy);
        Assert.Equal("proxy:8080", connect.HttpConnectProxy!.TargetHost);
        Assert.Equal(("user", "pass"), connect.HttpConnectProxy.BasicAuth);
        Assert.NotNull(connect.DnsLoadBalancing);
        Assert.Equal(TimeSpan.FromSeconds(60), connect.DnsLoadBalancing!.ResolutionInterval);
        Assert.IsType<GrpcCompression.None>(connect.GrpcCompression);
    }

    [Fact]
    public void Apply_LeavesTransportDefaults_WhenNotConfigured()
    {
        var connect = new TemporalClientConnectOptions();
        ClientOptionsFactory.Apply(connect, new TemporalConnectionOptions());

        Assert.NotNull(connect.KeepAlive);
        Assert.Null(connect.HttpConnectProxy);
        Assert.Null(connect.DnsLoadBalancing);
        Assert.IsType<GrpcCompression.Gzip>(connect.GrpcCompression);
    }

    [Fact]
    public void Apply_InvalidGrpcCompressionMode_Throws()
    {
        var options = new TemporalConnectionOptions
        {
            GrpcCompression = new TemporalGrpcCompressionOptions { Mode = "lz4" },
        };

        var connect = new TemporalClientConnectOptions();

        Assert.Throws<InvalidOperationException>(() => ClientOptionsFactory.Apply(connect, options));
    }
}
