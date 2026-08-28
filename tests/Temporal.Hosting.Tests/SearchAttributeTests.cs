using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Temporalio.Api.Enums.V1;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class SearchAttributeTests
{
    [Fact]
    public void AddTemporal_Configuration_BindsSearchAttributes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:SearchAttributes:Enabled"] = "true",
                ["Temporal:SearchAttributes:FailOnConflict"] = "true",
                ["Temporal:SearchAttributes:Attributes:CustomerId:Type"] = "Keyword",
                ["Temporal:SearchAttributes:Attributes:Amount:Type"] = "Double",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TemporalOptions>>().Value;

        var searchAttributes = options.SearchAttributes;
        Assert.NotNull(searchAttributes);
        Assert.True(searchAttributes.Enabled);
        Assert.True(searchAttributes.FailOnConflict);

        var attributes = searchAttributes.Attributes;
        Assert.NotNull(attributes);
        Assert.Equal(IndexedValueType.Keyword, attributes["CustomerId"].Type);
        Assert.Equal(IndexedValueType.Double, attributes["Amount"].Type);
    }

    [Fact]
    public void AddTemporal_Configuration_SearchAttributesDefaultsToEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:SearchAttributes:Attributes:CustomerId:Type"] = "Keyword",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TemporalOptions>>().Value;

        Assert.True(options.SearchAttributes!.Enabled);
        Assert.False(options.SearchAttributes.FailOnConflict);
    }

    [Fact]
    public void AddTemporal_Configuration_SearchAttributeTypeUnspecified_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:SearchAttributes:Attributes:CustomerId:Type"] = "Unspecified",
            })
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddTemporal(configuration));
    }

    [Fact]
    public void Diff_SeparatesMissingFromConflicting()
    {
        var declared = new Dictionary<string, IndexedValueType>
        {
            ["CustomerId"] = IndexedValueType.Keyword,
            ["Amount"] = IndexedValueType.Double,
            ["Seen"] = IndexedValueType.Bool,
        };

        var existing = new Dictionary<string, IndexedValueType>
        {
            ["CustomerId"] = IndexedValueType.Keyword,
            ["Amount"] = IndexedValueType.Int,
        };

        var diff = SearchAttributeOps.Diff(declared, existing);

        Assert.Single(diff.Missing);
        Assert.Equal(IndexedValueType.Bool, diff.Missing["Seen"]);

        var conflict = Assert.Single(diff.Conflicts);
        Assert.Equal("Amount", conflict.Name);
        Assert.Equal(IndexedValueType.Double, conflict.Declared);
        Assert.Equal(IndexedValueType.Int, conflict.Existing);
    }

    [Fact]
    public void Diff_AllPresentMatching_IsEmpty()
    {
        var declared = new Dictionary<string, IndexedValueType>
        {
            ["CustomerId"] = IndexedValueType.Keyword,
        };

        var existing = new Dictionary<string, IndexedValueType>
        {
            ["CustomerId"] = IndexedValueType.Keyword,
        };

        var diff = SearchAttributeOps.Diff(declared, existing);

        Assert.Empty(diff.Missing);
        Assert.Empty(diff.Conflicts);
    }
}
