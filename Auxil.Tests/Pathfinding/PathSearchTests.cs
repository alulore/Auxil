using Auxil.Pathfinding;

namespace Auxil.Tests.Pathfinding;

/// <summary>
/// Black-box tests for stepping a search across frames: BeginSearch, Step and GetResult.
/// No engine types, no internals.
/// </summary>
public class PathSearchTests
{
    [Fact]
    public void SteppedSearch_MatchesTheOneShotSearch()
    {
        using Pathfinder finder = new(new PathMap(20, 20));
        finder.Options.Smooth = false;
        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(19, 19), out PathResult oneShot)
            .ShouldBeTrue();

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(19, 19));
        search.IsComplete.ShouldBeFalse();

        search.RunToCompletion(1);

        search.IsComplete.ShouldBeTrue();
        search.Found.ShouldBeTrue();
        PathResult stepped = search.GetResult();
        stepped.Cost.ShouldBe(oneShot.Cost);
        stepped.Points.ShouldBe(oneShot.Points);
        stepped.NodesExpanded.ShouldBe(oneShot.NodesExpanded);
    }

    [Fact]
    public void SmallSteps_ReportRunningUntilTheGoalIsFound()
    {
        using Pathfinder finder = new(new PathMap(30, 30));
        finder.Options.Smooth = false;

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(29, 29));

        int steps = search.RunToCompletion(10);

        steps.ShouldBeGreaterThan(0);
        search.Found.ShouldBeTrue();
        search.NodesExpanded.ShouldBeGreaterThan(0);
        search.GetResult().Cost.ShouldBe(29 * 14);
    }

    [Fact]
    public void StartEqualsGoal_CompletesImmediately()
    {
        using Pathfinder finder = new(new PathMap(5, 5));

        PathSearch search = finder.BeginSearch(new PathPoint(2, 2), new PathPoint(2, 2));

        search.IsComplete.ShouldBeTrue();
        search.Step(1).ShouldBe(PathStatus.Found);
        PathResult result = search.GetResult();
        result.Cost.ShouldBe(0);
        result.Points.Count.ShouldBe(1);
        result.Points[0].ShouldBe(new PathPoint(2, 2));
    }

    [Fact]
    public void BlockedStart_CompletesWithStartBlocked()
    {
        PathMap map = new(5, 5);
        map.SetBlocked(0, 0);
        using Pathfinder finder = new(map);

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(4, 4));

        search.IsComplete.ShouldBeTrue();
        search.Step(1).ShouldBe(PathStatus.StartBlocked);
        search.GetResult().Status.ShouldBe(PathStatus.StartBlocked);
    }

    [Fact]
    public void BlockedGoal_CompletesWithGoalBlocked()
    {
        PathMap map = new(5, 5);
        map.SetBlocked(4, 4);
        using Pathfinder finder = new(map);

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(4, 4));

        search.IsComplete.ShouldBeTrue();
        search.Step(1).ShouldBe(PathStatus.GoalBlocked);
    }

    [Fact]
    public void PointOutsideTheMap_CompletesWithInvalid()
    {
        using Pathfinder finder = new(new PathMap(5, 5));

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(10, 10));

        search.IsComplete.ShouldBeTrue();
        search.Step(1).ShouldBe(PathStatus.Invalid);
    }

    [Fact]
    public void GetResult_BeforeCompletion_Throws()
    {
        using Pathfinder finder = new(new PathMap(50, 50));

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(49, 49));
        search.Step(1).ShouldBeNull();

        Should.Throw<InvalidOperationException>(search.GetResult);
    }

    [Fact]
    public void SteppedSearch_WorksThroughAMaze()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "....."]);
        using Pathfinder finder = new(map);
        finder.Options.Smooth = false;
        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(4, 0), out PathResult oneShot)
            .ShouldBeTrue();

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(4, 0));
        search.RunToCompletion(2);

        PathResult stepped = search.GetResult();
        stepped.Cost.ShouldBe(oneShot.Cost);
        stepped.Points.ShouldBe(oneShot.Points);
    }

    [Fact]
    public void NoRoute_CompletesWithNoPath()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "..#.."]);
        using Pathfinder finder = new(map);

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(4, 0));
        search.RunToCompletion(2);

        search.Found.ShouldBeFalse();
        search.GetResult().Status.ShouldBe(PathStatus.NoPath);
    }

    [Fact]
    public void JpsPlus_CompletesOnTheFirstStep()
    {
        using Pathfinder finder = new(new PathMap(20, 20), PathfinderAlgorithm.JpsPlus);
        finder.Options.Smooth = false;

        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(19, 19));
        search.IsComplete.ShouldBeFalse();

        search.Step(1).ShouldBe(PathStatus.Found);
        search.GetResult().Cost.ShouldBe(19 * 14);
    }

    [Fact]
    public void DisposingTheFinder_InvalidatesTheSearch()
    {
        Pathfinder finder = new(new PathMap(50, 50));
        PathSearch search = finder.BeginSearch(new PathPoint(0, 0), new PathPoint(49, 49));
        search.Step(1).ShouldBeNull();

        finder.Dispose();

        Should.Throw<ObjectDisposedException>(() => search.Step(1));
        Should.Throw<ObjectDisposedException>(search.GetResult);
    }
}
