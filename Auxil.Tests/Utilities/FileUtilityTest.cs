using Auxil.Utilities;
using JetBrains.Annotations;

namespace Auxil.Tests.Utilities;

[TestSubject(typeof(FileUtility))]
public class FileUtilityTest : IDisposable
{
    private readonly string _sourceDirectory = Path.Combine(
        Path.GetTempPath(),
        $"uphoric-test-src-{Guid.NewGuid():N}"
    );
    private readonly string _destinationDirectory = Path.Combine(
        Path.GetTempPath(),
        $"uphoric-test-dst-{Guid.NewGuid():N}"
    );

    public FileUtilityTest()
    {
        Directory.CreateDirectory(_sourceDirectory);
        File.WriteAllText(Path.Combine(_sourceDirectory, "root.txt"), "root");
        Directory.CreateDirectory(Path.Combine(_sourceDirectory, "sub"));
        File.WriteAllText(Path.Combine(_sourceDirectory, "sub", "nested.txt"), "nested");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_sourceDirectory))
        {
            Directory.Delete(_sourceDirectory, true);
        }

        if (Directory.Exists(_destinationDirectory))
        {
            Directory.Delete(_destinationDirectory, true);
        }
    }

    [Fact]
    public void CopyDirectory_CopiesFullTree()
    {
        FileUtility.CopyDirectory(_sourceDirectory, _destinationDirectory).ShouldBeTrue();

        File.ReadAllText(Path.Combine(_destinationDirectory, "root.txt")).ShouldBe("root");
        File.ReadAllText(Path.Combine(_destinationDirectory, "sub", "nested.txt"))
            .ShouldBe("nested");
    }

    [Fact]
    public void CopyDirectory_MissingSourceReturnsFalse()
    {
        FileUtility
            .CopyDirectory(Path.Combine(_sourceDirectory, "missing"), _destinationDirectory)
            .ShouldBeFalse();
    }

    [Fact]
    public void CopyDirectory_ExistingDestinationWithoutOverwriteReturnsFalse()
    {
        Directory.CreateDirectory(_destinationDirectory);

        FileUtility.CopyDirectory(_sourceDirectory, _destinationDirectory).ShouldBeFalse();
    }

    [Fact]
    public void CopyDirectory_OverwriteCopiesOverExistingFiles()
    {
        Directory.CreateDirectory(_destinationDirectory);
        File.WriteAllText(Path.Combine(_destinationDirectory, "root.txt"), "stale");

        FileUtility
            .CopyDirectory(_sourceDirectory, _destinationDirectory, overwrite: true)
            .ShouldBeTrue();

        File.ReadAllText(Path.Combine(_destinationDirectory, "root.txt")).ShouldBe("root");
    }

    [Fact]
    public void CopyDirectory_RejectsSourceAndDescendantDestinations()
    {
        Should.Throw<ArgumentException>(() =>
            FileUtility.CopyDirectory(_sourceDirectory, _sourceDirectory)
        );
        Should.Throw<ArgumentException>(() =>
            FileUtility.CopyDirectory(
                _sourceDirectory,
                Path.Combine(_sourceDirectory, "nested-copy")
            )
        );
    }

    [Fact]
    public async Task CopyDirectoryAsync_CopiesFullTree()
    {
        (
            await FileUtility.CopyDirectoryAsync(
                _sourceDirectory,
                _destinationDirectory,
                cancellationToken: TestContext.Current.CancellationToken
            )
        ).ShouldBeTrue();

        (
            await File.ReadAllTextAsync(
                Path.Combine(_destinationDirectory, "root.txt"),
                TestContext.Current.CancellationToken
            )
        ).ShouldBe("root");
        (
            await File.ReadAllTextAsync(
                Path.Combine(_destinationDirectory, "sub", "nested.txt"),
                TestContext.Current.CancellationToken
            )
        ).ShouldBe("nested");
    }

    [Fact]
    public async Task CopyDirectoryAsync_MissingSourceReturnsFalse()
    {
        (
            await FileUtility.CopyDirectoryAsync(
                Path.Combine(_sourceDirectory, "missing"),
                _destinationDirectory,
                cancellationToken: TestContext.Current.CancellationToken
            )
        ).ShouldBeFalse();
    }

    [Fact]
    public async Task CopyDirectoryAsync_ExistingDestinationWithoutOverwriteReturnsFalse()
    {
        Directory.CreateDirectory(_destinationDirectory);

        (
            await FileUtility.CopyDirectoryAsync(
                _sourceDirectory,
                _destinationDirectory,
                cancellationToken: TestContext.Current.CancellationToken
            )
        ).ShouldBeFalse();
    }

    [Fact]
    public async Task CopyDirectoryAsync_OverwriteCopiesOverExistingFiles()
    {
        Directory.CreateDirectory(_destinationDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_destinationDirectory, "root.txt"),
            "stale",
            TestContext.Current.CancellationToken
        );

        (
            await FileUtility.CopyDirectoryAsync(
                _sourceDirectory,
                _destinationDirectory,
                overwrite: true,
                cancellationToken: TestContext.Current.CancellationToken
            )
        ).ShouldBeTrue();

        (
            await File.ReadAllTextAsync(
                Path.Combine(_destinationDirectory, "root.txt"),
                TestContext.Current.CancellationToken
            )
        ).ShouldBe("root");
    }

    [Fact]
    public async Task CopyDirectoryAsync_RejectsSourceAndDescendantDestinations()
    {
        await Should.ThrowAsync<ArgumentException>(() =>
            FileUtility.CopyDirectoryAsync(
                _sourceDirectory,
                _sourceDirectory,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        await Should.ThrowAsync<ArgumentException>(() =>
            FileUtility.CopyDirectoryAsync(
                _sourceDirectory,
                Path.Combine(_sourceDirectory, "nested-copy"),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task CopyDirectoryAsync_ThrowsWhenCancelled()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            FileUtility.CopyDirectoryAsync(
                _sourceDirectory,
                _destinationDirectory,
                cancellationToken: cancellation.Token
            )
        );
    }
}
