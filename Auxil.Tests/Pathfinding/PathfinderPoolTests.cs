using Auxil.Pathfinding;

namespace Auxil.Tests.Pathfinding;

/// <summary>
/// Black-box tests for running many queries in parallel on one map.
/// No engine types, no internals.
/// </summary>
public class PathfinderPoolTests
{
    private const int OctileLeg = 48;
    private const int ThroughTheOnlyCrossing = OctileLeg + OctileLeg;

    [Fact]
    public async Task ParallelQueries_MatchSequentialResults()
    {
        PathMap map = PathMap.FromRows([
            "..........",
            "..###.....",
            "..#.......",
            "..#..###..",
            ".....#....",
            "###..#....",
            ".....#....",
            "..........",
        ]);

        (PathPoint Start, PathPoint Goal)[] queries =
        [
            (new(0, 0), new(9, 7)),
            (new(9, 0), new(0, 7)),
            (new(0, 7), new(9, 7)),
            (new(4, 0), new(4, 7)),
            (new(9, 7), new(0, 0)),
        ];

        using Pathfinder finder = new(map);
        finder.Options.Smooth = false;
        (PathStatus Status, int Cost)[] expected = queries
            .Select(q => finder.FindPath(q.Start, q.Goal))
            .Select(r => (r.Status, r.Cost))
            .ToArray();

        using PathfinderPool pool = new(map, maxConcurrency: 4);
        Task<PathResult>[] tasks = queries
            .Select(q => pool.FindPathAsync(q.Start, q.Goal, TestContext.Current.CancellationToken))
            .ToArray();
        PathResult[] results = await Task.WhenAll(tasks);

        results.Select(r => (r.Status, r.Cost)).ShouldBe(expected);
    }

    [Fact]
    public void Pool_ExposesItsMapAndAlgorithm()
    {
        PathMap map = new(5, 5);
        using PathfinderPool pool = new(map);

        pool.Map.ShouldBeSameAs(map);
        pool.Stack.ShouldBeNull();
        pool.Algorithm.ShouldBe(PathfinderAlgorithm.Default);
    }

    [Fact]
    public async Task ManyQueries_AllSucceed()
    {
        using PathfinderPool pool = new(new PathMap(100, 100), maxConcurrency: 8);

        Task<PathResult>[] tasks = Enumerable
            .Range(0, 64)
            .Select(i =>
                pool.FindPathAsync(
                    new PathPoint(0, i),
                    new PathPoint(99, 99 - i),
                    TestContext.Current.CancellationToken
                )
            )
            .ToArray();

        PathResult[] results = await Task.WhenAll(tasks);
        results.ShouldAllBe(r => r.Found);
    }

    [Fact]
    public async Task CancelledBeforeStart_Throws()
    {
        using PathfinderPool pool = new(new PathMap(5, 5), maxConcurrency: 1);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            pool.FindPathAsync(new PathPoint(0, 0), new PathPoint(4, 4), cancellation.Token)
        );
    }

    [Fact]
    public async Task DisposedPool_Throws()
    {
        PathfinderPool pool = new(new PathMap(5, 5));
        pool.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(() =>
            pool.FindPathAsync(
                new PathPoint(0, 0),
                new PathPoint(4, 4),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Pool_WithUnboundedConcurrency_Works()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "....."]);
        using PathfinderPool pool = new(map, maxConcurrency: int.MaxValue);

        PathResult result = await pool.FindPathAsync(
            new PathPoint(0, 0),
            new PathPoint(4, 0),
            TestContext.Current.CancellationToken
        );

        result.Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Pool_WithAUniformAlgorithm_Works()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "....."]);
        using PathfinderPool pool = new(map, PathfinderAlgorithm.JpsPlus, maxConcurrency: 2);

        PathResult result = await pool.FindPathAsync(
            new PathPoint(0, 0),
            new PathPoint(4, 0),
            TestContext.Current.CancellationToken
        );

        result.Found.ShouldBeTrue();
        result.Cost.ShouldBe(ThroughTheOnlyCrossing);
    }
}
