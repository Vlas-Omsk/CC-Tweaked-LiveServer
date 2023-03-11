using System.Buffers;
using System.Text;
using CCTweaked.LiveServer.Core.IO;
using Microsoft.Extensions.Logging;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace CCTweaked.LiveServer.HttpServer;

public sealed class ApplicationHttpServer : WebSocketSharp.Server.HttpServer
{
    private readonly string _rootDirectory;
    private readonly string _luaDirectory;
    private readonly ILogger _logger;

    public ApplicationHttpServer(
        string url,
        string rootDirectory,
        string luaDirectory,
        ILogger<ApplicationHttpServer> logger
    ) : base(url)
    {
        _rootDirectory = rootDirectory;
        _luaDirectory = luaDirectory;
        _logger = logger;
    }

    protected override void OnRequest(HttpRequestEventArgs e)
    {
        var path = e.Request.Url.AbsolutePath[1..];
        var success = false;

        if (e.Request.HttpMethod == "GET")
        {
            if (
                TryFindFullPath(_rootDirectory, path, out var fullPath) ||
                TryFindFullPath(_luaDirectory, path, out fullPath)
            )
            {
                SendFile(fullPath, e.Response);
                success = true;
            }
        }
        else if (e.Request.HttpMethod == "POST")
        {
            using (var stream = File.Open(Path.Combine(_rootDirectory, path), FileMode.Create))
                e.Request.InputStream.CopyTo(stream);

            e.Response.StatusCode = 200;
            success = true;
        }

        if (!success)
            e.Response.StatusCode = 404;

        _logger.LogInformation($"[{e.Request.HttpMethod}, {e.Request.RemoteEndPoint}] {e.Request.Url.AbsolutePath} ({e.Response.StatusCode})");
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
