using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class TemporalClientFactoryTests
{
    [Fact]
    public void DefaultClient_ResolvesDefaultNamespace()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions { Namespace = "default" });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ITemporalClient>();
        Assert.Equal("default", client.Options.Namespace);
    }

    [Fact]
    public void Factory_GetNamedNamespace_ResolvesAndCaches()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions
        {
            Namespace = "default",
            Namespaces = ["payments", "orders"],
        });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ITemporalClientFactory>();

        var dflt = factory.Get();
        var payments = factory.Get("payments");
        var orders = factory.Get("orders");

        Assert.Equal("default", dflt.Options.Namespace);
        Assert.Equal("payments", payments.Options.Namespace);
        Assert.Equal("orders", orders.Options.Namespace);

        Assert.Same(payments, factory.Get("payments"));
        Assert.NotSame(payments, orders);
        Assert.NotSame(payments, dflt);
    }

    [Fact]
    public void Factory_DefaultClient_IsFactoryDefault()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions { Namespace = "default" });

        using var provider = services.BuildServiceProvider();

        Assert.Same(
            provider.GetRequiredService<ITemporalClient>(),
            provider.GetRequiredService<ITemporalClientFactory>().Get());
    }

    [Fact]
    public void Factory_NamespacesShareConnection()
    {
        var services = new ServiceCollection();
        services.AddTemporal(new TemporalOptions
        {
            Namespace = "default",
            Namespaces = ["payments", "orders"],
        });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ITemporalClientFactory>();

        Assert.Same(factory.Get("payments").Connection, factory.Get("orders").Connection);
        Assert.Same(factory.Get().Connection, factory.Get("orders").Connection);
    }

    [Fact]
    public void AddTemporal_WithClient_ReturnsSuppliedClient()
    {
        var client = TemporalClient.CreateLazy(new TemporalClientConnectOptions("localhost:7233")
        {
            Namespace = "custom",
        });

        var services = new ServiceCollection();
        services.AddTemporal(client);

        using var provider = services.BuildServiceProvider();

        Assert.Same(client, provider.GetRequiredService<ITemporalClient>());
        Assert.Same(client, provider.GetRequiredService<ITemporalClientFactory>().Get());
        Assert.Same(client, provider.GetRequiredService<ITemporalClientFactory>().Get("any-namespace"));
    }

    [Fact]
    public void AddTemporal_WithDelegate_UsesDelegateClient()
    {
        var client = TemporalClient.CreateLazy(new TemporalClientConnectOptions("localhost:7233")
        {
            Namespace = "custom",
        });

        var services = new ServiceCollection();
        services.AddTemporal(_ => client);

        using var provider = services.BuildServiceProvider();

        Assert.Same(client, provider.GetRequiredService<ITemporalClient>());
        Assert.Same(client, provider.GetRequiredService<ITemporalClientFactory>().Get());
    }

    [Fact]
    public void AddTemporal_Configuration_BindsNamespacesAndWorkerNamespace()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:Namespace"] = "default",
                ["Temporal:Namespaces:0"] = "payments",
                ["Temporal:Namespaces:1"] = "orders",
                ["Temporal:Workers:queue:Namespace"] = "payments",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TemporalOptions>>().Value;

        Assert.Equal("default", options.Namespace);
        Assert.Equal(["payments", "orders"], options.Namespaces);
        Assert.Equal("payments", options.Workers!["queue"].Namespace);
    }
}
