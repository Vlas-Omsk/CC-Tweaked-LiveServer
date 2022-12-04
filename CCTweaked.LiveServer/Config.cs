using System.Reflection;
using ProcessArguments;

namespace CCTweaked.LiveServer;

internal sealed class Config
{
    [Required]
    public string Url { get; set; }
    [Required]
    public string RootDirectory { get; set; }
    public string LuaDirectory { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua");
}
