using System.Collections;
using System.Text;
using PetDesktop.Core.Pets;

namespace PetDesktop.Core.Tests;

public sealed class PetCatalogTests
{
    [Fact]
    public async Task ScanAsyncSortsByDisplayNameUsingCurrentCulture()
    {
        using var source = new TestDirectory();
        source.CreatePet("third", "id-third", "Zulu");
        source.CreatePet("first", "id-first", "Alpha");
        source.CreatePet("second", "id-second", "Bravo");

        var candidates = await new PetCatalog().ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Equal(["id-first", "id-second", "id-third"], candidates.Select(pet => pet.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ScanAsyncFallsBackToDirectoryNameWhenDisplayNameIsMissingOrBlank(string? displayName)
    {
        using var source = new TestDirectory();
        source.CreatePet("Directory Name", "pet-id", displayName);

        var candidate = Assert.Single(await new PetCatalog().ScanAsync(source.RootPath, CancellationToken.None));

        Assert.Equal("Directory Name", candidate.DisplayName);
    }

    [Fact]
    public async Task ScanAsyncBreaksEqualDisplayNameTiesByDirectoryName()
    {
        using var source = new TestDirectory();
        source.CreatePet("z-directory", "z", "Same");
        source.CreatePet("A-directory", "a", "Same");

        var candidates = await new PetCatalog().ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Equal(["a", "z"], candidates.Select(pet => pet.Id));
    }

    [Theory]
    [InlineData("second", new[] { "second", "first", "third" })]
    [InlineData("missing", new[] { "first", "second", "third" })]
    [InlineData(null, new[] { "first", "second", "third" })]
    [InlineData("", new[] { "first", "second", "third" })]
    public void PreferSavedReturnsStableReadOnlyOrderWithoutMutatingInput(string? savedId, string[] expected)
    {
        var input = new List<PetDescriptor>
        {
            Descriptor("first"),
            Descriptor("second"),
            Descriptor("third"),
        };

        var ordered = PetCandidateOrder.PreferSaved(input, savedId);

        Assert.Equal(expected, ordered.Select(pet => pet.Id));
        Assert.Equal(["first", "second", "third"], input.Select(pet => pet.Id));
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList>(ordered).Add(Descriptor("fourth")));
    }

    [Fact]
    public void PreferSavedUsesOrdinalComparisonAndStablyMovesAllDuplicateIdsFirst()
    {
        var input = new[] { Descriptor("other"), Descriptor("saved"), Descriptor("SAVED"), Descriptor("saved") };

        var ordered = PetCandidateOrder.PreferSaved(input, "saved");

        Assert.Equal(["saved", "saved", "other", "SAVED"], ordered.Select(pet => pet.Id));
    }

    [Fact]
    public async Task ScanAsyncFindsAStandardPackageInsideAnArchiveWrapperDirectory()
    {
        using var source = new TestDirectory();
        source.CreatePet("top", "top-id", "Top");
        var container = Path.Combine(source.RootPath, "container");
        Directory.CreateDirectory(container);
        var nested = Path.Combine(container, "nested");
        Directory.CreateDirectory(nested);
        WriteManifest(nested, "nested-id", "Nested", "spritesheet.webp");
        File.WriteAllBytes(Path.Combine(nested, "spritesheet.webp"), [1]);

        var candidates = await new PetCatalog().ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Equal(["nested-id", "top-id"], candidates.Select(candidate => candidate.Id).Order());
    }

    [Fact]
    public async Task ScanAsyncSkipsBrokenJsonAndContinuesWithValidSibling()
    {
        using var source = new TestDirectory();
        var broken = Path.Combine(source.RootPath, "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "pet.json"), "{ definitely broken", Encoding.UTF8);
        source.CreatePet("valid", "valid-id", "Valid");
        var diagnostics = new List<PetCatalogDiagnostic>();

        var candidates = await new PetCatalog(diagnostics.Add).ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Equal("valid-id", Assert.Single(candidates).Id);
        Assert.Contains(diagnostics, diagnostic => diagnostic.DirectoryPath == Path.GetFullPath(broken));
    }

    [Fact]
    public async Task ScanAsyncSkipsMissingManifestAndRequiredBlankFields()
    {
        using var source = new TestDirectory();
        Directory.CreateDirectory(Path.Combine(source.RootPath, "missing-manifest"));
        source.CreatePet("blank-id", "   ");
        source.CreatePet("blank-path", "id", spritesheetPath: "   ", createSpritesheet: false);
        var diagnostics = new List<PetCatalogDiagnostic>();

        var candidates = await new PetCatalog(diagnostics.Add).ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Empty(candidates);
        Assert.Equal(3, diagnostics.Count);
    }

    [Fact]
    public async Task ScanAsyncRejectsAbsoluteParentAndSamePrefixSiblingEscapes()
    {
        using var source = new TestDirectory();
        var absoluteFile = Path.Combine(source.RootPath, "absolute.webp");
        File.WriteAllBytes(absoluteFile, [1]);
        source.CreatePet("absolute", "absolute-id", spritesheetPath: absoluteFile, createSpritesheet: false);
        source.CreatePet("parent", "parent-id", spritesheetPath: "..\\outside.webp", createSpritesheet: false);
        var siblingPet = source.CreatePet("pet", "sibling-id", spritesheetPath: "..\\pet-sibling\\sprite.webp", createSpritesheet: false);
        var siblingDirectory = Path.Combine(source.RootPath, "pet-sibling");
        Directory.CreateDirectory(siblingDirectory);
        File.WriteAllBytes(Path.Combine(siblingDirectory, "sprite.webp"), [1]);
        var diagnostics = new List<PetCatalogDiagnostic>();

        var candidates = await new PetCatalog(diagnostics.Add).ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Empty(candidates);
        Assert.Contains(diagnostics, diagnostic => diagnostic.DirectoryPath == Path.GetFullPath(siblingPet));
    }

    [Fact]
    public async Task ScanAsyncSkipsMissingSpritesheet()
    {
        using var source = new TestDirectory();
        source.CreatePet("missing-sprite", "id", createSpritesheet: false);

        var candidates = await new PetCatalog().ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task ScanAsyncReturnsEmptyAndDiagnosticWhenRootDoesNotExist()
    {
        using var source = new TestDirectory();
        var missingRoot = Path.Combine(source.RootPath, "does-not-exist");
        var diagnostics = new List<PetCatalogDiagnostic>();

        var candidates = await new PetCatalog(diagnostics.Add).ScanAsync(missingRoot, CancellationToken.None);

        Assert.Empty(candidates);
        Assert.Contains(diagnostics, diagnostic => diagnostic.DirectoryPath == Path.GetFullPath(missingRoot));
    }

    [Fact]
    public async Task DiagnosticsAreReadableDoNotExposeManifestContentsAndSinkFailuresAreContained()
    {
        using var source = new TestDirectory();
        const string secret = "SECRET-MANIFEST-CONTENT";
        var broken = Path.Combine(source.RootPath, "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "pet.json"), $"{{ \"secret\": \"{secret}\"", Encoding.UTF8);
        var recorded = new List<PetCatalogDiagnostic>();
        var catalog = new PetCatalog(diagnostic =>
        {
            recorded.Add(diagnostic);
            throw new InvalidOperationException("diagnostic sink failure");
        });

        var candidates = await catalog.ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Empty(candidates);
        var diagnostic = Assert.Single(recorded);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Reason));
        Assert.DoesNotContain(secret, diagnostic.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, diagnostic.ExceptionType ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsyncPropagatesCancellation()
    {
        using var source = new TestDirectory();
        source.CreatePet("pet", "id");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new PetCatalog().ScanAsync(source.RootPath, cancellation.Token));
    }

    [Fact]
    public async Task ScanAsyncPropagatesCancellationAtTheNextDirectoryLoopBoundary()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new StubPetCatalogFileSystem
        {
            DirectoryExists = _ => true,
            GetDirectories = _ => [first, second],
            FileExists = _ => false,
        };
        var catalog = new PetCatalog(_ => cancellation.Cancel(), fileSystem);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => catalog.ScanAsync(root, cancellation.Token));
        Assert.Equal(1, fileSystem.FileExistsCallCount);
    }

    [Fact]
    public async Task ScanAsyncPropagatesCancellationWhileManifestReadIsInProgress()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var petDirectory = Path.Combine(root, "pet");
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fileSystem = new StubPetCatalogFileSystem
        {
            DirectoryExists = _ => true,
            GetDirectories = _ => [petDirectory],
            FileExists = _ => true,
            ReadAllTextAsync = async (_, cancellationToken) =>
            {
                readStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return string.Empty;
            },
        };
        using var cancellation = new CancellationTokenSource();
        var scan = new PetCatalog(null, fileSystem).ScanAsync(root, cancellation.Token);

        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scan);
    }

    [Fact]
    public async Task ScanAsyncDiagnosesCandidateIoFailureAndContinuesWithValidSibling()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var broken = Path.Combine(root, "broken");
        var valid = Path.Combine(root, "valid");
        var diagnostics = new List<PetCatalogDiagnostic>();
        var fileSystem = new StubPetCatalogFileSystem
        {
            DirectoryExists = _ => true,
            GetDirectories = _ => [broken, valid],
            FileExists = _ => true,
            ReadAllTextAsync = (path, _) => path.StartsWith(broken, StringComparison.OrdinalIgnoreCase)
                ? Task.FromException<string>(new IOException("controlled read failure"))
                : Task.FromResult("""
                    {
                      "id": "valid-id",
                      "displayName": "Valid",
                      "spriteVersionNumber": 2,
                      "spritesheetPath": "spritesheet.webp"
                    }
                    """),
        };

        var candidates = await new PetCatalog(diagnostics.Add, fileSystem).ScanAsync(root, CancellationToken.None);

        Assert.Equal("valid-id", Assert.Single(candidates).Id);
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.DirectoryPath == broken && diagnostic.ExceptionType == nameof(IOException));
    }

