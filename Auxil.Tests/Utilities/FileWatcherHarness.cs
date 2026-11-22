using Auxil.Utilities;

namespace Auxil.Tests.Utilities;

internal sealed class FileWatcherHarness : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Lock _lock = new();
    private readonly List<FileWatcherBurstEventArgs> _bursts = [];
    private TaskCompletionSource _changed = NewSignal();

    public FileWatcherHarness(FileWatcherOptions? options = null)
    {
        Root = Path.Combine(Path.GetTempPath(), $"uphoric-fw-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        Watcher = new FileWatcher(
            Root,
            options ?? new FileWatcherOptions { Debounce = TimeSpan.FromMilliseconds(40) }
        );
        Watcher.Burst += OnBurst;
        Watcher.Start();
    }

    public string Root { get; }

    public FileWatcher Watcher { get; }

    public IReadOnlyList<FileWatcherBurstEventArgs> Bursts
    {
        get
        {
            lock (_lock)
            {
                return _bursts.ToArray();
            }
        }
    }

    public IReadOnlyList<FileWatcherEventArgs> Events => Bursts.SelectMany(b => b.Events).ToArray();

    public string PathOf(string relativePath) => Path.Combine(Root, relativePath);

    public async Task WriteAsync(string relativePath, string content = "x")
    {
        string path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Root);
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
    }

    public async Task WriteAllAsync(IEnumerable<string> relativePaths)
    {
        foreach (string relativePath in relativePaths)
        {
            await WriteAsync(relativePath);
        }
    }

    public Task WaitForPathsAsync(params string[] paths) =>
        WaitUntilAsync(
            bursts =>
                paths.All(path =>
                    bursts.SelectMany(b => b.Events).Any(e => e.RelativePath == path)
                ),
            $"events for [{string.Join(", ", paths)}]"
        );

    public Task WaitForEventAsync(FileWatcherEventKind kind, string relativePath) =>
        WaitUntilAsync(
            bursts =>
                bursts
                    .SelectMany(b => b.Events)
                    .Any(e => e.Kind == kind && e.RelativePath == relativePath),
            $"{kind} event for {relativePath}"
        );

    public async Task WaitUntilAsync(
        Func<IReadOnlyList<FileWatcherBurstEventArgs>, bool> condition,
        string description
    )
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        timeout.CancelAfter(Timeout);

        while (true)
        {
            Task changed = NextChange(condition, out bool satisfied);
            if (satisfied)
            {
                return;
            }

            try
            {
                await changed.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException exception)
                when (!TestContext.Current.CancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for {description}.", exception);
            }
        }
    }

    public void Dispose()
    {
        Watcher.Dispose();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Task NextChange(
        Func<IReadOnlyList<FileWatcherBurstEventArgs>, bool> condition,
        out bool satisfied
    )
    {
        lock (_lock)
        {
            satisfied = condition(_bursts);
            return _changed.Task;
        }
    }

    private void OnBurst(object? sender, FileWatcherBurstEventArgs burst)
    {
        TaskCompletionSource previous;
        lock (_lock)
        {
            _bursts.Add(burst);
            previous = _changed;
            _changed = NewSignal();
        }

        previous.SetResult();
    }
}
