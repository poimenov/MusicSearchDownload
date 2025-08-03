using System.Collections.Generic;
using System.Threading.Tasks;
using MusicSearchDownload.Models;

namespace MusicSearchDownload.Services;

public interface ISearchService
{
    Task<IEnumerable<Track>> Search(string keyword);
    Task Download(string downloadUrl);
    Task<string> DownloadUrl(int trackId);
}
