namespace Auxil.Common.Cli;

/// <summary>Carries one line of process output.</summary>
[PublicAPI]
public sealed class CliOutputEventArgs(string line) : EventArgs
{
    /// <summary>The line of output text.</summary>
    public string Line { get; } = line;
}
