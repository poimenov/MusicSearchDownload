namespace MusicSearchDownload.Services;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WatsonWebserver;
using WatsonWebserver.Core;

public class TrackRouteProxy : ITrackRouteProxy
{
    private const string DOWNLOAD_URL = "https://m.z3.fm/download";
    private const string DEFAULT_HOST = "127.0.0.1";
    private const int DEFAULT_PORT = 9000;
    private bool disposed = false;
    private HttpClient _httpClient;
    private WebserverBase _server;

    public TrackRouteProxy(IConfiguration config)
    {
        _httpClient = new HttpClient();
        var port = config.GetValue("Port", DEFAULT_PORT);
        if (port <= 0 || port > 65535)
        {
            port = DEFAULT_PORT;
        }
        WebserverSettings settings = new WebserverSettings(DEFAULT_HOST, port);
        _server = new Webserver(settings, DefaultRoute);
        _server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.GET, "/track/{id}", GetBarRoute);
    }

    public void Start()
    {
        _server.Start();
    }

    public void Stop()
    {
        _server.Stop();
    }

    private async Task DefaultRoute(HttpContextBase ctx) =>
      await ctx.Response.Send("Hello from the default route!");
    private async Task GetBarRoute(HttpContextBase ctx)
    {
        var id = ctx.Request.Url.Parameters["id"];
        if (!string.IsNullOrEmpty(id) && int.TryParse(id, out int trackId))
        {
            var inputStream = await _httpClient.GetStreamAsync($"{DOWNLOAD_URL}/{trackId}");
            ctx.Response.StatusCode = 200;
            ctx.Response.ChunkedTransfer = true;
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                byte[] data = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);
                await ctx.Response.SendChunk(data, false);
            }
            await ctx.Response.SendChunk(Array.Empty<byte>(), true);
        }
        else
        {
            await ctx.Response.Send("Track ID not provided.");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }
        else
        {
            if (disposing)
            {
                _httpClient.Dispose();
                _server.Dispose();
            }

            disposed = true;
        }
    }
}
