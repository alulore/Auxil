using Auxil.Pathfinding;

namespace Auxil.Tests.Pathfinding;

/// <summary>
/// Black-box tests for the shared-goal flow field: distances, per-step movement and whole routes.
/// No engine types, no internals.
/// </summary>
public class CrowdFieldTests
{
    [Fact]
    public void FieldRoute_MatchesThePathfinder()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "....."]);
        using Pathfinder finder = new(map);
        finder.Options.Smooth = false;
        finder
            .TryFindPath(new PathPoint(0, 0), new PathPoint(4, 0), out PathResult expected)
            .ShouldBeTrue();

        using CrowdField field = new(map);
        field.SetGoal(new PathPoint(4, 0));
        field.Map.ShouldBeSameAs(map);
        field.Stack.ShouldBeNull();
        field.TryFindPath(new PathPoint(0, 0), out PathResult route).ShouldBeTrue();

        route.Cost.ShouldBe(expected.Cost);
        route.Points[0].ShouldBe(new PathPoint(0, 0));
        route.Points[^1].ShouldBe(new PathPoint(4, 0));
    }

    [Fact]
    public void NextStep_WalksAllTheWayToTheGoal()
    {
        using CrowdField field = new(new PathMap(10, 10));
        field.SetGoal(new PathPoint(9, 9));
        field.Goal.ShouldBe(new PathPoint(9, 9));

        PathPoint position = new(0, 0);
        field.DistanceAt(position).ShouldBe(9 * 14);

        (PathPoint end, int steps) = WalkToGoal(field, position);
        position = end;

        position.ShouldBe(new PathPoint(9, 9));
        steps.ShouldBe(9);
    }

    [Fact]
    public void GoalTile_HasZeroDistanceAndNoNextStep()
    {
        using CrowdField field = new(new PathMap(5, 5));
        field.SetGoal(new PathPoint(4, 4));

        field.DistanceAt(new PathPoint(4, 4)).ShouldBe(0);
        field.TryGetNextStep(new PathPoint(4, 4), out _).ShouldBeFalse();
    }

    [Fact]
    public void WalledOffArea_ReportsMinusOneAndNoRoute()
    {
        PathMap map = PathMap.FromRows(["..#..", "..#..", "..#..", "..#..", "..#.."]);
        using CrowdField field = new(map);
        field.SetGoal(new PathPoint(4, 0));

        field.DistanceAt(new PathPoint(0, 0)).ShouldBe(-1);
        field.TryGetNextStep(new PathPoint(0, 0), out _).ShouldBeFalse();
        field.TryFindPath(new PathPoint(0, 0), out PathResult result).ShouldBeFalse();
        result.Status.ShouldBe(PathStatus.NoPath);
    }

    [Fact]
    public void BlockedStart_ReturnsStartBlocked()
    {
        PathMap map = new(5, 5);
        map.SetBlocked(0, 0);
        using CrowdField field = new(map);
        field.SetGoal(new PathPoint(4, 4));

        field.TryFindPath(new PathPoint(0, 0), out PathResult result).ShouldBeFalse();
        result.Status.ShouldBe(PathStatus.StartBlocked);
    }

    [Fact]
    public void StartEqualsGoal_ReturnsASinglePointRoute()
    {
        using CrowdField field = new(new PathMap(5, 5));
        field.SetGoal(new PathPoint(2, 2));

        field.TryFindPath(new PathPoint(2, 2), out PathResult result).ShouldBeTrue();

        result.Cost.ShouldBe(0);
        result.Points.Count.ShouldBe(1);
    }

    [Fact]
    public void SetGoal_RejectsBlockedAndOutsideTiles()
    {
        PathMap map = new(5, 5);
        map.SetBlocked(1, 1);
        using CrowdField field = new(map);

        Should.Throw<ArgumentException>(() => field.SetGoal(new PathPoint(1, 1)));
        Should.Throw<ArgumentOutOfRangeException>(() => field.SetGoal(new PathPoint(9, 9)));
        field.HasGoal.ShouldBeFalse();
    }

    [Fact]
    public void UsingWithoutAGoal_Throws()
    {
        using CrowdField field = new(new PathMap(5, 5));

        Should.Throw<InvalidOperationException>(() => field.DistanceAt(new PathPoint(0, 0)));
        Should.Throw<InvalidOperationException>(() =>
            field.TryFindPath(new PathPoint(0, 0), out _)
        );
    }

    [Fact]
    public void DisposedField_Throws()
    {
        CrowdField field = new(new PathMap(5, 5));
        field.SetGoal(new PathPoint(4, 4));
        field.Dispose();

        Should.Throw<ObjectDisposedException>(() => field.DistanceAt(new PathPoint(0, 0)));
    }

    [Fact]
    public void Field_RebuildsFromTheLiveMap()
    {
        PathMap map = new(5, 3);
        map.SetBlocked(2, 0);
        map.SetBlocked(2, 1);

        using CrowdField field = new(map);
        field.SetGoal(new PathPoint(4, 0));
        field.TryFindPath(new PathPoint(0, 0), out PathResult before).ShouldBeTrue();
        before.Cost.ShouldBe(56);

        map.SetBlocked(2, 2);
        field.SetGoal(new PathPoint(4, 0));

        field.TryFindPath(new PathPoint(0, 0), out PathResult after).ShouldBeFalse();
        after.Status.ShouldBe(PathStatus.NoPath);
    }

    [Fact]
    public void SetGoal_ReplacesThePreviousGoal()
    {
        using CrowdField field = new(new PathMap(5, 5));
        field.SetGoal(new PathPoint(4, 0));
        field.SetGoal(new PathPoint(0, 4));

        field.Goal.ShouldBe(new PathPoint(0, 4));
        field.DistanceAt(new PathPoint(0, 0)).ShouldBe(40);
        field.TryGetNextStep(new PathPoint(0, 0), out PathPoint next).ShouldBeTrue();
        next.ShouldBe(new PathPoint(0, 1));
    }

    [Fact]
    public void FieldDistances_IgnoreExtraTerrainCosts()
    {
        PathMap map = new(3, 1);
        map.SetExtraCost(1, 0, 100);
        using CrowdField field = new(map);
        field.SetGoal(new PathPoint(2, 0));

        field.DistanceAt(new PathPoint(0, 0)).ShouldBe(20);
    }

    private static (PathPoint End, int Steps) WalkToGoal(CrowdField field, PathPoint start)
    {
        PathPoint position = start;
        int steps = 0;
        while (field.TryGetNextStep(position, out PathPoint next))
        {
            field.DistanceAt(next).ShouldBeLessThan(field.DistanceAt(position));
            position = next;
            steps++;
            steps.ShouldBeLessThan(100);
        }

        return (position, steps);
    }
}
