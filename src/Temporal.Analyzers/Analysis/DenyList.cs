using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Curated lists of members that are non-deterministic (or otherwise unsafe) in
/// workflow code. Keys use <see cref="SymbolKeys.Member"/>. Because C# reference
/// assemblies have no method bodies, we cannot walk the BCL transitively and
/// must enumerate entry points instead.
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
            ["System.Security.Cryptography.RNGCryptoServiceProvider..ctor"] = DiagnosticDescriptors.CryptoRandomness,
        }.ToImmutableDictionary(StringComparer.Ordinal);

    // Constructors matched regardless of argument count (e.g. new Thread(...)
    // takes a delegate, new BackgroundWorker() takes none). Unlike the
    // parameterless-only Constructors list above.
    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> AnyArgConstructors =
        new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal)
        {
            ["System.Threading.Thread..ctor"] = DiagnosticDescriptors.ConcurrentExecution,
            ["System.ComponentModel.BackgroundWorker..ctor"] = DiagnosticDescriptors.ConcurrentExecution,
            ["System.Threading.Tasks.TaskCompletionSource<TResult>..ctor"] = DiagnosticDescriptors.ManualTaskCoordination,
            ["System.Threading.Tasks.TaskCompletionSource..ctor"] = DiagnosticDescriptors.ManualTaskCoordination,
            ["System.Threading.AsyncLocal<T>..ctor"] = DiagnosticDescriptors.AmbientState,
            ["System.Threading.ThreadLocal<T>..ctor"] = DiagnosticDescriptors.AmbientState,
            ["System.Threading.Timer..ctor"] = DiagnosticDescriptors.TimerScheduling,
            ["System.Threading.PeriodicTimer..ctor"] = DiagnosticDescriptors.TimerScheduling,
            ["System.Timers.Timer..ctor"] = DiagnosticDescriptors.TimerScheduling,
            ["System.WeakReference..ctor"] = DiagnosticDescriptors.WeakReference,
            ["System.WeakReference<T>..ctor"] = DiagnosticDescriptors.WeakReference,
            ["System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>..ctor"] = DiagnosticDescriptors.WeakReference,
        }.ToImmutableDictionary(StringComparer.Ordinal);

    // Constructors matched only when given at least one argument (e.g.
    // new CancellationTokenSource(TimeSpan) schedules a system timer, whereas the
    // parameterless CancellationTokenSource() is a harmless token factory).
    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> NonEmptyArgConstructors =
        new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal)
        {
            ["System.Threading.CancellationTokenSource..ctor"] = DiagnosticDescriptors.TimerScheduling,
            ["System.IO.FileStream..ctor"] = DiagnosticDescriptors.IoOrEnvironmentAccess,
            ["System.IO.StreamReader..ctor"] = DiagnosticDescriptors.IoOrEnvironmentAccess,
            ["System.IO.StreamWriter..ctor"] = DiagnosticDescriptors.IoOrEnvironmentAccess,
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static bool TryGetMember(string key, out DiagnosticDescriptor? descriptor)
        => Members.TryGetValue(key, out descriptor);

    public static bool TryGetConstructor(string key, out DiagnosticDescriptor? descriptor)
        => Constructors.TryGetValue(key, out descriptor);

    public static bool TryGetAnyArgConstructor(string key, out DiagnosticDescriptor? descriptor)
        => AnyArgConstructors.TryGetValue(key, out descriptor);

    public static bool TryGetNonEmptyArgConstructor(string key, out DiagnosticDescriptor? descriptor)
        => NonEmptyArgConstructors.TryGetValue(key, out descriptor);

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
            "System.TimeProvider.GetUtcNow",
            "System.TimeProvider.GetLocalNow",
        })
        {
            entries.Add((name, DiagnosticDescriptors.WallClockTime));
        }

        // TMP0111 — sleep / block
        foreach (var name in new[]
        {
            "System.Threading.Thread.Sleep",
            "System.Threading.Thread.Join",
            "System.Threading.Thread.SpinWait",
            "System.Threading.Tasks.Task.Delay",
            "System.Threading.Tasks.Task.Wait",
            "System.Threading.Tasks.Task.WaitAll",
            "System.Threading.Tasks.Task.WaitAny",
            // Synchronous waits on task results / awaiters.
            "System.Threading.Tasks.Task<TResult>.Result",
            "System.Threading.Tasks.ValueTask<TResult>.Result",
            "System.Runtime.CompilerServices.TaskAwaiter.GetResult",
            "System.Runtime.CompilerServices.TaskAwaiter<TResult>.GetResult",
            "System.Runtime.CompilerServices.ValueTaskAwaiter.GetResult",
            "System.Runtime.CompilerServices.ValueTaskAwaiter<TResult>.GetResult",
            "System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter.GetResult",
            "System.Runtime.CompilerServices.ConfiguredTaskAwaitable<TResult>.ConfiguredTaskAwaiter.GetResult",
            "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter.GetResult",
            "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter.GetResult",
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

        // TMP0102 — Stopwatch (elapsed wall-clock time)
        foreach (var name in new[]
        {
            "System.Diagnostics.Stopwatch.StartNew",
            "System.Diagnostics.Stopwatch.Start",
            "System.Diagnostics.Stopwatch.Stop",
            "System.Diagnostics.Stopwatch.Restart",
            "System.Diagnostics.Stopwatch.GetTimestamp",
            "System.Diagnostics.Stopwatch.Frequency",
            "System.Diagnostics.Stopwatch.Elapsed",
            "System.Diagnostics.Stopwatch.ElapsedMilliseconds",
            "System.Diagnostics.Stopwatch.ElapsedTicks",
            "System.Diagnostics.Stopwatch.GetElapsedTime",
            "System.TimeProvider.GetTimestamp",
            "System.TimeProvider.GetElapsedTime",
        })
        {
            entries.Add((name, DiagnosticDescriptors.StopwatchUsage));
        }

        // TMP0131 — I/O and environment access
        foreach (var name in new[]
        {
            "System.Environment.GetEnvironmentVariable",
            "System.Environment.GetEnvironmentVariables",
            "System.Environment.CurrentDirectory",
            "System.Environment.MachineName",
            "System.Environment.UserName",
            "System.Environment.OSVersion",
            "System.Environment.ProcessorCount",
            "System.Environment.GetFolderPath",
            "System.IO.Path.GetTempPath",
            "System.Console.Read",
            "System.Console.ReadLine",
            "System.Console.ReadKey",
            "System.IO.File.ReadAllText",
            "System.IO.File.ReadAllTextAsync",
            "System.IO.File.ReadAllLines",
            "System.IO.File.ReadAllLinesAsync",
            "System.IO.File.ReadAllBytes",
            "System.IO.File.ReadAllBytesAsync",
            "System.IO.File.WriteAllText",
            "System.IO.File.WriteAllTextAsync",
            "System.IO.File.WriteAllLines",
            "System.IO.File.WriteAllLinesAsync",
            "System.IO.File.WriteAllBytes",
            "System.IO.File.WriteAllBytesAsync",
            "System.IO.File.AppendAllText",
            "System.IO.File.AppendAllTextAsync",
            "System.IO.File.AppendAllLines",
            "System.IO.File.AppendAllLinesAsync",
            "System.IO.File.Exists",
            "System.IO.File.Delete",
            "System.IO.File.Move",
            "System.IO.File.Copy",
            "System.IO.File.Open",
            "System.IO.File.OpenRead",
            "System.IO.File.OpenWrite",
            "System.IO.File.OpenText",
            "System.IO.File.Create",
            "System.IO.File.CreateText",
            "System.IO.Directory.GetCurrentDirectory",
            "System.IO.Directory.GetFiles",
            "System.IO.Directory.GetDirectories",
            "System.IO.Directory.EnumerateFiles",
            "System.IO.Directory.EnumerateDirectories",
            "System.IO.Directory.CreateDirectory",
            "System.IO.Directory.Delete",
            "System.IO.Directory.Exists",
            "System.IO.Directory.Move",
            "System.IO.StreamReader.Read",
            "System.IO.StreamReader.ReadLine",
            "System.IO.StreamReader.ReadToEnd",
            "System.IO.StreamWriter.Write",
            "System.IO.StreamWriter.WriteLine",
            "System.Net.Http.HttpClient.GetAsync",
            "System.Net.Http.HttpClient.GetStringAsync",
            "System.Net.Http.HttpClient.GetByteArrayAsync",
            "System.Net.Http.HttpClient.GetStreamAsync",
            "System.Net.Http.HttpClient.PostAsync",
            "System.Net.Http.HttpClient.PutAsync",
            "System.Net.Http.HttpClient.DeleteAsync",
            "System.Net.Http.HttpClient.PatchAsync",
            "System.Net.Http.HttpClient.SendAsync",
            "System.Net.Dns.GetHostName",
            "System.Net.Dns.GetHostAddresses",
            "System.Net.Dns.GetHostEntry",
            "System.Net.Dns.Resolve",
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

        // TMP0141 — concurrency (no Workflow.* replacement; move to an activity)
        foreach (var name in new[]
        {
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

        // TMP0146 — task scheduling on the default scheduler (Workflow.RunTaskAsync)
        foreach (var name in new[]
        {
            "System.Threading.Tasks.Task.Run",
        })
        {
            entries.Add((name, DiagnosticDescriptors.ConcurrentTaskRun));
        }

        // TMP0142 — blocking synchronization primitives (no Workflow.* replacement)
        foreach (var name in new[]
        {
            "System.Threading.ManualResetEventSlim.Wait",
            "System.Threading.Monitor.Enter",
            "System.Threading.Monitor.TryEnter",
            "System.Threading.Monitor.Exit",
            "System.Threading.Monitor.Wait",
            "System.Threading.Monitor.Pulse",
            "System.Threading.Monitor.PulseAll",
            // WaitHandle.WaitOne/WaitAny/WaitAll are the base declarations; on modern
            // .NET the subclasses (Semaphore, Mutex, EventWaitHandle,
            // ManualResetEvent, AutoResetEvent) don't override WaitOne, so they all
            // resolve to the base member.
            "System.Threading.WaitHandle.WaitOne",
            "System.Threading.WaitHandle.WaitAny",
            "System.Threading.WaitHandle.WaitAll",
            "System.Threading.ReaderWriterLock.AcquireReaderLock",
            "System.Threading.ReaderWriterLock.AcquireWriterLock",
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

        // TMP0143 — raw task scheduling. Only the APIs that actually leave the
        // deterministic scheduler are listed: ContinueWith/ContinueWhenAll/
        // ContinueWhenAny default to TaskScheduler.Current (the workflow scheduler)
        // and non-generic Task.WhenAny is what Workflow.WhenAnyAsync wraps, so those
        // are intentionally excluded; generic Task.WhenAny<T> is detected separately.
        foreach (var name in new[]
        {
            "System.Threading.CancellationTokenSource.CancelAsync",
        })
        {
            entries.Add((name, DiagnosticDescriptors.TaskScheduling));
        }

        // TMP0148 — Task.WhenAll is technically safe, but the wrapper is
        // recommended (lower severity than the raw-scheduling rules above).
        entries.Add(("System.Threading.Tasks.Task.WhenAll", DiagnosticDescriptors.TaskWhenAll));

        // TMP0145 — reflection / dynamic invocation
        foreach (var name in new[]
        {
            "System.Activator.CreateInstance",
            "System.Reflection.Assembly.Load",
            "System.Reflection.Assembly.LoadFrom",
            "System.Reflection.Assembly.LoadFile",
            "System.Reflection.Assembly.LoadWithPartialName",
            "System.Reflection.Assembly.GetTypes",
            "System.Reflection.Assembly.GetExportedTypes",
            "System.Type.GetType",
            "System.Type.GetMethod",
            "System.Type.GetMethods",
            "System.Type.GetProperty",
            "System.Type.GetProperties",
            "System.Type.GetField",
            "System.Type.GetFields",
            "System.Type.GetMembers",
            "System.Type.GetConstructors",
            "System.Type.GetEvent",
            "System.Type.GetEvents",
            "System.Reflection.MethodInfo.Invoke",
            "System.Reflection.MethodBase.Invoke",
            "System.Reflection.ConstructorInfo.Invoke",
            "System.Delegate.DynamicInvoke",
        })
        {
            entries.Add((name, DiagnosticDescriptors.ReflectionInvocation));
        }

        // TMP1106 — ambient AsyncLocal/ThreadLocal state (.Value access)
        foreach (var name in new[]
        {
            "System.Threading.AsyncLocal<T>.Value",
            "System.Threading.ThreadLocal<T>.Value",
        })
        {
            entries.Add((name, DiagnosticDescriptors.AmbientState));
        }

        // TMP0122 — cryptographic randomness
        foreach (var name in new[]
        {
            "System.Security.Cryptography.RandomNumberGenerator.Create",
            "System.Security.Cryptography.RandomNumberGenerator.GetBytes",
            "System.Security.Cryptography.RandomNumberGenerator.GetInt32",
            "System.Security.Cryptography.RandomNumberGenerator.GetInt64",
            "System.Security.Cryptography.RandomNumberGenerator.GetNonZeroBytes",
            "System.Security.Cryptography.RandomNumberGenerator.GetString",
            "System.Security.Cryptography.RandomNumberGenerator.GetHexString",
            "System.Security.Cryptography.RandomNumberGenerator.Fill",
            "System.Security.Cryptography.RandomNumberGenerator.GetItems",
            "System.Security.Cryptography.RandomNumberGenerator.Shuffle",
            "System.Security.Cryptography.RNGCryptoServiceProvider.GetBytes",
            "System.Security.Cryptography.RNGCryptoServiceProvider.GetNonZeroBytes",
        })
        {
            entries.Add((name, DiagnosticDescriptors.CryptoRandomness));
        }

        // TMP0172 — wall-clock timer scheduling
        foreach (var name in new[]
        {
            "System.Threading.Timer.Change",
            "System.Threading.Timer.ChangeAsync",
            "System.Threading.PeriodicTimer.WaitForNextTickAsync",
            "System.Timers.Timer.Start",
            "System.Threading.CancellationTokenSource.CancelAfter",
            "System.Threading.Tasks.Task.WaitAsync",
            "System.Threading.Tasks.Task<TResult>.WaitAsync",
        })
        {
            entries.Add((name, DiagnosticDescriptors.TimerScheduling));
        }

        // TMP0174 — weak references / GC-timing dependence
        foreach (var name in new[]
        {
            "System.WeakReference.Target",
            "System.WeakReference.IsAlive",
            "System.WeakReference<T>.TryGetTarget",
            "System.WeakReference<T>.SetTarget",
            "System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>.Add",
            "System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>.TryGetValue",
            "System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>.GetValue",
            "System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>.GetOrCreateValue",
            "System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>.Remove",
        })
        {
            entries.Add((name, DiagnosticDescriptors.WeakReference));
        }

        return entries.ToImmutableDictionary(e => e.Key, e => e.Descriptor, StringComparer.Ordinal);
    }
}
