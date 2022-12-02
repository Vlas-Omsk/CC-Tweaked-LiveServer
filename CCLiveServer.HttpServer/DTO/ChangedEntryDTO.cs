namespace CCLiveServer.HttpServer.DTO;

public sealed class ChangedEntryDTO
{
    public ChangeTypeDTO ChangeType { get; set; }
    public EntryTypeDTO EntryType { get; set; }
    public string Path { get; set; }
    public string OldPath { get; set; }
    public bool ContentChanged { get; set; }
}
