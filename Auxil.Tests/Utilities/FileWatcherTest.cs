using Auxil.Utilities;
using JetBrains.Annotations;

namespace Auxil.Tests.Utilities;

[TestSubject(typeof(FileWatcher))]
public class FileWatcherTest : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly FileWatcherHarness _harness = new();

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Start_ThrowsWhenDirectoryMissing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"uphoric-fw-missing-{Guid.NewGuid():N}");
        using FileWatcher watcher = new(missing);

        Should.Throw<DirectoryNotFoundException>(watcher.Start);
    }

    [Fact]
    public void StartAndStop_AreIdempotent()
    {
        _harness.Watcher.Start();
        _harness.Watcher.IsRunning.ShouldBeTrue();

        _harness.Watcher.Stop();
        _harness.Watcher.IsRunning.ShouldBeFalse();
        _harness.Watcher.Stop();
        _harness.Watcher.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        _harness.Watcher.Dispose();

        Should.Throw<ObjectDisposedException>(_harness.Watcher.Start);
    }

    [Fact]
    public async Task CreatedFile_IsDeliveredWithRelativePath()
    {
        await _harness.WriteAsync("hello.txt", "hi");
        await _harness.WaitForEventAsync(FileWatcherEventKind.Created, "hello.txt");

        string fullPath = Path.GetFullPath(_harness.PathOf("hello.txt"));
        _harness
            .Events.Any(e =>
                e
                    is {
                        Kind: FileWatcherEventKind.Created,
                        RelativePath: "hello.txt",
                        OldFullPath: null,
                        IsDirectory: false,
                    }
                && e.FullPath == fullPath
            )
            .ShouldBeTrue();
    }

    [Fact]
    public async Task NestedFile_UsesForwardSlashRelativePath()
    {
        Directory.CreateDirectory(_harness.PathOf("sub"));
        await _harness.WaitForPathsAsync("sub");

        await _harness.WriteAsync("sub/nested.txt", "hi");
        await _harness.WaitForPathsAsync("sub/nested.txt");

        FileWatcherEventArgs[] delivered = _harness
            .Events.Where(e => e.RelativePath.EndsWith("nested.txt", StringComparison.Ordinal))
            .ToArray();
        delivered.ShouldNotBeEmpty();
        delivered.ShouldAllBe(e => e.RelativePath == "sub/nested.txt");
    }

    [Fact]
    public async Task ChangedFile_IsDelivered()
    {
        await _harness.WriteAsync("a.txt", "one");
        await _harness.WaitForPathsAsync("a.txt");

        await _harness.WriteAsync("a.txt", "two");

        await _harness.WaitForEventAsync(FileWatcherEventKind.Changed, "a.txt");

        _harness.Events.ShouldContain(e =>
            e.Kind == FileWatcherEventKind.Changed && e.RelativePath == "a.txt"
        );
    }

    [Fact]
    public async Task Filter_OnlyDeliversMatchingFiles()
    {
        using FileWatcherHarness harness = new(
            new FileWatcherOptions { Debounce = Debounce, Filter = "*.txt" }
        );

        await harness.WriteAsync("b.log");
        await harness.WriteAsync("a.txt");
        await harness.WaitForPathsAsync("a.txt");

        harness.Events.ShouldNotContain(e => e.RelativePath == "b.log");
    }

    [Fact]
    public async Task IncludeSubdirectoriesFalse_DeliversOnlyRootChanges()
    {
        using FileWatcherHarness harness = new(
            new FileWatcherOptions { Debounce = Debounce, IncludeSubdirectories = false }
        );
        Directory.CreateDirectory(harness.PathOf("sub"));
        await harness.WaitForPathsAsync("sub");

        await harness.WriteAsync("sub/nested.txt");
        await harness.WriteAsync("root.txt");
        await harness.WaitForPathsAsync("root.txt");

        harness.Events.ShouldNotContain(e => e.RelativePath == "sub/nested.txt");
    }

    [Fact]
    public async Task MultipleWrites_CoalesceIntoOneBurst()
    {
        using FileWatcherHarness slow = new(
            new FileWatcherOptions { Debounce = TimeSpan.FromSeconds(1) }
        );

        await slow.WriteAllAsync(["f0.txt", "f1.txt", "f2.txt"]);
        await slow.WaitForPathsAsync("f0.txt", "f1.txt", "f2.txt");
        slow.Watcher.Stop();

        slow.Bursts.Count.ShouldBe(1);
        slow.Bursts[0].Events.Select(e => e.RelativePath).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task Rename_IsDeliveredAsSingleRenamedEvent()
    {
        await _harness.WriteAsync("a.txt");
        await _harness.WaitForPathsAsync("a.txt");

        File.Move(_harness.PathOf("a.txt"), _harness.PathOf("b.txt"));
        await _harness.WaitForEventAsync(FileWatcherEventKind.Renamed, "b.txt");

        FileWatcherEventArgs renamed = _harness.Events.Single(e =>
            e is { Kind: FileWatcherEventKind.Renamed, RelativePath: "b.txt" }
        );
        renamed.OldRelativePath.ShouldBe("a.txt");
        renamed.OldFullPath.ShouldBe(Path.GetFullPath(_harness.PathOf("a.txt")));
    }

    [Fact]
    public async Task Rename_ToIgnoredPath_IsDropped()
    {
        using FileWatcherHarness harness = new(
            new FileWatcherOptions { Debounce = Debounce, IgnoredPaths = ["out.txt"] }
        );
        await harness.WriteAsync("a.txt");
        await harness.WaitForPathsAsync("a.txt");

        File.Move(harness.PathOf("a.txt"), harness.PathOf("out.txt"));
        await harness.WriteAsync("after.txt");
        await harness.WaitForPathsAsync("after.txt");

        harness.Events.ShouldNotContain(e => e.Kind == FileWatcherEventKind.Renamed);
    }

    [Fact]
    public async Task IgnoredSegment_IsDropped()
    {
        using FileWatcherHarness harness = new(
            new FileWatcherOptions { Debounce = Debounce, IgnoredPaths = ["bin"] }
        );

        Directory.CreateDirectory(harness.PathOf("bin"));
        await harness.WriteAsync("bin/a.dll");
        await harness.WriteAsync("after.txt");
        await harness.WaitForPathsAsync("after.txt");

        harness.Events.ShouldAllBe(e => e.RelativePath == "after.txt");
    }

    [Fact]
    public async Task DeletedFile_IsDelivered()
    {
        await _harness.WriteAsync("a.txt");
        await _harness.WaitForPathsAsync("a.txt");

        File.Delete(_harness.PathOf("a.txt"));

        await _harness.WaitForEventAsync(FileWatcherEventKind.Deleted, "a.txt");

        _harness.Events.ShouldContain(e =>
            e.Kind == FileWatcherEventKind.Deleted && e.RelativePath == "a.txt"
        );
    }

    [Fact]
    public async Task DirectoryCreated_ReportsIsDirectory()
    {
        Directory.CreateDirectory(_harness.PathOf("assets"));
        await _harness.WaitForPathsAsync("assets");

        FileWatcherEventArgs? delivered = _harness.Events.FirstOrDefault(e =>
            e.RelativePath == "assets"
        );
        delivered.ShouldNotBeNull();
        delivered.IsDirectory.ShouldBeTrue();
    }

    [Fact]
    public async Task Stop_FlushesPendingBurst()
    {
        using FileWatcherHarness slow = new(
            new FileWatcherOptions { Debounce = TimeSpan.FromMinutes(5) }
        );

        slow.Watcher.EnqueueEvent(
            FileWatcherEventKind.Created,
            slow.PathOf("last.txt"),
            oldFullPath: null
        );
        slow.Watcher.Stop();
        await slow.WaitForPathsAsync("last.txt");

        slow.Events.ShouldContain(e => e.RelativePath == "last.txt");
    }

    [Fact]
    public async Task Bursts_AreRaisedSerially()
    {
        int concurrent = 0;
        int overlaps = 0;
        _harness.Watcher.Burst += (_, _) =>
        {
            if (Interlocked.Increment(ref concurrent) != 1)
            {
                Interlocked.Increment(ref overlaps);
            }

            Thread.Yield();
            Interlocked.Decrement(ref concurrent);
        };
        string[] paths = Enumerable.Range(0, 10).Select(i => $"s{i}.txt").ToArray();

        await _harness.WriteAllAsync(paths);
        await _harness.WaitForPathsAsync(paths);
        _harness.Watcher.Stop();

        Volatile.Read(ref overlaps).ShouldBe(0);
    }

    [Fact]
    public async Task ThrowingBurstHandler_DoesNotStopFutureDispatch()
    {
        using FileWatcherHarness harness = new(new FileWatcherOptions { Debounce = Debounce });
        int callbackCount = 0;
        TaskCompletionSource firstCallback = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource secondCallback = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        harness.Watcher.Burst += (_, _) =>
        {
            if (Interlocked.Increment(ref callbackCount) == 1)
            {
                firstCallback.SetResult();
                throw new InvalidOperationException("expected callback failure");
            }

            secondCallback.TrySetResult();
        };

        await harness.WriteAsync("first.txt");
        await firstCallback.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

        await harness.WriteAsync("second.txt");
        await secondCallback.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Volatile.Read(ref callbackCount).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Start_DuringTimedOutStopIsRejectedUntilDispatcherExits()
    {
        using FileWatcherHarness harness = new(new FileWatcherOptions { Debounce = Debounce });
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
            await harness.WriteAsync("blocked.txt");
            await callbackStarted.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

            await Task.Run(harness.Watcher.Stop, TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Should.Throw<InvalidOperationException>(harness.Watcher.Start);

            releaseCallback.Set();
            await StartWhenDispatcherHasExitedAsync(harness.Watcher);
        }
        finally
        {
            releaseCallback.Set();
        }
    }

    private static async Task StartWhenDispatcherHasExitedAsync(FileWatcher watcher)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        deadline.CancelAfter(TimeSpan.FromSeconds(5));

        while (true)
        {
            try
            {
                watcher.Start();
                return;
            }
            catch (InvalidOperationException) when (!deadline.IsCancellationRequested)
            {
                await Task.Yield();
            }
        }
    }
}
