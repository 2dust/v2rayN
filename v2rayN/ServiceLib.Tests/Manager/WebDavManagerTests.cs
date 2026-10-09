using System.Net.Http;
using System.Net.Http.Headers;
using WebDav;

namespace ServiceLib.Tests.Manager;

public class WebDavManagerTests
{
    [Test]
    public async Task CheckConnection_ReadOnlyAccount_CanRestoreWithoutMkcol()
    {
        var handler = new RecordingHandler(request => request.Method.Method switch
        {
            "GET" => Response(HttpStatusCode.OK, "backup"),
            "PUT" => Response(HttpStatusCode.Forbidden),
            _ => throw new InvalidOperationException($"Unexpected method: {request.Method}"),
        });
        var manager = CreateManager(handler);

        await (await manager.CheckConnection()).Should().BeEqualTo(WebDavCheckStatus.ReadOnly);
        await handler.Methods.SequenceEqual(["GET", "PUT"]).Should().BeTrue();
        await handler.Paths[0].EndsWith("/backup.zip", StringComparison.Ordinal).Should().BeTrue();
        await handler.Paths[1].EndsWith("/backup.zip", StringComparison.Ordinal).Should().BeFalse();

        var destination = Path.Combine(Path.GetTempPath(), $"v2rayN-webdav-test-{Guid.NewGuid():N}");
        try
        {
            await (await manager.GetRawFile(destination)).Should().BeTrue();
            await File.ReadAllText(destination).Should().BeEqualTo("backup");
            await handler.Methods.SequenceEqual(["GET", "PUT", "GET"]).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }

    [Test]
    public async Task CheckConnection_MissingBackup_CreatesDirectoryAndCleansUniqueTestFile()
    {
        var putCount = 0;
        var handler = new RecordingHandler(request => request.Method.Method switch
        {
            "GET" => Response(HttpStatusCode.NotFound),
            "PUT" => Response(++putCount == 1 ? HttpStatusCode.NotFound : HttpStatusCode.Created),
            "MKCOL" => Response(HttpStatusCode.Created),
            "DELETE" => Response(HttpStatusCode.NoContent),
            _ => throw new InvalidOperationException($"Unexpected method: {request.Method}"),
        });
        var manager = CreateManager(handler);

        await (await manager.CheckConnection()).Should().BeEqualTo(WebDavCheckStatus.BackupMissingWritable);
        await handler.Methods.SequenceEqual(["GET", "PUT", "MKCOL", "PUT", "DELETE"]).Should().BeTrue();
        var testPaths = handler.Paths.Where(path => path.Contains("v2rayN_check_", StringComparison.Ordinal)).ToArray();
        await testPaths.Length.Should().BeEqualTo(3);
        await testPaths.Distinct().Count().Should().BeEqualTo(1);
        await testPaths[0].Should().Contain("v2rayN_check_");
    }

    [Test]
    public async Task CheckConnection_Unauthorized_DoesNotProbeWrites()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.Unauthorized));
        var manager = CreateManager(handler);

        await (await manager.CheckConnection()).Should().BeEqualTo(WebDavCheckStatus.Unauthorized);
        await handler.Methods.SequenceEqual(["GET"]).Should().BeTrue();
    }

    [Test]
    public async Task CheckConnection_MaskedMissingBackupAndUnauthorizedWrite_ReportsAuthentication()
    {
        var handler = new RecordingHandler(request => request.Method.Method switch
        {
            "GET" => Response(HttpStatusCode.NotFound),
            "PUT" => Response(HttpStatusCode.Unauthorized),
            _ => throw new InvalidOperationException($"Unexpected method: {request.Method}"),
        });
        var manager = CreateManager(handler);

        await (await manager.CheckConnection()).Should().BeEqualTo(WebDavCheckStatus.Unauthorized);
        await handler.Methods.SequenceEqual(["GET", "PUT"]).Should().BeTrue();
    }

    [Test]
    public async Task CheckConnection_ForbiddenReadAndWrite_ReportsReadPermission()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.Forbidden));
        var manager = CreateManager(handler);

        await (await manager.CheckConnection()).Should().BeEqualTo(WebDavCheckStatus.ReadForbidden);
        await handler.Methods.SequenceEqual(["GET", "PUT"]).Should().BeTrue();
    }

    [Test]
    public async Task CheckConnection_DeleteFailure_IsReported()
    {
        var handler = new RecordingHandler(request => request.Method.Method switch
        {
            "GET" => Response(HttpStatusCode.OK, "backup"),
            "PUT" => Response(HttpStatusCode.Created),
            "DELETE" => Response(HttpStatusCode.Forbidden),
            _ => throw new InvalidOperationException($"Unexpected method: {request.Method}"),
        });
        var manager = CreateManager(handler);

        await (await manager.CheckConnection()).Should().BeEqualTo(WebDavCheckStatus.CleanupFailed);
        await handler.Methods.SequenceEqual(["GET", "PUT", "DELETE"]).Should().BeTrue();
    }

    [Test]
    public async Task CheckConnection_ServerError_DoesNotProbeWrites()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.InternalServerError));
        var manager = CreateManager(handler);

        await (await manager.CheckConnection()).Should().BeEqualTo(WebDavCheckStatus.Failed);
        await handler.Methods.SequenceEqual(["GET"]).Should().BeTrue();
    }

    [Test]
    public async Task CheckConnection_Timeout_DoesNotProbeWrites()
    {
        var handler = new RecordingHandler(_ => throw new TaskCanceledException("Connection timed out"));
        var manager = CreateManager(handler);

        await (await manager.CheckConnection()).Should().BeEqualTo(WebDavCheckStatus.Failed);
        await handler.Methods.SequenceEqual(["GET"]).Should().BeTrue();
    }

    private static WebDavManager CreateManager(RecordingHandler handler)
    {
        var config = new Config
        {
            WebDavItem = new WebDavItem
            {
                Url = "https://webdav.example/",
                UserName = "test-user",
                Password = "test-password",
            },
        };
        return new WebDavManager(config, parameters =>
        {
            var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = parameters.BaseAddress };
            return new WebDavClient(client);
        });
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string content = "")
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(content) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return response;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method.Method);
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(response(request));
        }
    }
}
