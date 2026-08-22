using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TemporalSharp.Analyzers.Diagnostics;

namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Curated lists of members that are non-deterministic (or otherwise unsafe) in
/// workflow code. Keys use <see cref="SymbolKeys.Member"/>. This is the .NET
/// equivalent of workflowcheck's IdentRefs: because C# reference assemblies have
/// no method bodies, we cannot walk the BCL transitively and must enumerate
/// entry points instead.
/// </summary>
internal static class DenyList
{
    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> Members = BuildMembers();

    // Constructors keyed by "ContainingType..ctor". Object-creation expressions
    // are matched separately so we can require an empty argument list for
    // parameterless non-deterministic constructors (e.g. new Random()).
    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> Constructors =
        new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal)
        {
            ["System.Random..ctor"] = DiagnosticDescriptors.NonDeterministicRandomness,
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static bool TryGetMember(string key, out DiagnosticDescriptor? descriptor)
        => Members.TryGetValue(key, out descriptor);

    public static bool TryGetConstructor(string key, out DiagnosticDescriptor? descriptor)
        => Constructors.TryGetValue(key, out descriptor);

    private static ImmutableDictionary<string, DiagnosticDescriptor> BuildMembers()
    {
        var entries = new List<(string Key, DiagnosticDescriptor Descriptor)>();

        // TMP0101 — wall-clock time
        foreach (var name in new[]
        {
            "System.DateTime.Now",
            "System.DateTime.UtcNow",
            "System.DateTime.Today",
            "System.DateTimeOffset.Now",
            "System.DateTimeOffset.UtcNow",
            "System.TimeZoneInfo.Local",
            "System.Environment.TickCount",
            "System.Environment.TickCount64",
        })
        {
            entries.Add((name, DiagnosticDescriptors.WallClockTime));
        }

        // TMP0111 — sleep / block
        foreach (var name in new[]
        {
            "System.Threading.Thread.Sleep",
            "System.Threading.Tasks.Task.Delay",
            "System.Threading.Tasks.Task.Wait",
            "System.Threading.Tasks.Task.WaitAll",
            "System.Threading.Tasks.Task.WaitAny",
        })
        {
            entries.Add((name, DiagnosticDescriptors.BlockOrSleep));
        }

        // TMP0121 — randomness / identity
        foreach (var name in new[]
        {
            "System.Guid.NewGuid",
            "System.Random.Shared",
        })
        {
            entries.Add((name, DiagnosticDescriptors.NonDeterministicRandomness));
        }

        // TMP0131 — I/O and environment access
        foreach (var name in new[]
        {
            "System.Environment.GetEnvironmentVariable",
            "System.Environment.GetEnvironmentVariables",
            "System.Console.WriteLine",
            "System.Console.Write",
            "System.IO.File.ReadAllText",
            "System.IO.File.ReadAllLines",
            "System.IO.File.ReadAllBytes",
            "System.IO.File.WriteAllText",
            "System.IO.File.WriteAllBytes",
            "System.IO.File.Exists",
            "System.IO.Directory.GetCurrentDirectory",
            "System.IO.Directory.GetFiles",
            "System.Net.Http.HttpClient.GetAsync",
            "System.Net.Http.HttpClient.GetStringAsync",
            "System.Net.Http.HttpClient.PostAsync",
            "System.Net.Http.HttpClient.SendAsync",
        })
        {
            entries.Add((name, DiagnosticDescriptors.IoOrEnvironmentAccess));
        }

        return entries.ToImmutableDictionary(e => e.Key, e => e.Descriptor, StringComparer.Ordinal);
    }
}
