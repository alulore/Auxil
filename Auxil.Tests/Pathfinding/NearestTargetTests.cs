using Auxil.Pathfinding;

namespace Auxil.Tests.Pathfinding;

/// <summary>
/// Black-box tests for walking to the nearest of several targets.
/// No engine types, no internals.
/// </summary>
public class NearestTargetTests
{
    [Fact]
    public void PicksTheCheapestTarget()
    {
        using Pathfinder finder = new(new PathMap(10, 10));
        finder.Options.Smooth = false;

        PathPoint[] goals = [new(5, 0), new(0, 5), new(2, 2)];

        finder
            .TryFindPathToNearest(new PathPoint(0, 0), goals, out PathResult result)
            .ShouldBeTrue();

        result.Goal.ShouldBe(new PathPoint(2, 2));
        result.Points[^1].ShouldBe(new PathPoint(2, 2));
        result.Cost.ShouldBe(28);
    }

    [Fact]
    public void BlockedTargetsAreSkipped()
    {
        PathMap map = new(10, 10);
        map.SetBlocked(2, 2);
        using Pathfinder finder = new(map);
        finder.Options.Smooth = false;

        PathPoint[] goals = [new(2, 2), new(0, 5)];

        finder
            .TryFindPathToNearest(new PathPoint(0, 0), goals, out PathResult result)
            .ShouldBeTrue();

        result.Goal.ShouldBe(new PathPoint(0, 5));
        result.Cost.ShouldBe(50);
    }

    [Fact]
    public void TargetsOutsideTheMapAreSkipped()
    {
        using Pathfinder finder = new(new PathMap(10, 10));
        finder.Options.Smooth = false;

        PathPoint[] goals = [new(50, 50), new(3, 0)];

        finder
            .TryFindPathToNearest(new PathPoint(0, 0), goals, out PathResult result)
            .ShouldBeTrue();

        result.Goal.ShouldBe(new PathPoint(3, 0));
        result.Cost.ShouldBe(30);
    }

    [Fact]
    public void TerrainCostCanChangeWhichTargetIsNearest()
    {
        PathMap map = new(5, 3);
        map.SetExtraCost(1, 1, 100);
        map.SetExtraCost(2, 1, 100);
        using Pathfinder finder = new(map);
        finder.Options.Smooth = false;

        PathPoint[] goals = [new(2, 1), new(4, 1)];

        finder
            .TryFindPathToNearest(new PathPoint(0, 0), goals, out PathResult result)
            .ShouldBeTrue();

        result.Goal.ShouldBe(new PathPoint(4, 1));
        result.Cost.ShouldBe(44);
    }

    [Fact]
    public void StartEqualsATarget_ReturnsAZeroCostRoute()
    {
        using Pathfinder finder = new(new PathMap(5, 5));

        finder
            .TryFindPathToNearest(
                new PathPoint(1, 1),
                [new(1, 1), new(4, 4)],
                out PathResult result
            )
            .ShouldBeTrue();

        result.Goal.ShouldBe(new PathPoint(1, 1));
        result.Cost.ShouldBe(0);
        result.Points.Count.ShouldBe(1);
        result.Points[0].ShouldBe(new PathPoint(1, 1));
    }

    [Fact]
    public void BlockedStart_ReturnsFalse()
    {
        PathMap map = new(5, 5);
        map.SetBlocked(0, 0);
        using Pathfinder finder = new(map);

        finder
            .TryFindPathToNearest(new PathPoint(0, 0), [new(4, 4)], out PathResult result)
            .ShouldBeFalse();

        result.Status.ShouldBe(PathStatus.StartBlocked);
    }

    [Fact]
    public void NoUsableTargets_ReturnsFalse()
    {
        PathMap map = new(5, 5);
        map.SetBlocked(4, 4);
        using Pathfinder finder = new(map);

        finder
            .TryFindPathToNearest(
                new PathPoint(0, 0),
                [new(4, 4), new(9, 9)],
                out PathResult result
            )
            .ShouldBeFalse();

        result.Status.ShouldBe(PathStatus.Invalid);
    }

    [Fact]
    public void NoReachableTarget_ReturnsNoPath()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "..#.."]);
        using Pathfinder finder = new(map);

        finder
            .TryFindPathToNearest(new PathPoint(0, 0), [new(4, 4)], out PathResult result)
            .ShouldBeFalse();

        result.Status.ShouldBe(PathStatus.NoPath);
    }
}
