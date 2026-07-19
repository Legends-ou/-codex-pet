using System.Collections.ObjectModel;
using System.Text.Json;

namespace PetDesktop.Core.Pets;

public sealed record PetCatalogDiagnostic(
    string DirectoryPath,
    string Reason,
    string? ExceptionType = null);

public sealed class PetCatalog
{
    private const string ManifestFileName = "pet.json";
    private const int MaximumPackageNestingDepth = 3;
    private readonly Action<PetCatalogDiagnostic>? diagnosticSink;
    private readonly IPetCatalogFileSystem fileSystem;

    public PetCatalog(Action<PetCatalogDiagnostic>? diagnosticSink = null)
        : this(diagnosticSink, PhysicalPetCatalogFileSystem.Instance)
    {
    }

    internal PetCatalog(
        Action<PetCatalogDiagnostic>? diagnosticSink,
        IPetCatalogFileSystem fileSystem)
    {
        this.diagnosticSink = diagnosticSink;
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<IReadOnlyList<PetDescriptor>> ScanAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedRoot;
        try
        {
            normalizedRoot = Path.GetFullPath(rootPath);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            Report(new PetCatalogDiagnostic(rootPath ?? string.Empty, "Pet root path is invalid.", exception.GetType().Name));
            return Array.Empty<PetDescriptor>();
        }

        if (!fileSystem.DirectoryExists(normalizedRoot))
        {
            Report(new PetCatalogDiagnostic(normalizedRoot, "Pet root directory does not exist."));
            return Array.Empty<PetDescriptor>();
        }

        string[] directories;
        try
        {
            directories = fileSystem.GetDirectories(normalizedRoot);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && IsFileSystemException(exception))
        {
            Report(new PetCatalogDiagnostic(normalizedRoot, "Pet root directory could not be enumerated.", exception.GetType().Name));
            return Array.Empty<PetDescriptor>();
        }

        var candidates = new List<PetDescriptor>();
        foreach (var directory in EnumeratePackageDirectories(directories, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = await TryReadCandidateAsync(directory, cancellationToken).ConfigureAwait(false);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        candidates.Sort(PetDescriptorComparer.Instance);
        return new ReadOnlyCollection<PetDescriptor>(candidates);
    }

    private IEnumerable<string> EnumeratePackageDirectories(
        IEnumerable<string> topLevelDirectories,
        CancellationToken cancellationToken)
    {
        var pending = new Queue<(string Directory, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in topLevelDirectories)
        {
            pending.Enqueue((directory, 1));
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, depth) = pending.Dequeue();
            string normalizedDirectory;
            try
            {
                normalizedDirectory = Path.GetFullPath(directory);
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                Report(new PetCatalogDiagnostic(directory, "Pet package directory is invalid.", exception.GetType().Name));
                continue;
            }

            if (!visited.Add(normalizedDirectory)) continue;
            yield return normalizedDirectory;
            if (depth >= MaximumPackageNestingDepth) continue;

            string[] children;
            try
            {
                children = fileSystem.GetDirectories(normalizedDirectory);
            }
            catch (Exception exception) when (exception is not OperationCanceledException && IsFileSystemException(exception))
            {
                Report(new PetCatalogDiagnostic(normalizedDirectory, "Pet package directory could not be enumerated.", exception.GetType().Name));
                continue;
            }

            foreach (var child in children)
            {
                pending.Enqueue((child, depth + 1));
            }
        }
    }

    private async Task<PetDescriptor?> TryReadCandidateAsync(string directory, CancellationToken cancellationToken)
    {
        string normalizedDirectory;
        try
        {
            normalizedDirectory = Path.GetFullPath(directory);
            var manifestPath = Path.GetFullPath(Path.Combine(normalizedDirectory, ManifestFileName));
            if (!fileSystem.FileExists(manifestPath))
            {
                Report(new PetCatalogDiagnostic(normalizedDirectory, "Required pet manifest is missing."));
                return null;
            }

            var json = await fileSystem.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PetManifest>(json);
            if (manifest is null)
            {
                Report(new PetCatalogDiagnostic(normalizedDirectory, "Pet manifest is empty or invalid."));
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                Report(new PetCatalogDiagnostic(normalizedDirectory, "Pet manifest id is required."));
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.SpritesheetPath))
            {
                Report(new PetCatalogDiagnostic(normalizedDirectory, "Pet manifest spritesheetPath is required."));
                return null;
            }

            if (Path.IsPathRooted(manifest.SpritesheetPath))
            {
                Report(new PetCatalogDiagnostic(normalizedDirectory, "Pet spritesheet path must be relative."));
                return null;
            }

            var spritesheetPath = Path.GetFullPath(Path.Combine(normalizedDirectory, manifest.SpritesheetPath));
            if (!IsWithinDirectory(normalizedDirectory, spritesheetPath))
            {
                Report(new PetCatalogDiagnostic(normalizedDirectory, "Pet spritesheet path leaves its pet directory."));
                return null;
            }

            if (!fileSystem.FileExists(spritesheetPath))
            {
                Report(new PetCatalogDiagnostic(normalizedDirectory, "Required pet spritesheet is missing."));
                return null;
            }

            var directoryName = Path.GetFileName(normalizedDirectory);
            var displayName = string.IsNullOrWhiteSpace(manifest.DisplayName)
                ? directoryName
                : manifest.DisplayName;

            return new PetDescriptor(
                manifest.Id,
                displayName,
                manifest.Description,
                manifest.SpriteVersionNumber,
                normalizedDirectory,
                manifestPath,
                spritesheetPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsFileSystemException(exception) || exception is JsonException)
        {
            Report(new PetCatalogDiagnostic(
                TryNormalizeForDiagnostic(directory),
                "Pet candidate could not be read.",
                exception.GetType().Name));
            return null;
        }
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        var relativePath = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", StringComparison.OrdinalIgnoreCase)
            && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string TryNormalizeForDiagnostic(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return path;
        }
    }

    private static bool IsFileSystemException(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException;
    }

    private void Report(PetCatalogDiagnostic diagnostic)
    {
        try
        {
            diagnosticSink?.Invoke(diagnostic);
        }
        catch
        {
            // Diagnostics are best-effort and must never interrupt catalog scanning.
        }
    }

    private sealed class PetDescriptorComparer : IComparer<PetDescriptor>
    {
        public static PetDescriptorComparer Instance { get; } = new();

        public int Compare(PetDescriptor? left, PetDescriptor? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var displayNameComparison = StringComparer.CurrentCulture.Compare(left.DisplayName, right.DisplayName);
            if (displayNameComparison != 0)
            {
                return displayNameComparison;
            }

            var directoryNameComparison = StringComparer.OrdinalIgnoreCase.Compare(
                Path.GetFileName(left.PetDirectoryPath),
                Path.GetFileName(right.PetDirectoryPath));
            return directoryNameComparison != 0
                ? directoryNameComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left.PetDirectoryPath, right.PetDirectoryPath);
        }
    }
}
