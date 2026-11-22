using Auxil.Utilities;
using JetBrains.Annotations;

namespace Auxil.Tests.Utilities;

[TestSubject(typeof(FileWatcher))]
public class FileWatcherLifecycleTest
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public Task Stop_DoesNotHoldTheLifecycleLockWhileJoining() =>
        AssertLockIsFreeWhileJoiningAsync(watcher => watcher.Stop());

    [Fact]
    public Task Dispose_DoesNotHoldTheLifecycleLockWhileJoining() =>
        AssertLockIsFreeWhileJoiningAsync(watcher => watcher.Dispose());

    [Fact]
    public async Task ConcurrentDispose_DeliversPendingChangesExactlyOnce()
    {
        using FileWatcherHarness harness = new(
            new FileWatcherOptions { Debounce = TimeSpan.FromMinutes(5) }
        );
        string[] paths = ["a.txt", "b.txt", "c.txt"];
        EnqueueAll(harness.Watcher, harness, paths);

        using Barrier start = new(4);
        Task[] disposers = Enumerable
            .Range(0, 4)
            .Select(_ =>
                Task.Run(
                    () =>
                    {
                        start.SignalAndWait(TestContext.Current.CancellationToken);
                        harness.Watcher.Dispose();
                    },
                    TestContext.Current.CancellationToken
                )
            )
            .ToArray();
        await Task.WhenAll(disposers).WaitAsync(Patience, TestContext.Current.CancellationToken);

        harness.Events.Select(e => e.RelativePath).OrderBy(path => path).ShouldBe(paths);
        harness.Watcher.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Start_WhileDisposeIsWaitingOnTheDispatcher_ReportsDisposedNotShuttingDown()
    {
        using FileWatcherHarness harness = new();
        TaskCompletionSource callbackStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using ManualResetEventSlim releaseCallback = new(false);
        harness.Watcher.Burst += (_, _) =>
        {
            callbackStarted.TrySetResult();
            releaseCallback.Wait(Patience, TestContext.Current.CancellationToken);
        };

        try
        {
            EnqueueAll(harness.Watcher, harness, ["blocked.txt"]);
            await callbackStarted.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

            Task disposing = Task.Run(
                harness.Watcher.Dispose,
                TestContext.Current.CancellationToken
            );
            await WaitUntilStoppedAsync(harness.Watcher);

            Should.Throw<ObjectDisposedException>(harness.Watcher.Start);
            await Task.Run(harness.Watcher.Dispose, TestContext.Current.CancellationToken)
                .WaitAsync(Patience, TestContext.Current.CancellationToken);

            releaseCallback.Set();
            await disposing.WaitAsync(Patience, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseCallback.Set();
        }
    }

    private static async Task AssertLockIsFreeWhileJoiningAsync(Action<FileWatcher> stopping)
    {
        using FileWatcherHarness harness = new();
        TaskCompletionSource callbackStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using ManualResetEventSlim releaseCallback = new(false);
        harness.Watcher.Burst += (_, _) =>
        {
            callbackStarted.TrySetResult();
            releaseCallback.Wait(Patience, TestContext.Current.CancellationToken);
        };

        try
        {
            EnqueueAll(harness.Watcher, harness, ["blocked.txt"]);
            await callbackStarted.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

            Task stopper = Task.Run(
                () => stopping(harness.Watcher),
                TestContext.Current.CancellationToken
            );
            await WaitUntilStoppedAsync(harness.Watcher);

            Task contender = Task.Run(
                () => Record.Exception(harness.Watcher.Start),
                TestContext.Current.CancellationToken
            );
            await contender.WaitAsync(Patience, TestContext.Current.CancellationToken);

            stopper.IsCompleted.ShouldBeFalse();

            releaseCallback.Set();
            await stopper.WaitAsync(Patience, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseCallback.Set();
        }
    }

    private static void EnqueueAll(
        FileWatcher watcher,
        FileWatcherHarness harness,
        string[] relativePaths
    ) =>
        Array.ForEach(relativePaths, path =>
            watcher.EnqueueEvent(
                FileWatcherEventKind.Created,
                harness.PathOf(path),
                oldFullPath: null
            )
        );

    private static async Task WaitUntilStoppedAsync(FileWatcher watcher)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        deadline.CancelAfter(Patience);

        while (watcher.IsRunning)
        {
            await Task.Yield();
            deadline.Token.ThrowIfCancellationRequested();
        }
    }
}
