using System.IO;
using ThreadingTimer = System.Threading.Timer;

namespace PetDesktop.App.Pets;

/// <summary>
/// Collapses the burst of file-system notifications emitted while a pet package is copied or replaced.
/// </summary>
public sealed class PetDirectoryWatcher : IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);
    private readonly FileSystemWatcher _watcher;
    private readonly ThreadingTimer _debounceTimer;
    private readonly Action _reloadRequested;
    private bool _disposed;

    public PetDirectoryWatcher(string rootDirectory, Action reloadRequested)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(reloadRequested);

        _reloadRequested = reloadRequested;
        _debounceTimer = new ThreadingTimer(OnDebounceElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _watcher = new FileSystemWatcher(rootDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnFileSystemChanged;
        _watcher.Created += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemRenamed;
        _watcher.Error += OnWatcherError;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileSystemChanged;
        _watcher.Created -= OnFileSystemChanged;
        _watcher.Deleted -= OnFileSystemChanged;
        _watcher.Renamed -= OnFileSystemRenamed;
        _watcher.Error -= OnWatcherError;
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs eventArgs) => ScheduleReload();

    private void OnFileSystemRenamed(object sender, RenamedEventArgs eventArgs) => ScheduleReload();

    private void OnWatcherError(object sender, ErrorEventArgs eventArgs) => ScheduleReload();

    private void ScheduleReload()
    {
        if (!_disposed)
        {
            _debounceTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        if (!_disposed)
        {
            _reloadRequested();
        }
    }
}
