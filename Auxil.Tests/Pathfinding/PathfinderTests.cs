using Auxil.Pathfinding;

namespace Auxil.Tests.Pathfinding;

/// <summary>
/// Black-box behaviour tests: give the pathfinder a map and two points, expect a status, a cost
/// and a route. No engine types, no internals.
/// </summary>
public class PathfinderTests
{
    private const int OctileLeg = 48;
    private const int ThroughTheOnlyCrossing = OctileLeg + OctileLeg;
    private const int DirectDiagonalThroughMud = 14 + 100;
    private const int AroundTheMud = 10 + 14 + 10;

    [Fact]
    public void Pathfinder_ExposesItsMapAndAlgorithm()
    {
        PathMap map = new(5, 5);
        using Pathfinder finder = new(map, PathfinderAlgorithm.JpsPlus);

        finder.Map.ShouldBeSameAs(map);
        finder.Stack.ShouldBeNull();
        finder.Algorithm.ShouldBe(PathfinderAlgorithm.JpsPlus);
    }

    [Fact]
    public void EmptyMap_FindsTheStraightDiagonalRoute()
    {
        using Pathfinder finder = new(new PathMap(5, 5));
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(4, 4), out PathResult result)
            .ShouldBeTrue();

        result.Status.ShouldBe(PathStatus.Found);
        result.Cost.ShouldBe(56);
        result.Steps.ShouldBe(5);
        result.Points[0].ShouldBe(new PathPoint(0, 0));
        result.Points[^1].ShouldBe(new PathPoint(4, 4));
        result.NodesExpanded.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void WallWithADistantGap_ForcesTheDetour()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "....."]);
        using Pathfinder finder = new(map);
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(4, 0), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBe(ThroughTheOnlyCrossing);
        result.Points.ShouldContain(new PathPoint(2, 4));
    }

    [Fact]
    public void TerrainCost_MakesTheSearchWalkAroundIt()
    {
        PathMap map = new(3, 3);
        map.SetExtraCost(1, 1, 100);
        using Pathfinder finder = new(map);
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(2, 2), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBe(AroundTheMud);
        result.Cost.ShouldBeLessThan(DirectDiagonalThroughMud);
        result.Points.ShouldNotContain(new PathPoint(1, 1));
        result.Steps.ShouldBe(4);
    }

    [Fact]
    public void CorridorOneTileWide_WalksStraight()
    {
        PathMap map = new(1, 5);
        using Pathfinder finder = new(map);
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(0, 4), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBe(40);
        result.Steps.ShouldBe(5);
    }

    [Fact]
    public void LargeEmptyMap_CornerToCorner()
    {
        using Pathfinder finder = new(new PathMap(64, 64));
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(63, 63), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBe(882);
    }

    [Fact]
    public void SingleTileMap_StartEqualsGoal()
    {
        using Pathfinder finder = new(new PathMap(1, 1));

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(0, 0), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBe(0);
        result.Steps.ShouldBe(1);
    }

    [Fact]
    public void StartEqualsGoal_OnANormalMap()
    {
        using Pathfinder finder = new(new PathMap(8, 8));

        finder
            .TryFindPath(new PathPoint(3, 6), new PathPoint(3, 6), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBe(0);
        result.Steps.ShouldBe(1);
    }

    [Fact]
    public void PointsOutsideTheMap_ReportInvalid()
    {
        using Pathfinder finder = new(new PathMap(3, 3));

        finder
            .TryFindPath(new PathPoint(-1, 0), new PathPoint(1, 1), out PathResult startOutside)
            .ShouldBeFalse();
        startOutside.Status.ShouldBe(PathStatus.Invalid);

        finder
            .TryFindPath(new PathPoint(1, 1), new PathPoint(3, 0), out PathResult goalOutside)
            .ShouldBeFalse();
        goalOutside.Status.ShouldBe(PathStatus.Invalid);

        finder
            .TryFindPath(
                new PathPoint(0, 0, Layer: 1),
                new PathPoint(1, 1),
                out PathResult wrongLayer
            )
            .ShouldBeFalse();
        wrongLayer.Status.ShouldBe(PathStatus.Invalid);
    }

    [Fact]
    public void BlockedStartAndGoal_ReportWhy()
    {
        PathMap map = new(3, 3);
        map.SetBlocked(0, 0);
        using Pathfinder finder = new(map);

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(1, 1), out PathResult blockedStart)
            .ShouldBeFalse();
        blockedStart.Status.ShouldBe(PathStatus.StartBlocked);

        map.SetBlocked(0, 0, blocked: false);
        map.SetBlocked(1, 1);
        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(1, 1), out PathResult blockedGoal)
            .ShouldBeFalse();
        blockedGoal.Status.ShouldBe(PathStatus.GoalBlocked);
    }

    [Fact]
    public void UnreachableGoal_ReportsNoPath()
    {
        PathMap map = PathMap.FromRows([".#.", ".#.", ".#."]);
        using Pathfinder finder = new(map);

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(2, 0), out PathResult result)
            .ShouldBeFalse();

        result.Status.ShouldBe(PathStatus.NoPath);
        result.Points.ShouldBeEmpty();
    }

    [Fact]
    public void DisposedPathfinder_ThrowsOnUse()
    {
        Pathfinder finder = new(new PathMap(2, 2));
        finder.Dispose();

        Should.Throw<ObjectDisposedException>(() =>
            finder.TryFindPath(new PathPoint(0, 0), new PathPoint(1, 1), out _)
        );
    }

    [Theory]
    [InlineData(PathfinderAlgorithm.Default)]
    [InlineData(PathfinderAlgorithm.BitboardAStar)]
    [InlineData(PathfinderAlgorithm.JumpPointSearch)]
    [InlineData(PathfinderAlgorithm.JpsPlus)]
    public void EveryAlgorithm_AgreesOnCost(PathfinderAlgorithm algorithm)
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "....."]);
        using Pathfinder finder = new(map, algorithm);
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(4, 0), out PathResult result)
            .ShouldBeTrue();

        result.Cost.ShouldBe(ThroughTheOnlyCrossing);
    }

    [Fact]
    public void TerrainCosts_WithAUniformOnlyAlgorithm_Throws()
    {
        PathMap map = new(3, 3);
        map.SetExtraCost(1, 1, 5);

        Should.Throw<InvalidOperationException>(() =>
            new Pathfinder(map, PathfinderAlgorithm.JpsPlus)
        );
        Should.Throw<InvalidOperationException>(() =>
            new Pathfinder(map, PathfinderAlgorithm.JumpPointSearch)
        );
    }

    [Fact]
    public void Smoothing_KeepsTheCostAndShortensTheRoute()
    {
        using Pathfinder finder = new(new PathMap(6, 6));

        finder.Options.Smooth = false;
        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(5, 5), out PathResult raw)
            .ShouldBeTrue();

        finder.Options.Smooth = true;
        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(5, 5), out PathResult smoothed)
            .ShouldBeTrue();

        raw.Steps.ShouldBe(6);
        smoothed.Steps.ShouldBe(2);
        smoothed.Cost.ShouldBe(raw.Cost);
        smoothed.Points[0].ShouldBe(raw.Points[0]);
        smoothed.Points[^1].ShouldBe(raw.Points[^1]);
    }

    [Fact]
    public void ChangingTheMap_IsVisibleToTheNextQuery()
    {
        PathMap map = new(3, 3);
        using Pathfinder finder = new(map);

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(2, 0), out PathResult before)
            .ShouldBeTrue();
        before.Cost.ShouldBe(20);

        map.SetBlocked(1, 0, width: 1, height: 3);
        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(2, 0), out PathResult after)
            .ShouldBeFalse();
        after.Status.ShouldBe(PathStatus.NoPath);
    }

    [Fact]
    public void JumpPointSearch_SeesMapEditsAfterConstruction()
    {
        PathMap map = new(3, 3);
        using Pathfinder finder = new(map, PathfinderAlgorithm.JumpPointSearch);

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(2, 0), out PathResult before)
            .ShouldBeTrue();
        before.Cost.ShouldBe(20);

        map.SetBlocked(1, 0, width: 1, height: 3);
        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(2, 0), out PathResult after)
            .ShouldBeFalse();
        after.Status.ShouldBe(PathStatus.NoPath);
    }

    [Fact]
    public void PathResult_ToString_Summarises()
    {
        using Pathfinder finder = new(new PathMap(3, 3));
        finder.Options.Smooth = false;

        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(2, 2), out PathResult result)
            .ShouldBeTrue();

        result.ToString().ShouldContain("Found");
        result.ToString().ShouldContain("cost 28");
    }
}
