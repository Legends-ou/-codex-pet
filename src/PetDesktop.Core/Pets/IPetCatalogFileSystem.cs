namespace PetDesktop.Core.Pets;

internal interface IPetCatalogFileSystem
{
    bool DirectoryExists(string path);

    string[] GetDirectories(string path);

    bool FileExists(string path);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
}

internal sealed class PhysicalPetCatalogFileSystem : IPetCatalogFileSystem
{
    public static PhysicalPetCatalogFileSystem Instance { get; } = new();

    private PhysicalPetCatalogFileSystem()
    {
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string[] GetDirectories(string path) =>
        Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);

    public bool FileExists(string path) => File.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);
}
