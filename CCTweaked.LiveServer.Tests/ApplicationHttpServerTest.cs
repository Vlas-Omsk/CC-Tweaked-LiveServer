using System.Net;
using CCTweaked.LiveServer.HttpServer;

namespace CCTweaked.LiveServer.Tests;

public abstract class ApplicationHttpServerTest : IDisposable
{
    private readonly string _endPoint;
    private readonly ApplicationHttpServer _httpServer;
    private readonly HttpClient _httpClient;
    private readonly string _rootPath;
    private readonly string _targetPath;

    public abstract class DirectoryTest : ApplicationHttpServerTest
    {
        protected DirectoryTest(string endPoint, string rootPath, string targetPath) : base(endPoint, rootPath, targetPath)
        {
        }

        private void CreateBinaryFile(string path, byte[] data)
        {
            using var stream = File.Open(Path.Combine(_rootPath, _targetPath, path), FileMode.Create);
            stream.Write(data, 0, data.Length);
        }

        private void CreateLuaFile(string path, string data)
        {
            using var stream = new StreamWriter(File.Open(Path.Combine(_rootPath, _targetPath, path), FileMode.Create));
            stream.Write(data);
        }

        private bool IsFileExistst(string path)
        {
            return File.Exists(Path.Combine(_rootPath, _targetPath, path));
        }

        private string ReadAllTextFromFile(string path)
        {
            return File.ReadAllText(Path.Combine(_rootPath, _targetPath, path));
        }

        private byte[] ReadAllBytesFromFile(string path)
        {
            return File.ReadAllBytes(Path.Combine(_rootPath, _targetPath, path));
        }

        [Fact]
        // Getting existing binary file must return StatusCode 200 and return file data
        public async Task Test1()
        {
            var file = "testFile1";
            var data = new byte[] { 0xFF, 0xEE, 0xDD };

            CreateBinaryFile(file, data);

            var response = await GetAsync(file);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseData = await response.Content.ReadAsByteArrayAsync();

            Assert.Equal(data, responseData);
            Assert.Equal("application/octet-stream", response.Content.Headers.ContentType.MediaType);
        }

        [Fact]
        // Getting existing lua file must return StatusCode 200 and return file data
        public async Task Test2()
        {
            var file = "testFile2.lua";
            var data = "test\ntest";

            CreateLuaFile(file, data);

            var response = await GetAsync(file);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseData = await response.Content.ReadAsStringAsync();

            Assert.Equal(data, responseData);
            Assert.Equal("text/plain", response.Content.Headers.ContentType.MediaType);
        }

        [Fact]
        // Getting non existing binary file must return StatusCode 404
        public async Task Test3()
        {
            var response = await GetAsync("testFile3");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        // Getting non existing lua file must return StatusCode 404
        public async Task Test4()
        {
            var response = await GetAsync("testFile4.lua");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        // Putting existing binary file must return StatusCode 200 and rewrite file
        public async Task Test5()
        {
            var file = "testFile5";
            var data = new byte[] { 0xFF, 0xEE, 0xDD };

            CreateBinaryFile(file, new byte[] { 0xAA, 0xBB, 0xCC, 0xEE });

            var response = await PutAsync(file, new ByteArrayContent(data));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(IsFileExistst(file));
            Assert.Equal(data, ReadAllBytesFromFile(file));
        }

        [Fact]
        // Putting existing lua file must return StatusCode 200 and rewrite file
        public async Task Test6()
        {
            var file = "testFile6.lua";
            var data = "test\ntest";

            CreateLuaFile(file, data);

            var response = await PutAsync(file, new StringContent(data));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(IsFileExistst(file));
            Assert.Equal(data, ReadAllTextFromFile(file));
        }

        [Fact]
        // Putting non existing binary file must return StatusCode 200 and rewrite file
        public async Task Test7()
        {
            var file = "testFile7";
            var data = new byte[] { 0xFF, 0xEE, 0xDD };

            var response = await PutAsync(file, new ByteArrayContent(data));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(IsFileExistst(file));
            Assert.Equal(data, ReadAllBytesFromFile(file));
        }

        [Fact]
        // Putting non existing lua file must return StatusCode 200 and rewrite file
        public async Task Test8()
        {
            var file = "testFile8.lua";
            var data = "test\ntest";

            var response = await PutAsync(file, new StringContent(data));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(IsFileExistst(file));
            Assert.Equal(data, ReadAllTextFromFile(file));
        }

        [Fact]
        // Getting existing directory must return StatusCode 404
        public async Task Test9()
        {
            var path = "testDirectory9";

            CreateDirectory(path);

            var response = await GetAsync(path);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class RootDirectoryTest : DirectoryTest
    {
        public RootDirectoryTest() : base(NetUtils.GetUnusedUrl(), "ApplicationHttpServerTestRootDirectoryTest", "")
        {
        }
    }

    public class InnerDirectoryTest : DirectoryTest
    {
        public InnerDirectoryTest() : base(NetUtils.GetUnusedUrl(), "ApplicationHttpServerTestInnerDirectoryTest", "inner")
        {
        }
    }

    public class BuiltinFilesTest : ApplicationHttpServerTest
    {
        public BuiltinFilesTest() : base(NetUtils.GetUnusedUrl(), "ApplicationHttpServerTestBuiltinFilesTest", "")
        {
        }

        [Fact]
        // Getting existing lua builtin file must return StatusCode 200 and return file data
        public async Task Test1()
        {
            var file = "liveclient.lua";
            var data = File.ReadAllText(Path.Combine("lua", file));

            var response = await GetAsync(file);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseData = await response.Content.ReadAsStringAsync();

            Assert.Equal(data, responseData);
            Assert.Equal("text/plain", response.Content.Headers.ContentType.MediaType);
        }
    }

    protected ApplicationHttpServerTest(string endPoint, string rootPath, string targetPath)
    {
        _endPoint = endPoint;
        _rootPath = rootPath;
        _targetPath = targetPath;

        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, true);

        CreateDirectory(_targetPath);

        _httpServer = new ApplicationHttpServer(_endPoint, _rootPath);
        _httpServer.Start();

        _httpClient = new HttpClient();
    }

    protected void CreateDirectory(string path)
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, _targetPath, path));
    }

    protected Task<HttpResponseMessage> GetAsync(string url)
    {
        return _httpClient.SendAsync(new HttpRequestMessage()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(new Uri(_endPoint), Path.Combine(_targetPath, url))
        });
    }

    protected Task<HttpResponseMessage> PutAsync(string url, HttpContent content)
    {
        return _httpClient.SendAsync(new HttpRequestMessage()
        {
            Method = HttpMethod.Put,
            RequestUri = new Uri(new Uri(_endPoint), Path.Combine(_targetPath, url)),
            Content = content
        });
    }

    public void Dispose()
    {
        _httpServer.Stop();
        _httpClient.Dispose();
    }
}
