using System.Diagnostics;
using System.Text;

namespace Auxil.Common.Cli;

/// <summary>A fluent builder for executing command-line processes with output capture.</summary>
[PublicAPI]
public class CliCommand
{
    private readonly List<string> _argumentList = [];
    private readonly Dictionary<string, string> _environmentVariables = [];

    /// <summary>The executable to run.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>The raw argument string passed to the process.</summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>The working directory of the process.</summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>Whether to use the shell to launch the process.</summary>
    public bool UseShellExecute { get; set; }

    /// <summary>Whether the process runs without a console window.</summary>
    public bool CreateNoWindow { get; set; } = true;

    /// <summary>Whether standard error is redirected and captured.</summary>
    public bool RedirectStandardError { get; set; } = true;

    /// <summary>Whether standard output is redirected and captured.</summary>
    public bool RedirectStandardOutput { get; set; } = true;

    /// <summary>Raised for each line of standard output.</summary>
    public event EventHandler<CliOutputEventArgs>? OutputReceived;

    /// <summary>Raised for each line of standard error output.</summary>
    public event EventHandler<CliOutputEventArgs>? ErrorOutputReceived;

    /// <summary>Sets the executable to run.</summary>
    /// <param name="command">The executable.</param>
    public CliCommand Call(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        Command = command;

        return this;
    }

    /// <summary>Executes the process synchronously and returns the captured result.</summary>
    public CliResult Execute()
    {
        StringBuilder outputBuilder = new();
        StringBuilder errorsBuilder = new();
        using Process process = CreateProcess(outputBuilder, errorsBuilder);

        process.Start();

        DateTime startTime = process.StartTime;
        Stopwatch stopwatch = Stopwatch.StartNew();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        stopwatch.Stop();

        return BuildResult(process, outputBuilder, errorsBuilder, startTime, stopwatch.Elapsed);
    }

    /// <summary>Executes the process and returns the captured result.</summary>
    /// <remarks>On Unix, StartTime and ExitTime read /proc/&lt;pid&gt;/stat, which is gone once the process is reaped, so the start time is captured while the process is alive and the exit time is derived from a stopwatch. The process object is settled with a synchronous wait afterwards because WaitForExitAsync releases the handle on Unix, which makes ExitCode throw, and the wait also drains the async output handlers.</remarks>
    /// <param name="timeout">The maximum time to wait; when exceeded the process is killed and a <see cref="TimeoutException"/> is thrown.</param>
    /// <param name="cancellationToken">Cancels the wait for the process.</param>
    public async Task<CliResult> ExecuteAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        StringBuilder outputBuilder = new();
        StringBuilder errorsBuilder = new();
        Process process = CreateProcess(outputBuilder, errorsBuilder);

        try
        {
            process.Start();

            DateTime startTime = process.StartTime;
            Stopwatch stopwatch = Stopwatch.StartNew();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using CancellationTokenSource? timeoutSource = CreateTimeoutSource(
                timeout,
                cancellationToken
            );

            await WaitForExitAsync(process, timeoutSource, cancellationToken).ConfigureAwait(false);

            await Task.Run(process.WaitForExit, CancellationToken.None).ConfigureAwait(false);
            stopwatch.Stop();

            return BuildResult(process, outputBuilder, errorsBuilder, startTime, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);

            throw;
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>Sets the working directory of the process.</summary>
    /// <param name="workingDirectory">The working directory.</param>
    public CliCommand SetWorkingDirectory(string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);

        WorkingDirectory = workingDirectory;

        return this;
    }

    /// <summary>Sets the raw argument string; clears any argument list set via the params overload.</summary>
    /// <param name="arguments">The argument string.</param>
    public CliCommand SetArguments(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        Arguments = arguments;
        _argumentList.Clear();

        return this;
    }

