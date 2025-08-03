using System;

namespace MusicSearchDownload.Services;

public interface ITrackRouteProxy : IDisposable
{
    void Start();
    void Stop();
}
