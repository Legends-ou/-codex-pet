using System.Security.Cryptography;
using System.Text.Json;
using PetDesktop.App.Imaging;
using PetDesktop.Core.Pets;
using Xunit.Sdk;

namespace PetDesktop.App.Tests;

public sealed class RealPetContractTests
{
    [Fact]
    public async Task DecodeAsyncReadsARealCodexPetWithoutChangingSourceFiles()
    {
        var explicitDirectory = Environment.GetEnvironmentVariable("PETDESKTOP_REAL_PET_DIR");
        var petDirectory = explicitDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "pets",
            "小宇");

        if (explicitDirectory is not null)
        {
            Assert.True(
                Directory.Exists(petDirectory),
                $"PETDESKTOP_REAL_PET_DIR does not exist: {petDirectory}");
        }
        else if (!Directory.Exists(petDirectory))
        {
            // The repository is portable; only the absent conventional developer fixture is optional.
            throw SkipException.ForSkip($"Optional real Codex pet fixture was not found: {petDirectory}");
        }

        var manifestPath = Path.Combine(petDirectory, "pet.json");
        var manifest = JsonSerializer.Deserialize<PetManifest>(
            await File.ReadAllTextAsync(manifestPath)) ?? throw new InvalidDataException("Real pet.json was empty.");
        var spritesheetPath = Path.GetFullPath(Path.Combine(petDirectory, manifest.SpritesheetPath));
        var before = Snapshot(manifestPath, spritesheetPath);

        var asset = await SkiaSpriteDecoder.DecodeAsync(
            spritesheetPath,
            manifest.SpriteVersionNumber,
            CancellationToken.None);

        Assert.Equal(1536, asset.AtlasWidth);
        Assert.Equal(9, asset.ActionFrames.Count);
        Assert.Equal(57, asset.ActionFrames.Values.Sum(frames => frames.Count));
        Assert.Equal(asset.Version == 2 ? 2288 : 1872, asset.AtlasHeight);
        Assert.Equal(asset.Version == 2 ? 16 : 0, asset.LookFrames.Count);
        Assert.Equal(asset.Version == 2 ? 73 : 57, asset.Frames.Count);
        Assert.Equal(before, Snapshot(manifestPath, spritesheetPath));
    }

    private static FileSnapshot[] Snapshot(params string[] paths) =>
        paths.Select(path =>
        {
            var info = new FileInfo(path);
            using var stream = File.OpenRead(path);
            return new FileSnapshot(
                path,
                info.Length,
                info.LastWriteTimeUtc,
                Convert.ToHexString(SHA256.HashData(stream)));
        }).ToArray();

    private sealed record FileSnapshot(string Path, long Length, DateTime LastWriteTimeUtc, string Sha256);
}
