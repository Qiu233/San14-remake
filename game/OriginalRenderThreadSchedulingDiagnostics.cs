using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Opt-in observations of the existing render callback and its actual
/// awaiting caller. Does not wrap tasks, move continuations, change submission
/// order or measure GPU execution. Call Snapshot after the original request.</summary>
public static class OriginalRenderThreadSchedulingDiagnostics
{
    private static readonly AsyncLocal<Capture?> Current = new();

    public static Capture BeginCapture(long actualFrameNumber, string scope)
    {
        var capture = new Capture(actualFrameNumber, scope, Current.Value);
        Current.Value = capture;
        return capture;
    }

    /// <summary>Called immediately before the existing CallOnRenderThread.
    /// The returned ticket is captured by that original callback.</summary>
    public static Operation? Enqueued(Task actualTask,
        [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        => Current.Value?.Enqueued(actualTask, member, file, line);

    /// <summary>Called immediately after an existing await in the consumer,
    /// using the exact original task. This records the consumer's real resume.
    /// It does not introduce an extra continuation or observe a wrapper task.</summary>
    public static void CallerResumed(Task actualTask) => Current.Value?.CallerResumed(actualTask);

    /// <summary>Sequential boundaries inside the original method. Disabled
    /// captures do not allocate a trace or read the clock. Stages include the
    /// original awaits; nested traces are separate and must not be summed.</summary>
    public static CpuTrace? BeginCpuTrace(string operation, int sourcePassIndex)
        => Current.Value?.BeginCpuTrace(operation, sourcePassIndex);

    public sealed class Capture : IDisposable
    {
        private readonly Capture? previous;
        private readonly ConcurrentDictionary<Task, Operation> operations = new(ReferenceEqualityComparer.Instance);
        private readonly ConcurrentBag<CpuTrace> cpuTraces = new();
        private readonly long start = Stopwatch.GetTimestamp();
        private bool disposed;
        internal Capture(long actualFrameNumber, string scope, Capture? previous)
            => (FrameNumber, Scope, this.previous) = (actualFrameNumber, scope, previous);
        public long FrameNumber { get; }
        public string Scope { get; }
        internal Operation Enqueued(Task task, string member, string file, int line)
        {
            var operation = new Operation(member, file, line);
            operations.TryAdd(task, operation);
            return operation;
        }
        internal void CallerResumed(Task task)
        {
            if (operations.TryGetValue(task, out var operation)) operation.MarkCallerResumed();
        }
        internal CpuTrace BeginCpuTrace(string operation, int sourcePassIndex)
        {
            var trace = new CpuTrace(operation, sourcePassIndex);
            cpuTraces.Add(trace);
            return trace;
        }
        public Snapshot CaptureSnapshot(int maximumSites = 16, int maximumSlowOperations = 12)
        {
            var rows = operations.Values.Select(operation => operation.Read()).ToArray();
            var sites = rows.GroupBy(row => (row.File, row.Member, row.Line)).Select(group =>
            {
                var values = group.ToArray();
                return new SiteSummary(group.Key.File, group.Key.Member, group.Key.Line, values.Length,
                    values.Count(row => row.CallbackMilliseconds.HasValue), values.Count(row => row.ResumeDelayMilliseconds.HasValue),
                    values.Sum(row => row.QueueWaitMilliseconds ?? 0), values.Sum(row => row.CallbackMilliseconds ?? 0),
                    values.Sum(row => row.ResumeDelayMilliseconds ?? 0),
                    values.Max(row => row.QueueWaitMilliseconds ?? 0), values.Max(row => row.CallbackMilliseconds ?? 0),
                    values.Max(row => row.ResumeDelayMilliseconds ?? 0));
            }).OrderByDescending(site => site.QueueWaitMilliseconds + site.CallbackMilliseconds + site.ResumeDelayMilliseconds)
                .Take(Math.Max(0, maximumSites)).ToArray();
            var slow = rows.OrderByDescending(row => row.EnqueueToCallerMilliseconds ?? row.EnqueueToCallbackEndMilliseconds ?? 0)
                .Take(Math.Max(0, maximumSlowOperations)).ToArray();
            return new(FrameNumber, Scope, Milliseconds(Stopwatch.GetTimestamp() - start), rows.Length,
                rows.Count(row => row.CallbackMilliseconds.HasValue), rows.Count(row => row.ResumeDelayMilliseconds.HasValue),
                rows.Sum(row => row.QueueWaitMilliseconds ?? 0), rows.Sum(row => row.CallbackMilliseconds ?? 0),
                rows.Sum(row => row.ResumeDelayMilliseconds ?? 0), sites, slow)
                { CpuStages = cpuTraces.SelectMany(trace => trace.Read()).GroupBy(row =>
                    (row.Operation, row.SourcePassIndex, row.Stage)).Select(group => new CpuStageSummary(
                        group.Key.Operation, group.Key.SourcePassIndex, group.Key.Stage, group.Count(),
                        group.Sum(row => row.Milliseconds), group.Max(row => row.Milliseconds)))
                    .OrderBy(row => row.Operation).ThenBy(row => row.SourcePassIndex).ThenBy(row => row.Stage).ToArray() };
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ReferenceEquals(Current.Value, this)) Current.Value = previous;
            operations.Clear();
            cpuTraces.Clear();
        }
    }

    public sealed class CpuTrace
    {
        private readonly string operation;
        private readonly int sourcePassIndex;
        private readonly List<CpuStageRow> stages = new();
        private long previous = Stopwatch.GetTimestamp();
        internal CpuTrace(string operation, int sourcePassIndex)
            => (this.operation, this.sourcePassIndex) = (operation, sourcePassIndex);
        /// <summary>Finish the named stage and start the next interval.
        /// Call from the original sequential caller, after its existing await.</summary>
        public void Mark(string stage)
        {
            long now = Stopwatch.GetTimestamp();
            stages.Add(new(operation, sourcePassIndex, stage, Milliseconds(now - previous), Environment.CurrentManagedThreadId));
            previous = now;
        }
        internal IReadOnlyList<CpuStageRow> Read() => stages.ToArray();
    }

    public sealed class Operation
    {
        private readonly long enqueued = Stopwatch.GetTimestamp();
        private readonly string member, file;
        private readonly int line, enqueueThread = Environment.CurrentManagedThreadId;
        private readonly string? enqueueContext = SynchronizationContext.Current?.GetType().FullName;
        private long callbackStart, callbackEnd, resumed;
        private int callbackThread, resumedThread, callbackFailed;
        private string? resumedContext;
        internal Operation(string member, string file, int line) => (this.member, this.file, this.line) = (member, file, line);
        public void CallbackStarted()
        {
            callbackThread = Environment.CurrentManagedThreadId;
            Volatile.Write(ref callbackStart, Stopwatch.GetTimestamp());
        }
        /// <summary>Record after the original action returns or throws,
        /// immediately before signaling its original TCS.</summary>
        public void CallbackFinished(bool failed = false)
        {
            if (failed) callbackFailed = 1;
            Volatile.Write(ref callbackEnd, Stopwatch.GetTimestamp());
        }
        internal void MarkCallerResumed()
        {
            resumedThread = Environment.CurrentManagedThreadId;
            resumedContext = SynchronizationContext.Current?.GetType().FullName;
            Interlocked.CompareExchange(ref resumed, Stopwatch.GetTimestamp(), 0);
        }
        internal OperationRow Read()
        {
            long begin = Volatile.Read(ref callbackStart), end = Volatile.Read(ref callbackEnd), resume = Volatile.Read(ref resumed);
            return new(System.IO.Path.GetFileName(file), member, line, enqueueThread, callbackThread,
                resume == 0 ? null : resumedThread, enqueueContext, resumedContext, callbackFailed != 0,
                begin == 0 ? null : Milliseconds(begin - enqueued),
                end == 0 || begin == 0 ? null : Milliseconds(end - begin),
                resume == 0 || end == 0 ? null : Milliseconds(resume - end),
                end == 0 ? null : Milliseconds(end - enqueued), resume == 0 ? null : Milliseconds(resume - enqueued));
        }
    }

    public sealed record OperationRow(string File, string Member, int Line,
        int EnqueueThread, int CallbackThread, int? ResumedThread, string? EnqueueContext, string? ResumedContext, bool CallbackFailed,
        double? QueueWaitMilliseconds, double? CallbackMilliseconds, double? ResumeDelayMilliseconds,
        double? EnqueueToCallbackEndMilliseconds, double? EnqueueToCallerMilliseconds);
    public sealed record SiteSummary(string File, string Member, int Line, int Calls, int CompletedCallbacks, int ObservedCallerResumes,
        double QueueWaitMilliseconds, double CallbackMilliseconds, double ResumeDelayMilliseconds,
        double MaximumQueueWaitMilliseconds, double MaximumCallbackMilliseconds, double MaximumResumeDelayMilliseconds);
    public sealed record Snapshot(long FrameNumber, string Scope, double CaptureMilliseconds, int Calls, int CompletedCallbacks,
        int ObservedCallerResumes, double QueueWaitMilliseconds, double CallbackMilliseconds, double ObservedResumeDelayMilliseconds,
        IReadOnlyList<SiteSummary> Sites, IReadOnlyList<OperationRow> SlowOperations)
    {
        public IReadOnlyList<CpuStageSummary> CpuStages { get; init; } = Array.Empty<CpuStageSummary>();
    }
    public sealed record CpuStageRow(string Operation, int SourcePassIndex, string Stage, double Milliseconds, int Thread);
    public sealed record CpuStageSummary(string Operation, int SourcePassIndex, string Stage, int Calls,
        double Milliseconds, double MaximumMilliseconds);
    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
