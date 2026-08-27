using Xunit;

namespace Kogoshvili.Temporal.Hosting.Tests;

// The activity-options registry is static shared state seeded by AddTemporal and
// directly by tests, so all tests that touch it run serially.
[CollectionDefinition("ActivityOptionsRegistry", DisableParallelization = true)]
public sealed class ActivityOptionsRegistryCollection
{
}
