using System.Buffers;
using System.Reflection;
using System.Text;
using CCTweaked.LiveServer.Core;
using CCTweaked.LiveServer.Core.IO;
using CCTweaked.LiveServer.HttpServer.WebSocketServices;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace CCTweaked.LiveServer.HttpServer;

public sealed class ApplicationHttpServer : WebSocketSharp.Server.HttpServer
{
    private readonly string _rootDirectory;
    private readonly static string _luaDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua");

    public ApplicationHttpServer(string url, string rootDirectory) : base(url)
    {
        _rootDirectory = rootDirectory;

        WebSocketServices.AddService<RootWebSocketService>("/", () => new RootWebSocketService(new DirectoryWatcher(rootDirectory)));
    }

    protected override void OnRequest(HttpRequestEventArgs e)
    {
        var path = e.Request.Url.AbsolutePath[1..];

        if (e.Request.HttpMethod == "GET")
        {
            if (
                TryFindFullPath(_rootDirectory, path, out var fullPath) ||
                TryFindFullPath(_luaDirectory, path, out fullPath)
            )
            {
                SendFile(fullPath, e.Response);
                return;
            }
        }
        else if (e.Request.HttpMethod == "PUT")
        {
            using (var stream = File.Open(Path.Combine(_rootDirectory, path), FileMode.Create))
                e.Request.InputStream.CopyTo(stream);

            e.Response.StatusCode = 200;
            return;
        }

        e.Response.StatusCode = 404;
    }

    private bool TryFindFullPath(string rootPath, string name, out string fullPath)
    {
        var directory = Path.Combine(rootPath, Path.GetDirectoryName(name));
        name = Path.GetFileName(name);

        fullPath = Path.Combine(directory, name);

        if (File.Exists(fullPath))
            return true;

        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (Path.GetFileNameWithoutExtension(file) == name)
                {
                    fullPath = Path.Combine(directory, Path.GetFileName(file));
                    return true;
                }
            }
        }

        fullPath = null;
        return false;
    }

    private void SendFile(string path, HttpListenerResponse response)
    {
        if (FileUtils.IsTextFile(path))
        {
            int length;
            var chars = ArrayPool<char>.Shared.Rent(81920);
            var encoding = new UTF8Encoding(false, false);

            var inputStream = new StreamReader(path);
            var outputProxyStream = new ProxyStream(null);
            var outputStream = new StreamWriter(outputProxyStream, encoding, -1, true);

            while ((length = inputStream.Read(chars, 0, chars.Length)) != 0)
            {
                outputStream.Write(chars, 0, length);
            }

            inputStream.Dispose();
            outputStream.Dispose();
            outputProxyStream.Dispose();

            response.ContentLength64 = outputProxyStream.Length;

            inputStream = new StreamReader(path);
            outputStream = new StreamWriter(response.OutputStream, encoding, -1, true);
            response.ContentEncoding = encoding;
            response.ContentType = "text/plain";

            while ((length = inputStream.Read(chars, 0, chars.Length)) != 0)
            {
                outputStream.Write(chars, 0, length);
            }

            ArrayPool<char>.Shared.Return(chars);

            inputStream.Dispose();
            outputStream.Dispose();
        }
        else
        {
            using var inputStream = File.OpenRead(path);

            response.ContentLength64 = inputStream.Length;
            response.ContentType = "application/octet-stream";
            inputStream.CopyTo(response.OutputStream);
        }

        response.StatusCode = 200;
        response.SendChunked = true;
        response.Close();
    }
}
