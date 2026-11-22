using System.Runtime.CompilerServices;

namespace Auxil.Pathfinding.Engine;

/// <summary>
/// JPS+ (Rabin; Harabor and Grastien) for uniform-cost, single-layer grids.
///
/// The whole jump-point graph is precomputed into one distance table per direction, stored as a
/// <see cref="short"/> per cell:
///   * positive: distance to the next jump point in that direction,
///   * negative: distance to the wall in that direction,
///   * zero: no information (blocked cell or immediate wall).
///
/// Preprocessing sweeps are linear in the map size. At runtime the search never scans rows or
/// columns; it reads the table, relaxes the landing jump point, and synthesizes "target jump
/// points" when a jump crosses the goal's row or column. An optional intermediate-pruning mode
/// (<c>intermediatePruning</c>) expands diagonal jump points in place instead of queueing them.
/// </summary>
internal sealed class JpsPlusSearch : GridSearchBase
{
    private const byte NoParentDirection = 255;

    private const int East = 0;
    private const int North = 2;
    private const int West = 4;
    private const int South = 6;
    private const byte EastFlag = 1;
    private const byte NorthFlag = 2;
    private const byte WestFlag = 4;
    private const byte SouthFlag = 8;

    private readonly bool _intermediatePruning;

    /// <summary>Per-direction jump and wall distances: <c>[direction * NodeCount + cell]</c>.</summary>
    private short[] _distanceTable = [];

    private byte[] _parentDirection = [];
    private int[] _fScore = [];
    private int[] _openGeneration = [];
    private DialHeapIndexed _openList;

    public JpsPlusSearch(bool intermediatePruning = false)
    {
        _intermediatePruning = intermediatePruning;
    }

    public override void Init(Grid grid)
    {
        if (grid.Layers != 1)
        {
            throw new InvalidOperationException("JPS+ is defined for single-layer grids.");
        }
        InitializeSearchState(grid);
        _distanceTable = new short[Grid.DirectionCount * NodeCount];
        _parentDirection = new byte[NodeCount];
        _fScore = new int[NodeCount];
        _openGeneration = new int[NodeCount];
        _openList.Init(grid);
        Preprocess();
    }

    public override void Release()
    {
        ReleaseSearchState();
        _distanceTable = [];
        _parentDirection = [];
        _fScore = [];
        _openGeneration = [];
        _openList = default;
    }

