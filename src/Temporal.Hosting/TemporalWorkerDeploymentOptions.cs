using Temporalio.Common;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Worker deployment/versioning configuration, bound from
/// <c>Temporal:Workers:&lt;queue&gt;:Deployment</c>. Versioning is opt-in via
/// <see cref="UseWorkerVersioning"/>: a versioned worker reports its deployment
/// version to the server but receives no tasks until a Current (or Ramping)
/// version is promoted server-side.
/// </summary>
public sealed class TemporalWorkerDeploymentOptions
{
    /// <summary>Gets or sets the worker deployment name.</summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the build ID identifying this version within the deployment.
    /// This is the canonical name; <see cref="Version"/> is an alias for it.
    /// </summary>
    public string? BuildId { get; set; }

    /// <summary>
    /// Gets or sets an alias for <see cref="BuildId"/>. When both are set,
    /// <see cref="BuildId"/> wins.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the worker opts into Worker
    /// Versioning. When enabled, both <see cref="DeploymentName"/> and a build
    /// ID are required.
    /// </summary>
    public bool UseWorkerVersioning { get; set; }

    /// <summary>
    /// Gets or sets the default versioning behavior for workflows this worker
    /// executes. <c>null</c> leaves the SDK default (<c>Unspecified</c>, which
    /// requires each workflow to declare its own behavior).
    /// </summary>
    public VersioningBehavior? DefaultVersioningBehavior { get; set; }
}
