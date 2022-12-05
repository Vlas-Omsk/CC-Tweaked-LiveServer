using System.Reflection;
using ProcessArguments;

namespace CCTweaked.LiveServer;

internal sealed class Config
{
    public string Url { get; set; } = "http://0.0.0.0:1234";
    public string RootDirectory { get; set; } = ".";
    public string LuaDirectory { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua");
}
