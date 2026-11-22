using Auxil.Pathfinding.Engine;

namespace Auxil.Tests.Pathfinding;

/// <summary>Wiring check for the test project; real black-box tests arrive with the public API.</summary>
public class GridSmokeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void TryStep_OnEmptyGrid_AllowsEveryInPlaneDirection(int direction)
    {
        byte[] cells = new byte[9];
        Array.Fill(cells, Grid.Open);
        Grid grid = new(3, 3, 1, cells);
        int center = grid.Index(1, 1, 0);

        grid.TryStep(center, 1, 1, 0, direction, out _).ShouldBeTrue();
    }

    [Fact]
    public void TryStep_AgainstAWall_IsRejected()
    {
        byte[] cells = new byte[9];
        Array.Fill(cells, Grid.Open);
        cells[1 * 3 + 2] = Grid.Blocked;
        Grid grid = new(3, 3, 1, cells);
        int center = grid.Index(1, 1, 0);

        grid.TryStep(center, 1, 1, 0, 0, out _).ShouldBeFalse();
    }
}
