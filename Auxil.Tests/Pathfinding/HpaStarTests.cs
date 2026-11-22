using Auxil.Pathfinding;

namespace Auxil.Tests.Pathfinding;

/// <summary>
/// Black-box tests for the hierarchical A* engine exposed through the public API.
/// No engine types, no internals.
/// </summary>
public class HpaStarTests
{
    [Fact]
    public void FindsARouteAcrossAnOpenMap()
    {
        using Pathfinder finder = new(new PathMap(40, 40), PathfinderAlgorithm.HpaStar);
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(39, 39), out PathResult result)
            .ShouldBeTrue();

        result.Status.ShouldBe(PathStatus.Found);
        result.Points[0].ShouldBe(new PathPoint(0, 0));
        result.Points[^1].ShouldBe(new PathPoint(39, 39));
        result.Cost.ShouldBeGreaterThanOrEqualTo(39 * 14);

        ShouldBeContiguous(result.Points);
    }

    [Fact]
    public void RoutesAroundAWall()
    {
        PathMap map = PathMap.FromRows([
            ".....#.....",
            ".....#.....",
            ".....#.....",
            ".....#.....",
            ".....#.....",
            ".....#.....",
            ".....#.....",
            ".....#.....",
            ".....#.....",
            "...........",
        ]);
        using Pathfinder finder = new(map, PathfinderAlgorithm.HpaStar);
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(10, 0), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBeGreaterThan(10 * 10);
        result.Points.ShouldAllBe(p => !map.IsBlocked(p));
    }

    [Fact]
    public void FullyWalledOff_ReturnsNoPath()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "..#.."]);
        using Pathfinder finder = new(map, PathfinderAlgorithm.HpaStar);

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(4, 4), out PathResult result)
            .ShouldBeFalse();

        result.Status.ShouldBe(PathStatus.NoPath);
    }

    [Fact]
    public void BlockedStart_ReturnsStartBlocked()
    {
        PathMap map = new(10, 10);
        map.SetBlocked(0, 0);
        using Pathfinder finder = new(map, PathfinderAlgorithm.HpaStar);

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(9, 9), out PathResult result)
            .ShouldBeFalse();

        result.Status.ShouldBe(PathStatus.StartBlocked);
    }

    [Fact]
    public void TerrainCosts_AreRejected()
    {
        PathMap map = new(10, 10);
        map.SetExtraCost(5, 5, 10);

        Should.Throw<InvalidOperationException>(() =>
            new Pathfinder(map, PathfinderAlgorithm.HpaStar)
        );
    }

    [Fact]
    public void StackedMaps_CrossTheStairs()
    {
        PathMapStack stack = new();
        stack.AddLayer(new PathMap(30, 30));
        stack.AddLayer(new PathMap(30, 30));
        stack.ConnectStairs(15, 15, 0);

        using Pathfinder finder = new(stack, PathfinderAlgorithm.HpaStar);
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0, 0), new PathPoint(29, 29, 1), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBeGreaterThanOrEqualTo(15 * 14 + 10 + 14 * 14);
        result.Points.ShouldContain(new PathPoint(15, 15, 0));
        result.Points.ShouldContain(new PathPoint(15, 15, 1));
        result.Points[0].ShouldBe(new PathPoint(0, 0, 0));
        result.Points[^1].ShouldBe(new PathPoint(29, 29, 1));
    }

    private static void ShouldBeContiguous(IReadOnlyList<PathPoint> points)
    {
        for (int i = 0; i + 1 < points.Count; i++)
        {
            int dx = Math.Abs(points[i + 1].X - points[i].X);
            int dy = Math.Abs(points[i + 1].Y - points[i].Y);
            Math.Max(dx, dy).ShouldBe(1);
        }
    }
}