    /// <summary>Sets the arguments via the process argument list, avoiding quoting bugs; clears any raw argument string.</summary>
    /// <param name="arguments">The arguments.</param>
    public CliCommand SetArguments(params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        Arguments = string.Empty;
        _argumentList.Clear();
        _argumentList.AddRange(arguments);

        return this;
    }

    /// <summary>Sets an environment variable for the process.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    public CliCommand SetEnvironmentVariable(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        _environmentVariables[name] = value;

        return this;
    }

    /// <summary>Sets whether the process runs without a console window.</summary>
    /// <param name="createNoWindow">Whether to suppress the console window.</param>
    public CliCommand SetCreateNoWindow(bool createNoWindow)
    {
        CreateNoWindow = createNoWindow;

        return this;
    }

    /// <summary>Sets whether the process is launched through the shell.</summary>
    /// <param name="useShellExecute">Whether to use the shell.</param>
    public CliCommand SetUseShellExecute(bool useShellExecute)
    {
        UseShellExecute = useShellExecute;

        return this;
    }

    /// <summary>Sets whether standard error is captured.</summary>
    /// <param name="redirectStandardError">Whether to redirect standard error.</param>
    public CliCommand SetRedirectStandardError(bool redirectStandardError)
    {
        RedirectStandardError = redirectStandardError;

        return this;
    }

    /// <summary>Sets whether standard output is captured.</summary>
    /// <param name="redirectStandardOutput">Whether to redirect standard output.</param>
    public CliCommand SetRedirectStandardOutput(bool redirectStandardOutput)
    {
        RedirectStandardOutput = redirectStandardOutput;

        return this;
    }

    private Process CreateProcess(StringBuilder outputBuilder, StringBuilder errorsBuilder)
    {
        Process process = new() { StartInfo = BuildStartInfo() };

        process.OutputDataReceived += (_, args) =>
        {
            outputBuilder.AppendWithNewLine(args.Data);
            OnOutputReceived(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            errorsBuilder.AppendWithNewLine(args.Data);
            OnErrorOutputReceived(args.Data);
        };

        return process;
    }

    private static CliResult BuildResult(
        Process process,
        StringBuilder outputBuilder,
        StringBuilder errorsBuilder,
        DateTime startTime,
        TimeSpan elapsed
    )
    {
        return new CliResult
        {
            Output = outputBuilder.ToString(),
            ErrorOutput = errorsBuilder.ToString(),
            ExitCode = process.ExitCode,
            StartTime = startTime,
            ExitTime = startTime + elapsed,
        };
    }

    private ProcessStartInfo BuildStartInfo()
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Command,
            CreateNoWindow = CreateNoWindow,
            WorkingDirectory = WorkingDirectory,
            RedirectStandardOutput = RedirectStandardOutput,
            RedirectStandardError = RedirectStandardError,
            UseShellExecute = UseShellExecute,
        };

        if (Arguments.Length > 0)
        {
            startInfo.Arguments = Arguments;
        }

        foreach (string argument in _argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (KeyValuePair<string, string> variable in _environmentVariables)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        return startInfo;
    }

    private async Task WaitForExitAsync(
        Process process,
        CancellationTokenSource? timeoutSource,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await process
                .WaitForExitAsync(timeoutSource?.Token ?? cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (timeoutSource is not null
                && timeoutSource.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested
            )
        {
            TryKillProcess(process);

            throw new TimeoutException(
                $"The process '{Command}' did not finish within the configured timeout.",
                exception
            );
        }
    }

    private void OnOutputReceived(string? message)
    {
        OutputReceived?.Invoke(this, new CliOutputEventArgs(message ?? string.Empty));
    }

    private void OnErrorOutputReceived(string? message)
    {
        ErrorOutputReceived?.Invoke(this, new CliOutputEventArgs(message ?? string.Empty));
    }

    private static CancellationTokenSource? CreateTimeoutSource(
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        if (timeout is not { } limit)
        {
            return null;
        }

        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        source.CancelAfter(limit);
        return source;
    }

    private static bool TryKillProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
