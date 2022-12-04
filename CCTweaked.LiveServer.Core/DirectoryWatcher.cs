using System.Security.Cryptography;

namespace CCTweaked.LiveServer.Core;

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

    public IEnumerable<DirectoryEntry> GetEntries()
    {
        UpdateAll();

        return _entries
            .Select(x => new DirectoryEntry(Path.GetRelativePath(_path, x.Key), x.Key, x.Value.EntryType))
            .ToArray();
    }

    public void ReloadAll()
    {
        UpdateAll();

        foreach (var entry in _entries.Where(x => x.Value.EntryType == DirectoryEntryType.File))
            RaiseChanged(null, entry.Key, DirectoryChangeType.Changed, DirectoryEntryType.File);
    }

    public void UpdateAll()
    {
        foreach (var actualEntry in EnumerateEntries(_path))
        {
            if (_entries.TryGetValue(actualEntry.Key, out var entry))
            {
                if (entry.EntryType != actualEntry.Value)
                {
                    RaiseChanged(actualEntry.Key, null, DirectoryChangeType.Deleted, entry.EntryType);

                    if (actualEntry.Value == DirectoryEntryType.Directory)
                    {
                        entry.EntryType = actualEntry.Value;
                        entry.Hash = null;
                    }
                    else
                    {
                        entry.EntryType = actualEntry.Value;
                        entry.Hash = GetHashForFile(actualEntry.Key);
                    }

                    RaiseChanged(actualEntry.Key, null, DirectoryChangeType.Created, entry.EntryType);
                }
            }
            else
            {
                _entries.Add(actualEntry.Key, entry = new Entry()
                {
                    EntryType = actualEntry.Value
                });

                if (actualEntry.Value == DirectoryEntryType.File)
                    entry.Hash = GetHashForFile(actualEntry.Key);

                RaiseChanged(actualEntry.Key, null, DirectoryChangeType.Created, actualEntry.Value);
            }
        }

        foreach (var entry in _entries)
        {
            if ((entry.Value.EntryType == DirectoryEntryType.File && !File.Exists(entry.Key)) ||
                (entry.Value.EntryType == DirectoryEntryType.Directory && !Directory.Exists(entry.Key)))
            {
                _entries.Remove(entry.Key);

                RaiseChanged(entry.Key, null, DirectoryChangeType.Deleted, entry.Value.EntryType);
            }
        }
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