    protected override void ClearGenerationArrays()
    {
        base.ClearGenerationArrays();
        Array.Clear(_parentDirection);
        Array.Clear(_openGeneration);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int TableIndex(int direction, int cell) => direction * NodeCount + cell;

    private void Preprocess()
    {
        byte[] primaryJumpPoints = BuildPrimaryJumpPoints();
        (short[] East, short[] North, short[] West, short[] South) straightTables =
            BuildStraightTables(primaryJumpPoints);
        BuildDiagonalTables(primaryJumpPoints, straightTables);
    }

    /// <summary>
    /// Marks, for each straight direction, the cells that are primary jump points: cells whose
    /// wall configuration forces a turn. A single byte per cell carries the four flags.
    /// </summary>
    private byte[] BuildPrimaryJumpPoints()
    {
        byte[] primary = new byte[NodeCount];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int cell = y * Width + x;
                if (Cells[cell] != Grid.Blocked)
                {
                    primary[cell] = PrimaryFlagsAt(x, y);
                }
            }
        }
        return primary;
    }

    private byte PrimaryFlagsAt(int x, int y)
    {
        byte flags = 0;
        if (IsForcedTurn(x, y, 1, 1, 0, 1) || IsForcedTurn(x, y, 1, -1, 0, -1))
        {
            flags |= EastFlag;
        }
        if (IsForcedTurn(x, y, -1, 1, 0, 1) || IsForcedTurn(x, y, -1, -1, 0, -1))
        {
            flags |= WestFlag;
        }
        if (IsForcedTurn(x, y, 1, 1, 1, 0) || IsForcedTurn(x, y, -1, 1, -1, 0))
        {
            flags |= NorthFlag;
        }
        if (IsForcedTurn(x, y, 1, -1, 1, 0) || IsForcedTurn(x, y, -1, -1, -1, 0))
        {
            flags |= SouthFlag;
        }
        return flags;
    }

    /// <summary>
    /// Whether a walkable cell diagonally ahead (open offset) with a blocked cell beside it
    /// (blocked offset) forces a turn when travelling toward the open cell's side.
    /// </summary>
    private bool IsForcedTurn(int x, int y, int openDx, int openDy, int blockedDx, int blockedDy)
    {
        int openX = x + openDx;
        int openY = y + openDy;
        int blockedX = x + blockedDx;
        int blockedY = y + blockedDy;
        return IsInBounds(openX, openY)
            && IsInBounds(blockedX, blockedY)
            && Cells[openY * Width + openX] != Grid.Blocked
            && Cells[blockedY * Width + blockedX] == Grid.Blocked;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsInBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>
    /// Fills the four straight tables: for every cell, the distance to the next primary jump
    /// point in that direction, or to the wall when there is none.
    /// </summary>
    private (short[] East, short[] North, short[] West, short[] South) BuildStraightTables(
        byte[] primary
    )
    {
        short[] wallDistance = new short[NodeCount];
        short[] eastTable = SweepStraightTable(primary, wallDistance, East, EastFlag);
        short[] westTable = SweepStraightTable(primary, wallDistance, West, WestFlag);
        short[] northTable = SweepStraightTable(primary, wallDistance, North, NorthFlag);
        short[] southTable = SweepStraightTable(primary, wallDistance, South, SouthFlag);
        return (eastTable, northTable, westTable, southTable);
    }

    private short[] SweepStraightTable(
        byte[] primary,
        short[] wallDistance,
        int direction,
        byte flag
    )
    {
        int stepX = Grid.OffX[direction];
        int stepY = Grid.OffY[direction];
        int neighborOffset = stepX + stepY * Width;
        short[] table = new short[NodeCount];

        for (int row = 0; row < Height; row++)
        {
            int y = stepY > 0 ? Height - 1 - row : row;
            for (int column = 0; column < Width; column++)
            {
                int x = stepX > 0 ? Width - 1 - column : column;
                int cell = y * Width + x;
                if (Cells[cell] == Grid.Blocked)
                {
                    table[cell] = 0;
                    wallDistance[cell] = 0;
                    continue;
                }
                int neighbor = cell + neighborOffset;
                bool canStep = IsWalkable(x + stepX, y + stepY);
                int jumpDistance =
                    canStep && (primary[neighbor] & flag) != 0 ? 1
                    : canStep && table[neighbor] > 0 ? table[neighbor] + 1
                    : 0;
                int wallSteps = canStep ? wallDistance[neighbor] + 1 : 0;
                table[cell] = (short)jumpDistance;
                wallDistance[cell] = (short)wallSteps;
                _distanceTable[TableIndex(direction, cell)] = (short)(
                    jumpDistance > 0 ? jumpDistance : -wallSteps
                );
            }
        }

        return table;
    }

    /// <summary>
    /// Fills the four diagonal tables: distance to the next diagonal jump point (or the wall).
    /// A diagonal jump point is a cell whose diagonal move has a forced neighbour, or from which
    /// a straight jump exists in either component direction.
    /// </summary>
    private void BuildDiagonalTables(
        byte[] primary,
        (short[] East, short[] North, short[] West, short[] South) straight
    )
    {
        short[] wallDistance = new short[NodeCount];
        SweepDiagonalTable(primary, straight, wallDistance, 1);
        SweepDiagonalTable(primary, straight, wallDistance, 3);
        SweepDiagonalTable(primary, straight, wallDistance, 5);
        SweepDiagonalTable(primary, straight, wallDistance, 7);
    }

    private void SweepDiagonalTable(
        byte[] primary,
        (short[] East, short[] North, short[] West, short[] South) straight,
        short[] wallDistance,
        int direction
    )
    {
        int stepX = Grid.OffX[direction];
        int stepY = Grid.OffY[direction];
        int neighborOffset = stepX + stepY * Width;
        short[] horizontal = stepX > 0 ? straight.East : straight.West;
        byte horizontalFlag = stepX > 0 ? EastFlag : WestFlag;
        short[] vertical = stepY > 0 ? straight.North : straight.South;
        byte verticalFlag = stepY > 0 ? NorthFlag : SouthFlag;
        short[] diagonalJump = new short[NodeCount];

        for (int row = 0; row < Height; row++)
        {
            int y = stepY > 0 ? Height - 1 - row : row;
            for (int x = 0; x < Width; x++)
            {
                int cell = y * Width + x;
                if (Cells[cell] == Grid.Blocked || !CanStepDiagonally(cell, x, y, stepX, stepY))
                {
                    diagonalJump[cell] = 0;
                    wallDistance[cell] = 0;
                    continue;
                }
                int next = cell + neighborOffset;
                bool isJumpPoint = IsDiagonalJumpPoint(
                    next,
                    stepX,
                    stepY,
                    (horizontal, horizontalFlag),
                    (vertical, verticalFlag),
                    primary
                );
                int jumpDistance =
                    isJumpPoint ? 1
                    : diagonalJump[next] > 0 ? diagonalJump[next] + 1
                    : 0;
                int wallSteps = wallDistance[next] + 1;
                diagonalJump[cell] = (short)jumpDistance;
                wallDistance[cell] = (short)wallSteps;
                _distanceTable[TableIndex(direction, cell)] = (short)(
                    jumpDistance > 0 ? jumpDistance : -wallSteps
                );
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanStepDiagonally(int cell, int x, int y, int stepX, int stepY) =>
        IsInBounds(x + stepX, y + stepY)
        && Cells[cell + stepX + stepY * Width] != Grid.Blocked
        && (Cells[cell + stepX] != Grid.Blocked || Cells[cell + stepY * Width] != Grid.Blocked);

    /// <summary>
    /// Static diagonal jump point test at a cell for travel direction (dx, dy). A straight jump
    /// must exist in either component direction; the straight scan checks the component neighbour
    /// cell itself before stepping, so a primary jump point there counts.
    /// </summary>
    private bool IsDiagonalJumpPoint(
        int cell,
        int dx,
        int dy,
        (short[] Table, byte Flag) horizontal,
        (short[] Table, byte Flag) vertical,
        byte[] primary
    )
    {
        int x = cell % Width;
        int y = cell / Width;

        return HasForcedDiagonalNeighbor(x, y, dx, dy)
            || (IsInBounds(x + dx, y) && IsStraightJumpSource(cell + dx, horizontal, primary))
            || (
                IsInBounds(x, y + dy) && IsStraightJumpSource(cell + dy * Width, vertical, primary)
            );
    }

    private bool HasForcedDiagonalNeighbor(int x, int y, int dx, int dy)
    {
        int behindX = x - dx;
        int behindY = y + dy;
        if (
            IsInBounds(behindX, behindY)
            && Cells[behindY * Width + behindX] != Grid.Blocked
            && Cells[y * Width + behindX] == Grid.Blocked
        )
        {
            return true;
        }

        int sideX = x + dx;
        int sideY = y - dy;
        return IsInBounds(sideX, sideY)
            && Cells[sideY * Width + sideX] != Grid.Blocked
            && Cells[sideY * Width + x] == Grid.Blocked;
    }

    private static bool IsStraightJumpSource(
        int componentCell,
        (short[] Table, byte Flag) component,
        byte[] primary
    ) => (primary[componentCell] & component.Flag) != 0 || component.Table[componentCell] > 0;

    public override bool FindPath(int start, int goal, out int cost, out int expanded)
    {
        cost = 0;
        expanded = 0;
        if (start == goal)
        {
            return true;
        }

        int generation = StartSearch(start, goal);
        _parentDirection[start] = NoParentDirection;

        _openList.Clear();
        int startF = Heuristic(Grid.XOf(start), Grid.YOf(start), 0);
        _openGeneration[start] = generation;
        _fScore[start] = startF;
        _openList.Insert(start, startF);

        int expandedCount = 0;
        while (_openList.Count > 0)
        {
            int currentId = _openList.Pop(out _);
            CloseNode(currentId, generation);
            expandedCount++;

            if (currentId == GoalId)
            {
                cost = CostToReach[currentId];
                expanded = expandedCount;
                return true;
            }

            ExpandNode(currentId, generation);
        }

        expanded = expandedCount;
        return false;
    }

    private void ExpandNode(int currentId, int generation)
    {
        int x = Grid.XOf(currentId);
        int y = Grid.YOf(currentId);
        int currentCost = CostToReach[currentId];
        int parentDirection = _parentDirection[currentId];

        if (parentDirection == NoParentDirection)
        {
            ExpandStartNode(currentId, x, y, currentCost, generation);
            return;
        }

        int parentDx = Grid.OffX[parentDirection];
        int parentDy = Grid.OffY[parentDirection];

        if (parentDx != 0 && parentDy != 0)
        {
            ExpandAfterDiagonal(currentId, x, y, currentCost, generation, parentDx, parentDy);
        }
        else if (parentDx != 0)
        {
            ExpandAfterHorizontal(currentId, x, y, currentCost, generation, parentDx);
        }
        else
        {
            ExpandAfterVertical(currentId, x, y, currentCost, generation, parentDy);
        }
    }

    private void ExpandStartNode(int currentId, int x, int y, int currentCost, int generation)
    {
        for (int direction = 0; direction < Grid.InPlaneDirectionCount; direction++)
        {
            int dx = Grid.OffX[direction];
            int dy = Grid.OffY[direction];
            if (_intermediatePruning && (direction & 1) != 0)
            {
                TryNaturalDiagonal(currentId, x, y, currentCost, generation, dx, dy, direction);
            }
            else
            {
                TryJumpDirection(currentId, x, y, currentCost, generation, dx, dy, direction);
            }
        }
    }

    private void ExpandAfterDiagonal(
        int currentId,
        int x,
        int y,
        int currentCost,
        int generation,
        int parentDx,
        int parentDy
    )
    {
        RelaxSplitComponents(currentId, x, y, currentCost, generation, parentDx, parentDy);
        if (IsWalkable(x, y + parentDy) || IsWalkable(x + parentDx, y))
        {
            int parentDirection = Grid.DirectionOf(parentDx, parentDy);
            if (_intermediatePruning)
            {
                TryNaturalDiagonal(
                    currentId,
                    x,
                    y,
                    currentCost,
                    generation,
                    parentDx,
                    parentDy,
                    parentDirection
                );
            }
            else
            {
                TryJumpDirection(
                    currentId,
                    x,
                    y,
                    currentCost,
                    generation,
                    parentDx,
                    parentDy,
                    parentDirection
                );
            }
        }
        RelaxForcedDiagonals(currentId, x, y, currentCost, generation, parentDx, parentDy);
    }

    private void ExpandAfterHorizontal(
        int currentId,
        int x,
        int y,
        int currentCost,
        int generation,
        int parentDx
    )
    {
        if (IsWalkable(x + parentDx, y))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, parentDx, 0);
        }
        if (!IsWalkable(x, y + 1) && IsWalkable(x + parentDx, y + 1))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, parentDx, 1);
        }
        if (!IsWalkable(x, y - 1) && IsWalkable(x + parentDx, y - 1))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, parentDx, -1);
        }
    }

    private void ExpandAfterVertical(
        int currentId,
        int x,
        int y,
        int currentCost,
        int generation,
        int parentDy
    )
    {
        if (IsWalkable(x, y + parentDy))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, 0, parentDy);
        }
        if (!IsWalkable(x + 1, y) && IsWalkable(x + 1, y + parentDy))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, 1, parentDy);
        }
        if (!IsWalkable(x - 1, y) && IsWalkable(x - 1, y + parentDy))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, -1, parentDy);
        }
    }

    private void RelaxSplitComponents(
        int currentId,
        int x,
        int y,
        int currentCost,
        int generation,
        int dx,
        int dy
    )
    {
        if (IsWalkable(x, y + dy))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, 0, dy);
        }
        if (IsWalkable(x + dx, y))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, dx, 0);
        }
    }

    private void RelaxForcedDiagonals(
        int currentId,
        int x,
        int y,
        int currentCost,
        int generation,
        int dx,
        int dy
    )
    {
        if (!IsWalkable(x - dx, y) && IsWalkable(x - dx, y + dy))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, -dx, dy);
        }
        if (!IsWalkable(x, y - dy) && IsWalkable(x + dx, y - dy))
        {
            TryJumpToward(currentId, x, y, currentCost, generation, dx, -dy);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TryJumpToward(
        int parentId,
        int x,
        int y,
        int costSoFar,
        int generation,
        int dx,
        int dy
    ) => TryJumpDirection(parentId, x, y, costSoFar, generation, dx, dy, Grid.DirectionOf(dx, dy));

    /// <summary>Reads the table for one step, relaxes goal crossings and the static landing jump point.</summary>
    private void TryJumpDirection(
        int parentId,
        int x,
        int y,
        int costSoFar,
        int generation,
        int dx,
        int dy,
        int direction
    )
    {
        int neighborX = x + dx;
        int neighborY = y + dy;
        if (!IsWalkable(neighborX, neighborY))
        {
            return;
        }

        bool diagonal = dx != 0 && dy != 0;
        if (diagonal && !IsWalkable(x + dx, y) && !IsWalkable(x, y + dy))
        {
            return;
        }

        int tableValue = _distanceTable[TableIndex(direction, y * Width + x)];
        int run = tableValue > 0 ? tableValue : -tableValue;
        if (run == 0)
        {
            return;
        }

        if (diagonal)
        {
            RelaxDiagonalGoalCrossings(parentId, x, y, costSoFar, run, direction, generation);
        }
        else
        {
            RelaxStraightGoalCrossing(parentId, x, y, costSoFar, run, direction, generation);
        }

        if (tableValue > 0)
        {
            int jumpX = x + tableValue * dx;
            int jumpY = y + tableValue * dy;
            RelaxJumpPoint(
                jumpY * Width + jumpX,
                jumpX,
                jumpY,
                costSoFar + tableValue * (diagonal ? 14 : 10),
                parentId,
                direction,
                generation
            );
        }
    }

    /// <summary>
    /// Intermediate-pruning variant: follows the diagonal chain of jump points in place, expanding
    /// each intermediate's canonical successors directly and never queueing the intermediates.
    /// </summary>
    private void TryNaturalDiagonal(
        int parentId,
        int x,
        int y,
        int costSoFar,
        int generation,
        int dx,
        int dy,
        int direction
    )
    {
        int currentX = x,
            currentY = y,
            currentCost = costSoFar,
            currentParent = parentId;
        while (true)
        {
            int tableValue = _distanceTable[TableIndex(direction, currentY * Width + currentX)];
            int run = tableValue > 0 ? tableValue : -tableValue;
            if (run == 0)
            {
                return;
            }

            RelaxDiagonalGoalCrossings(
                currentParent,
                currentX,
                currentY,
                currentCost,
                run,
                direction,
                generation
            );
            if (tableValue <= 0)
            {
                return;
            }

            int intermediateX = currentX + tableValue * dx;
            int intermediateY = currentY + tableValue * dy;
            int intermediateId = intermediateY * Width + intermediateX;
            int intermediateCost = currentCost + tableValue * 14;

            RelaxSplitComponents(
                intermediateId,
                intermediateX,
                intermediateY,
                intermediateCost,
                generation,
                dx,
                dy
            );
            RelaxForcedDiagonals(
                intermediateId,
                intermediateX,
                intermediateY,
                intermediateCost,
                generation,
                dx,
                dy
            );

            currentX = intermediateX;
            currentY = intermediateY;
            currentCost = intermediateCost;
            currentParent = intermediateId;
        }
    }

    /// <summary>
    /// Goal shortcuts for a diagonal segment: the goal on the ray, or a turn where the segment
    /// crosses the goal's row or column. Mirrors the target-jump-point rule from Game AI Pro 2.
    /// </summary>
    private void RelaxDiagonalGoalCrossings(
        int parentId,
        int x,
        int y,
        int costSoFar,
        int run,
        int direction,
        int generation
    )
    {
        int dx = Grid.OffX[direction];
        int dy = Grid.OffY[direction];
        int columnSteps = (GoalX - x) * dx;
        int rowSteps = (GoalY - y) * dy;

        if (columnSteps == rowSteps && columnSteps >= 1 && columnSteps <= run)
        {
            RelaxJumpPoint(
                GoalId,
                GoalX,
                GoalY,
                costSoFar + columnSteps * 14,
                parentId,
                direction,
                generation
            );
        }

        if (rowSteps >= 1 && rowSteps <= run)
        {
            RelaxRowCrossing(parentId, x, costSoFar, rowSteps, dx, direction, generation);
        }

        if (columnSteps >= 1 && columnSteps <= run)
        {
            RelaxColumnCrossing(parentId, y, costSoFar, columnSteps, dy, direction, generation);
        }
    }

    /// <summary>The segment crosses the goal's row; a straight jump from the turn cell may reach the goal.</summary>
    private void RelaxRowCrossing(
        int parentId,
        int x,
        int costSoFar,
        int rowSteps,
        int dx,
        int direction,
        int generation
    )
    {
        int turnX = x + rowSteps * dx;
        int straightSteps = (GoalX - turnX) * dx;
        if (straightSteps < 1 || !IsWalkable(turnX + dx, GoalY))
        {
            return;
        }

        int scanStartX = turnX + dx;
        int scanDistance = (GoalX - scanStartX) * dx;
        int reach = TableRun(Grid.DirectionOf(dx, 0), GoalY * Width + scanStartX);
        if (scanDistance >= 0 && scanDistance <= reach)
        {
            RelaxJumpPoint(
                GoalId,
                GoalX,
                GoalY,
                costSoFar + rowSteps * 14 + straightSteps * 10,
                parentId,
                direction,
                generation
            );
        }
    }

    /// <summary>The segment crosses the goal's column; symmetric to <see cref="RelaxRowCrossing"/>.</summary>
    private void RelaxColumnCrossing(
        int parentId,
        int y,
        int costSoFar,
        int columnSteps,
        int dy,
        int direction,
        int generation
    )
    {
        int turnY = y + columnSteps * dy;
        int straightSteps = (GoalY - turnY) * dy;
        if (straightSteps < 1 || !IsWalkable(GoalX, turnY + dy))
        {
            return;
        }

        int scanStartY = turnY + dy;
        int scanDistance = (GoalY - scanStartY) * dy;
        int reach = TableRun(Grid.DirectionOf(0, dy), scanStartY * Width + GoalX);
        if (scanDistance >= 0 && scanDistance <= reach)
        {
            RelaxJumpPoint(
                GoalId,
                GoalX,
                GoalY,
                costSoFar + columnSteps * 14 + straightSteps * 10,
                parentId,
                direction,
                generation
            );
        }
    }

    /// <summary>Goal shortcut for a straight segment: the goal lies ahead on the same row or column.</summary>
    private void RelaxStraightGoalCrossing(
        int parentId,
        int x,
        int y,
        int costSoFar,
        int run,
        int direction,
        int generation
    )
    {
        int dx = Grid.OffX[direction];
        int dy = Grid.OffY[direction];
        if (dx != 0)
        {
            if (GoalY != y)
            {
                return;
            }
            int steps = (GoalX - x) * dx;
            if (steps >= 1 && steps <= run)
            {
                RelaxJumpPoint(
                    GoalId,
                    GoalX,
                    GoalY,
                    costSoFar + steps * 10,
                    parentId,
                    direction,
                    generation
                );
            }
        }
        else
        {
            if (GoalX != x)
            {
                return;
            }
            int steps = (GoalY - y) * dy;
            if (steps >= 1 && steps <= run)
            {
                RelaxJumpPoint(
                    GoalId,
                    GoalX,
                    GoalY,
                    costSoFar + steps * 10,
                    parentId,
                    direction,
                    generation
                );
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int TableRun(int direction, int cell)
    {
        int value = _distanceTable[TableIndex(direction, cell)];
        return value > 0 ? value : -value;
    }

    private void RelaxJumpPoint(
        int cell,
        int x,
        int y,
        int cost,
        int parentId,
        int direction,
        int generation
    )
    {
        if (IsClosed(cell, generation))
        {
            return;
        }
        if (HasCost(cell, generation) && cost >= CostToReach[cell])
        {
            return;
        }

        CostToReach[cell] = cost;
        CostGeneration[cell] = generation;
        Parent[cell] = parentId;
        _parentDirection[cell] = (byte)direction;

        int fScore = cost + Grid.Octile(x - GoalX, y - GoalY);
        if (_openGeneration[cell] != generation)
        {
            _openGeneration[cell] = generation;
            _fScore[cell] = fScore;
            _openList.Insert(cell, fScore);
        }
        else if (fScore < _fScore[cell])
        {
            _fScore[cell] = fScore;
            _openList.Decrease(cell, fScore);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsWalkable(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height && Cells[y * Width + x] != Grid.Blocked;
}
