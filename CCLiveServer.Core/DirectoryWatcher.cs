using System.Security.Cryptography;

namespace CCLiveServer.Core;

public sealed class DirectoryWatcher : IDisposable
{
    private static readonly MD5 _md5 = MD5.Create();

    private readonly string _path;
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

    private sealed class Entry
    {
        public DirectoryEntryType EntryType { get; set; }
        public byte[] Hash { get; set; }
    }

    public DirectoryWatcher(string path)
    {
        _path = Path.GetFullPath(path);
        _entries = EnumerateEntries(_path).ToDictionary(x => x.Key, c => new Entry() { EntryType = c.Value });
        _watcher = new FileSystemWatcher(_path);
        _watcher.Changed += OnWatcherChanged;
        _watcher.Created += OnWatcherChanged;
        _watcher.Deleted += OnWatcherChanged;
        _watcher.Renamed += OnWatcherRenamed;
        _watcher.IncludeSubdirectories = true;
        _watcher.EnableRaisingEvents = true;
    }

    ~DirectoryWatcher()
    {
        Dispose();
    }

    public DirectoryEntry[] GetEntries()
    {
        return _entries
            .Select(x => new DirectoryEntry(Path.GetRelativePath(_path, x.Key), x.Key, x.Value.EntryType))
            .ToArray();
    }

    public void ReloadAll()
    {
        foreach (var entry in EnumerateEntries(_path).Where(x => x.Value == DirectoryEntryType.File))
            RaiseChanged(null, entry.Key, DirectoryChangeType.Changed, DirectoryEntryType.File);
    }

    private IEnumerable<KeyValuePair<string, DirectoryEntryType>> EnumerateEntries(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path))
            yield return new KeyValuePair<string, DirectoryEntryType>(file, DirectoryEntryType.File);

        foreach (var directory in Directory.EnumerateDirectories(path))
        {
            yield return new KeyValuePair<string, DirectoryEntryType>(directory, DirectoryEntryType.Directory);

            foreach (var entry in EnumerateEntries(directory))
                yield return entry;
        }
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Renamed)
            throw new InvalidOperationException();

        HandleEvent(e.OldFullPath, e.FullPath, DirectoryChangeType.Moved);
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        var changeType = e.ChangeType switch
        {
            WatcherChangeTypes.Changed => DirectoryChangeType.Changed,
            WatcherChangeTypes.Created => DirectoryChangeType.Created,
            WatcherChangeTypes.Deleted => DirectoryChangeType.Deleted,
            _ => throw new InvalidOperationException()
        };

        HandleEvent(null, e.FullPath, changeType);
    }

    private void HandleEvent(string oldFullPath, string fullPath, DirectoryChangeType changeType)
    {
        DirectoryEntryType entryType;

        if (File.Exists(fullPath))
            entryType = DirectoryEntryType.File;
        else if (Directory.Exists(fullPath))
            entryType = DirectoryEntryType.Directory;
        else
            return;

        switch (changeType)
        {
            case DirectoryChangeType.Changed:
                {
                    if (entryType == DirectoryEntryType.File)
                    {
                        var fileHash = GetHashForFile(fullPath);

                        if (_entries.TryGetValue(fullPath, out var entry) && entry.Hash != null && entry.Hash.SequenceEqual(fileHash))
                            return;

                        _entries[fullPath].Hash = fileHash;
                    }

                    break;
                }
            case DirectoryChangeType.Created:
                {
                    if (_entries.TryGetValue(fullPath, out Entry entry))
                    {
                        RaiseChanged(fullPath, null, DirectoryChangeType.Deleted, entry.EntryType);

                        entry.EntryType = entryType;
                    }
                    else
                    {
                        _entries[fullPath] = entry = new Entry() { EntryType = entryType };
                    }

                    if (entryType == DirectoryEntryType.File)
                        entry.Hash = GetHashForFile(fullPath);
                    else
                        entry.Hash = null;

                    break;
                }
            case DirectoryChangeType.Deleted:
                {
                    if (!_entries.Remove(fullPath))
                        return;

                    break;
                }
            case DirectoryChangeType.Moved:
                {
                    _entries.Remove(oldFullPath, out var oldEntry);

                    if (_entries.Remove(fullPath, out Entry entry))
                        RaiseChanged(fullPath, null, DirectoryChangeType.Deleted, entry.EntryType);

                    _entries[fullPath] = oldEntry;

                    break;
                }
        }

        RaiseChanged(fullPath, oldFullPath, changeType, entryType);
    }

    private static byte[] GetHashForFile(string path)
    {
        using var stream = File.OpenRead(path);

        return _md5.ComputeHash(stream);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _watcher.Dispose();
    }

    private void RaiseChanged(string fullPath, string oldFullPath, DirectoryChangeType changeType, DirectoryEntryType entryType)
    {
        var path = Path.GetRelativePath(_path, fullPath);
        string oldPath = null;

        if (oldFullPath != null)
            oldPath = Path.GetRelativePath(_path, oldFullPath);

        Changed?.Invoke(this, new ChangedEventArgs(path, fullPath, oldPath, oldFullPath, changeType, entryType));
    }

    public event EventHandler<ChangedEventArgs> Changed;
}

public sealed class ChangedEventArgs : EventArgs
{
    public ChangedEventArgs(string path, string fullPath, string oldPath, string oldFullPath, DirectoryChangeType changeType, DirectoryEntryType entryType)
    {
        Path = path;
        FullPath = fullPath;
        OldPath = oldPath;
        OldFullPath = oldFullPath;
        ChangeType = changeType;
        EntryType = entryType;
    }

    public string Path { get; }
    public string FullPath { get; }
    public string OldPath { get; }
    public string OldFullPath { get; }
    public DirectoryChangeType ChangeType { get; }
    public DirectoryEntryType EntryType { get; }
}
