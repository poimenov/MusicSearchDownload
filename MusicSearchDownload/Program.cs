using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MusicSearchDownload.Services;
using Photino.Blazor;

namespace MusicSearchDownload
{
    class Program
    {
        static ITrackRouteProxy trackRouteProxy;
        [STAThread]
        static void Main(string[] args)
        {
            var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);
            appBuilder.Services.AddLogging();

            // register root component and selector
            appBuilder.RootComponents.Add<App>("app");

            //add json configuration
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            appBuilder.Services.AddSingleton<IConfiguration>(configuration);

            appBuilder.Services.AddScoped(sp => new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false }, disposeHandler: true));
            appBuilder.Services.AddSingleton<ITrackRouteProxy, TrackRouteProxy>();
            appBuilder.Services.AddTransient<ISearchService, SearchService>();

            var app = appBuilder.Build();
            trackRouteProxy = app.Services.GetRequiredService<ITrackRouteProxy>();
            trackRouteProxy.Start();

            // customize window
            app.MainWindow
                .SetIconFile("favicon.ico")
                .SetTitle("Music search and download");
            app.MainWindow.RegisterWindowClosingHandler((sender, args) =>
            {
                if (trackRouteProxy != null)
                {
                    trackRouteProxy.Stop();
                    trackRouteProxy.Dispose();
                    trackRouteProxy = null;
                }
                return false; // allow the window to close
            });

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };

            app.Run();

        }
    }
}