    [Fact]
    public async Task ScanAsyncReturnsEmptyAndDiagnosticWhenRootEnumerationFails()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var diagnostics = new List<PetCatalogDiagnostic>();
        var fileSystem = new StubPetCatalogFileSystem
        {
            DirectoryExists = _ => true,
            GetDirectories = _ => throw new UnauthorizedAccessException("controlled enumeration failure"),
        };

        var candidates = await new PetCatalog(diagnostics.Add, fileSystem).ScanAsync(root, CancellationToken.None);

        Assert.Empty(candidates);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(root, diagnostic.DirectoryPath);
        Assert.Equal(nameof(UnauthorizedAccessException), diagnostic.ExceptionType);
    }

    [Fact]
    public async Task ScanAsyncLeavesEverySourceDirectoryAndFileUnchanged()
    {
        using var source = new TestDirectory();
        var petDirectory = source.CreatePet("pet", "id", "Pet");
        File.WriteAllText(Path.Combine(petDirectory, "validation.json"), "{ \"stale\": true }", Encoding.UTF8);
        File.WriteAllText(Path.Combine(petDirectory, "unrelated.txt"), "keep me", Encoding.UTF8);
        var before = source.Snapshot();

        var candidates = await new PetCatalog().ScanAsync(source.RootPath, CancellationToken.None);

        Assert.Single(candidates);
        Assert.Equal(before, source.Snapshot());
    }

    [Fact]
    public async Task DescriptorContainsNormalizedAbsolutePathsAndImmutableValues()
    {
        using var source = new TestDirectory();
        var petDirectory = source.CreatePet("pet", "id", "Pet", "images\\sprite.webp", description: "Description");

        var descriptor = Assert.Single(await new PetCatalog().ScanAsync(source.RootPath, CancellationToken.None));

        Assert.Equal("id", descriptor.Id);
        Assert.Equal("Pet", descriptor.DisplayName);
        Assert.Equal("Description", descriptor.Description);
        Assert.Equal(2, descriptor.SpriteVersionNumber);
        Assert.Equal(Path.GetFullPath(petDirectory), descriptor.PetDirectoryPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(petDirectory, "pet.json")), descriptor.ManifestPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(petDirectory, "images", "sprite.webp")), descriptor.SpritesheetPath);
    }

    private static PetDescriptor Descriptor(string id)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), id));
        return new PetDescriptor(id, id, null, 2, root, Path.Combine(root, "pet.json"), Path.Combine(root, "sprite.webp"));
    }

    private static void WriteManifest(string directory, string id, string displayName, string spritesheetPath)
    {
        var json = $$"""
            {
              "id": "{{id}}",
              "displayName": "{{displayName}}",
              "spriteVersionNumber": 2,
              "spritesheetPath": "{{spritesheetPath}}"
            }
            """;
        File.WriteAllText(Path.Combine(directory, "pet.json"), json, Encoding.UTF8);
    }

    private sealed class StubPetCatalogFileSystem : IPetCatalogFileSystem
    {
        public Func<string, bool> DirectoryExists { get; init; } = _ => false;

        public Func<string, string[]> GetDirectories { get; init; } = _ => [];

        public Func<string, bool> FileExists { get; init; } = _ => false;

        public Func<string, CancellationToken, Task<string>> ReadAllTextAsync { get; init; } =
            (_, _) => Task.FromResult(string.Empty);

        public int FileExistsCallCount { get; private set; }

        bool IPetCatalogFileSystem.DirectoryExists(string path) => DirectoryExists(path);

        string[] IPetCatalogFileSystem.GetDirectories(string path) => GetDirectories(path);

        bool IPetCatalogFileSystem.FileExists(string path)
        {
            FileExistsCallCount++;
            return FileExists(path);
        }

        Task<string> IPetCatalogFileSystem.ReadAllTextAsync(string path, CancellationToken cancellationToken)
        {
            return ReadAllTextAsync(path, cancellationToken);
        }
    }
}
