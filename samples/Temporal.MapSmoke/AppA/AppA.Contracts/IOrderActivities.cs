using Temporalio.Activities;

namespace Kogoshvili.Temporal.MapSmoke.AppA.Contracts;

// Scenario 11: [Activity] interface contract — implementations repeat the
// attributes because the SDK only discovers attributes on the implementing
// methods, while the grapher matches callers through the interface.
public interface IOrderActivities
{
    [Activity]
    Task<string> Process(string input);

    [Activity]
    Task<string> Ship(string input);
}
