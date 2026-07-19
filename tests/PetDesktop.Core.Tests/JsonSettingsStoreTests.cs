using PetDesktop.Core.Configuration;

namespace PetDesktop.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PetDesktop.Core.Tests", Guid.NewGuid().ToString("N"));

    public JsonSettingsStoreTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task LoadAsyncCorruptJsonReturnsSafeDefaults()
    {
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{");

        var settings = await new JsonSettingsStore().LoadAsync(path, CancellationToken.None);

        Assert.Equal(AppSettings.Default, settings);
    }

    [Fact]
    public async Task SaveAsyncAtomicallyReplacesAndRoundTripsSettings()
    {
        var path = Path.Combine(_directory, "settings.json");
        var expected = new AppSettings(AppSettings.CurrentSchemaVersion, "xiaoyu", PetSize.Large, false, true, new("display-2", 0.25, 0.75), 100);
        var store = new JsonSettingsStore();

        await store.SaveAsync(path, expected, CancellationToken.None);

        Assert.Equal(expected, await store.LoadAsync(path, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task LoadAsyncNormalizesUnsupportedStoredValues()
    {
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, """
            {"schemaVersion":99,"selectedPetId":" ","size":99,"alwaysOnTop":false,"startWithWindows":true,"placement":{"displayId":"","relativeX":2,"relativeY":-1}}
            """);

        var settings = await new JsonSettingsStore().LoadAsync(path, CancellationToken.None);

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Null(settings.SelectedPetId);
        Assert.Equal(PetSize.Standard, settings.Size);
        Assert.False(settings.AlwaysOnTop);
        Assert.True(settings.StartWithWindows);
        Assert.Null(settings.Placement);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
