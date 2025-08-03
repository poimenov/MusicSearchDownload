using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MusicSearchDownload.Models;

namespace MusicSearchDownload.Services;

public class SearchService : ISearchService
{
    private const string BASE_URL = "https://m.z3.fm";
    private readonly string _downloadPath;
    private readonly HttpClient _httpClient;
    public SearchService(IConfiguration config, HttpClient httpClient)
    {
        var defaultDownloadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        _downloadPath = config.GetValue("DownloadPath", defaultDownloadPath);
        if (string.IsNullOrWhiteSpace(_downloadPath) || !Directory.Exists(_downloadPath))
        {
            _ = defaultDownloadPath;
        }
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BASE_URL);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");
    }

    public async Task Download(string downloadUrl)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new ArgumentException("Download URL cannot be null or empty.", nameof(downloadUrl));
        }

        if (!Uri.IsWellFormedUriString(downloadUrl, UriKind.Absolute))
        {
            throw new ArgumentException("Invalid download URL format.", nameof(downloadUrl));
        }

        var uri = new Uri(new Uri(BASE_URL), downloadUrl);
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to download file. Status code: {response.StatusCode}");
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("Could not determine file name from the download URL.");
        }

        var filePath = Path.Combine(_downloadPath, fileName);
        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await response.Content.CopyToAsync(fileStream);
        }
    }

    public async Task<string> DownloadUrl(int trackId)
    {
        var uri = new Uri(new Uri(BASE_URL), $"/download/{trackId}");
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await _httpClient.SendAsync(request);
        if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400 && response.Headers.Location != null)
        {
            return response.Headers.Location.ToString();
        }
        else
        {
            return uri.AbsoluteUri;
        }
    }

    public async Task<IEnumerable<Track>> Search(string keyword) =>
        await _httpClient.GetFromJsonAsync<IEnumerable<Track>>($"mp3/search?keywords={keyword}", default);

}
