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

    // Constructors of concurrency primitives. Unlike the parameterless-only
    // Constructors list, these are matched regardless of argument count (e.g.
    // new Thread(...) takes a delegate, new BackgroundWorker() takes none).
    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> ConcurrencyConstructors =
        new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal)
        {
            ["System.Threading.Thread..ctor"] = DiagnosticDescriptors.ConcurrentExecution,
            ["System.ComponentModel.BackgroundWorker..ctor"] = DiagnosticDescriptors.ConcurrentExecution,
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static bool TryGetMember(string key, out DiagnosticDescriptor? descriptor)
        => Members.TryGetValue(key, out descriptor);

    public static bool TryGetConstructor(string key, out DiagnosticDescriptor? descriptor)
        => Constructors.TryGetValue(key, out descriptor);

    public static bool TryGetConcurrencyConstructor(string key, out DiagnosticDescriptor? descriptor)
        => ConcurrencyConstructors.TryGetValue(key, out descriptor);

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
            "System.Diagnostics.Process.Start",
            "System.Net.Sockets.Socket.Connect",
            "System.Net.Sockets.Socket.Send",
            "System.Net.Sockets.Socket.Receive",
            "System.Net.Sockets.NetworkStream.Read",
            "System.Net.Sockets.NetworkStream.Write",
        })
        {
            entries.Add((name, DiagnosticDescriptors.IoOrEnvironmentAccess));
        }

        // TMP0141 — concurrency
        foreach (var name in new[]
        {
            "System.Threading.Tasks.Task.Run",
            "System.Threading.Tasks.TaskFactory.StartNew",
            "System.Threading.ThreadPool.QueueUserWorkItem",
            "System.Threading.Tasks.Parallel.For",
            "System.Threading.Tasks.Parallel.ForEach",
            "System.Threading.Tasks.Parallel.Invoke",
            "System.ComponentModel.BackgroundWorker.RunWorkerAsync",
            "System.Threading.Thread.Start",
        })
        {
            entries.Add((name, DiagnosticDescriptors.ConcurrentExecution));
        }

        // TMP0142 — blocking synchronization primitives
        foreach (var name in new[]
        {
            "System.Threading.SemaphoreSlim.Wait",
            "System.Threading.SemaphoreSlim.WaitAsync",
            "System.Threading.ManualResetEventSlim.Wait",
            "System.Threading.Monitor.Enter",
            "System.Threading.Monitor.TryEnter",
            "System.Threading.Monitor.Exit",
            "System.Threading.Monitor.Wait",
            "System.Threading.Monitor.Pulse",
            "System.Threading.Monitor.PulseAll",
            "System.Threading.Mutex.WaitOne",
            "System.Threading.AutoResetEvent.WaitOne",
            "System.Threading.ReaderWriterLockSlim.EnterReadLock",
            "System.Threading.ReaderWriterLockSlim.EnterWriteLock",
            "System.Threading.ReaderWriterLockSlim.EnterUpgradeableReadLock",
            "System.Threading.SpinWait.SpinOnce",
            "System.Threading.CountdownEvent.Wait",
            "System.Threading.Barrier.SignalAndWait",
            "System.Threading.Channels.ChannelReader<T>.ReadAsync",
            "System.Threading.Channels.ChannelReader<T>.WaitToReadAsync",
            "System.Threading.Channels.ChannelWriter<T>.WriteAsync",
            "System.Threading.Channels.ChannelWriter<T>.WaitToWriteAsync",
            "System.Collections.Concurrent.BlockingCollection<T>.Add",
            "System.Collections.Concurrent.BlockingCollection<T>.Take",
        })
        {
            entries.Add((name, DiagnosticDescriptors.BlockingPrimitive));
        }

        return entries.ToImmutableDictionary(e => e.Key, e => e.Descriptor, StringComparer.Ordinal);
    }
}
