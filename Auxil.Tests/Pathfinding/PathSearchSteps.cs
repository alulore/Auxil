using Auxil.Pathfinding;

namespace Auxil.Tests.Pathfinding;

internal static class PathSearchSteps
{
    private const int StepLimit = 10_000;

    public static int RunToCompletion(this PathSearch search, int budget)
    {
        int steps = 0;
        while (search.Step(budget) is null)
        {
            search.IsComplete.ShouldBeFalse();
            steps++;
            steps.ShouldBeLessThan(StepLimit);
        }

        return steps;
    }
}
