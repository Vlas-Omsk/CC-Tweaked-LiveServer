namespace CCLiveServer.Core;

public sealed class DirectoryEntry
{
    public DirectoryEntry(string path, string fullPath, DirectoryEntryType entryType)
    {
        Path = path;
        FullPath = fullPath;
        EntryType = entryType;
    }

    public string Path { get; }
    public string FullPath { get; }
    public DirectoryEntryType EntryType { get; }
}
