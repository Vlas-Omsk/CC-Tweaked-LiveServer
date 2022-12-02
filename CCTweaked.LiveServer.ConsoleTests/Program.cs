using CCTweaked.LiveServer.Core;

namespace CCTweaked.LiveServer.ConsoleTests;

internal static class Program
{
    private static void Main(string[] args)
    {
        Directory.CreateDirectory("TestDirectory");

        var watcher = new DirectoryWatcher("TestDirectory");
        watcher.Changed += (sender, e) =>
        {
            Console.WriteLine($"{e.OldPath} {e.Path} {e.ChangeType} {e.EntryType}");
        };

        while (true)
        {
            Console.ReadLine();
        }
    }
}