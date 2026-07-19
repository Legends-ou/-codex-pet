using System.Text;
using System.IO;

namespace PetDesktop.App.Platform;

internal static class LocalDiagnostics
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PetDesktop",
        "pet-desktop.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(
                LogPath,
                $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Diagnostics must never interrupt the pet process.
        }
    }

    public static void WritePetPackageIssue(string directory, string reason)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(
                LogPath,
                $"{DateTimeOffset.Now:O} Pet package skipped: {reason} ({directory}){Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Diagnostics are best-effort and must never interrupt pet scanning.
        }
    }
}
