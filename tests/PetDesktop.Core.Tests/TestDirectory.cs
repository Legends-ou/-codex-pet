using System.Text;

namespace PetDesktop.Core.Tests;

internal sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "PetDesktop.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string CreatePet(
        string directoryName,
        string id,
        string? displayName = null,
        string spritesheetPath = "spritesheet.webp",
        bool createSpritesheet = true,
        string? description = null,
        int spriteVersionNumber = 2)
    {
        var petDirectory = Path.Combine(RootPath, directoryName);
        Directory.CreateDirectory(petDirectory);
        var displayNameProperty = displayName is null
            ? string.Empty
            : $",\n  \"displayName\": {System.Text.Json.JsonSerializer.Serialize(displayName)}";
        var descriptionProperty = description is null
            ? string.Empty
            : $",\n  \"description\": {System.Text.Json.JsonSerializer.Serialize(description)}";
        var json = $$"""
            {
              "id": {{System.Text.Json.JsonSerializer.Serialize(id)}},
              "spriteVersionNumber": {{spriteVersionNumber}},
              "spritesheetPath": {{System.Text.Json.JsonSerializer.Serialize(spritesheetPath)}}{{displayNameProperty}}{{descriptionProperty}}
            }
            """;
        File.WriteAllText(Path.Combine(petDirectory, "pet.json"), json, Encoding.UTF8);

        if (createSpritesheet)
        {
            var spritePath = Path.GetFullPath(Path.Combine(petDirectory, spritesheetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(spritePath)!);
            File.WriteAllBytes(spritePath, [1, 2, 3, 4]);
        }

        return petDirectory;
    }

    public IReadOnlyList<string> Snapshot()
    {
        return Directory
            .EnumerateFileSystemEntries(RootPath, "*", SearchOption.AllDirectories)
            .Select(path => Directory.Exists(path)
                ? $"D:{Path.GetRelativePath(RootPath, path)}"
                : $"F:{Path.GetRelativePath(RootPath, path)}:{Convert.ToBase64String(File.ReadAllBytes(path))}")
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
