namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// The lifetime used when auto-discovered activities are registered with the
/// worker. Mirrors the activity-registration lifetime options offered by
/// <c>Temporalio.Extensions.Hosting</c>.
/// </summary>
public enum ActivityLifetime
{
    /// <summary>A new instance is created for each activity attempt (the default for instance activities).</summary>
    Scoped,

    /// <summary>A single instance is created and reused across activity attempts.</summary>
    Singleton,

    /// <summary>A new instance is created each time the activity is resolved.</summary>
    Transient,

    /// <summary>The activity methods are static and are invoked without an instance.</summary>
    Static,
}

/// <summary>
/// Overrides the default activity lifetime chosen by convention-based
/// auto-discovery. Without this attribute, static classes are registered as
/// <see cref="ActivityLifetime.Static"/> and instance classes as
/// <see cref="ActivityLifetime.Scoped"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ActivityLifetimeAttribute : Attribute
{
    /// <summary>Initializes the attribute with the activity lifetime to use.</summary>
    /// <param name="lifetime">The registration lifetime for the discovered activity class.</param>
    public ActivityLifetimeAttribute(ActivityLifetime lifetime) => Lifetime = lifetime;

    /// <summary>Gets the activity lifetime.</summary>
    public ActivityLifetime Lifetime { get; }
}
