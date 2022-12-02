using CCTweaked.LiveServer.Core;
using CCTweaked.LiveServer.HttpServer.DTO;

namespace CCTweaked.LiveServer.HttpServer;

public static class MapperUtils
{
    public static EntryTypeDTO Map(DirectoryEntryType value)
    {
        return value switch
        {
            DirectoryEntryType.Directory => EntryTypeDTO.Directory,
            DirectoryEntryType.File => EntryTypeDTO.File,
            _ => throw new NotSupportedException()
        };
    }

    public static ChangeTypeDTO Map(DirectoryChangeType value)
    {
        return value switch
        {
            DirectoryChangeType.Changed => ChangeTypeDTO.Changed,
            DirectoryChangeType.Created => ChangeTypeDTO.Created,
            DirectoryChangeType.Deleted => ChangeTypeDTO.Deleted,
            DirectoryChangeType.Moved => ChangeTypeDTO.Moved,
            _ => throw new NotSupportedException()
        };
    }
}
