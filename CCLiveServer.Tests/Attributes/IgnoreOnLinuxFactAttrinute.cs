namespace CCLiveServer.Tests;

public class IgnoreOnLinuxFactAttrinute : FactAttribute
{
    public IgnoreOnLinuxFactAttrinute()
    {
        if (OperatingSystem.IsLinux())
            Skip = "Ignored on linux";
    }
}
