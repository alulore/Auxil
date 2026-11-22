using Auxil.Common.Cli;
using JetBrains.Annotations;

namespace Auxil.Tests.Common.Cli;

[TestSubject(typeof(CliCommand))]
public class CliTest
{
    [Fact]
    public void Execute_ReturnsZeroExitCodeForSuccessfulCommand()
    {
        CliResult result = new CliCommand().Call("dotnet").SetArguments("--version").Execute();

        result.ExitCode.ShouldBe(0);
        result.Success.ShouldBeTrue();
        result.Output.ShouldNotBeEmpty();
    }

    [Fact]
    public void Execute_ReturnsNonZeroExitCodeForFailingCommand()
    {
        CliResult result = new CliCommand()
            .Call("dotnet")
            .SetArguments("definitely-not-a-real-command-xyz")
            .Execute();

        result.ExitCode.ShouldNotBe(0);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSameResultsAsSync()
    {
        CliResult result = await Echo("hello")
            .ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("hello");
    }

    [Fact]
    public void Execute_CapturesErrorOutput()
    {
        CliResult result = EchoToError("oops").Execute();

        result.ErrorOutput.ShouldContain("oops");
    }

    [Fact]
    public void Execute_RaisesOutputEvent()
    {
        CliCommand cli = Echo("hello");

        List<string> lines = [];
        cli.OutputReceived += (_, e) => lines.Add(e.Line);

        cli.Execute();

        lines.ShouldContain(line => line.Contains("hello", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_RaisesErrorEvent()
    {
        CliCommand cli = EchoToError("oops");

        List<string> lines = [];
        cli.ErrorOutputReceived += (_, e) => lines.Add(e.Line);

        cli.Execute();

        lines.ShouldContain(line => line.Contains("oops", StringComparison.Ordinal));
    }

    [Fact]
    public void SetArguments_WithParamsUsesArgumentList()
    {
        CliResult result = new CliCommand().Call("dotnet").SetArguments("--version").Execute();

        result.ExitCode.ShouldBe(0);
        result.Output.ShouldNotBeEmpty();
    }

    [Fact]
    public void SetArguments_WithQuotedArgumentPreservesSpaces()
    {
        CliResult result = Echo("hello world").Execute();

        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("hello world");
    }

    [Fact]
    public void SetWorkingDirectory_IsApplied()
    {
        CliResult result = new CliCommand()
            .Call("dotnet")
            .SetArguments("--version")
            .SetWorkingDirectory(Path.GetTempPath())
            .Execute();

        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void SetEnvironmentVariable_IsPassedToProcess()
    {
        CliResult result = PrintVariable("UPHORIC_TEST_VAR")
            .SetEnvironmentVariable("UPHORIC_TEST_VAR", "hello")
            .Execute();

        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("hello");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsTimeoutExceptionWhenProcessExceedsTimeout()
    {
        await Should.ThrowAsync<TimeoutException>(() =>
            LongRunning()
                .ExecuteAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledExceptionWhenCancelled()
    {
        using CancellationTokenSource cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            LongRunning().ExecuteAsync(TimeSpan.FromSeconds(10), cancellation.Token)
        );
    }

    [Fact]
    public void FluentSetters_UpdateProperties()
    {
        CliCommand cli = new();

        cli.SetCreateNoWindow(false).CreateNoWindow.ShouldBeFalse();
        cli.SetUseShellExecute(true).UseShellExecute.ShouldBeTrue();
        cli.SetRedirectStandardError(false).RedirectStandardError.ShouldBeFalse();
        cli.SetRedirectStandardOutput(false).RedirectStandardOutput.ShouldBeFalse();
        cli.SetWorkingDirectory("C:\\temp").WorkingDirectory.ShouldBe("C:\\temp");
    }

    private static CliCommand Echo(string text) =>
        OperatingSystem.IsWindows()
            ? new CliCommand().Call("cmd").SetArguments("/c", "echo", text)
            : new CliCommand().Call("echo").SetArguments(text);

    private static CliCommand EchoToError(string text) =>
        OperatingSystem.IsWindows()
            ? new CliCommand().Call("cmd").SetArguments("/c", $"echo {text} 1>&2")
            : new CliCommand().Call("sh").SetArguments("-c", $"echo {text} >&2");

    private static CliCommand PrintVariable(string name) =>
        OperatingSystem.IsWindows()
            ? new CliCommand().Call("cmd").SetArguments("/c", "echo", $"%{name}%")
            : new CliCommand().Call("sh").SetArguments("-c", $"echo \"${name}\"");

    private static CliCommand LongRunning() =>
        OperatingSystem.IsWindows()
            ? new CliCommand().Call("ping").SetArguments("-n", "5", "127.0.0.1")
            : new CliCommand().Call("sleep").SetArguments("5");
}
