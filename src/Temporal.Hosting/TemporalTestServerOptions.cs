namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Test-server toggle. When <see cref="Enabled"/> is <c>true</c>, the starter
/// runs an in-process Temporal dev server instead of connecting to a real one,
/// mirroring <c>spring.temporal.test-server.enabled</c>.
/// </summary>
public sealed class TemporalTestServerOptions
{
    /// <summary>Gets or sets a value indicating whether the test server is used.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the local port the test server binds to. The default of
    /// <c>0</c> asks the OS for an ephemeral free port, which is then shared
    /// with the lazy client after startup. Set a concrete port to pin the
    /// server for reproducibility.
    /// </summary>
    public int Port { get; set; }
}
