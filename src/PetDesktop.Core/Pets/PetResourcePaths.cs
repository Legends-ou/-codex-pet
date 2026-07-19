namespace PetDesktop.Core.Pets;

public static class PetResourcePaths
{
    public const string DefaultDirectoryName = "pets";

    public static string GetInstalledPetsRoot(string applicationBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        return Path.GetFullPath(Path.Combine(applicationBaseDirectory, DefaultDirectoryName));
    }
}
