using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using downloader.Utils;
using downloader.Utils.Songs;
using Downloader.Apis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Downloader
{
    public partial class MainWindow : Window
    {

        public static readonly HttpClient httpClient = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartDownload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                StartDownload();
            } catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // TODO log to ui, delete ./downloaded/ folder
            }
        }

        private async Task StartDownload() 
        { 

            string URL = DownloadURLBox.Text ?? "";

            bool validURL = Uri.TryCreate(URL, UriKind.Absolute, out Uri? uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (!validURL)
            {
                setStatusText("Invalid URL");
            } else
            { 
                if (
                    uriResult.Host.ToLower().Contains("spotify")
                )
                {
                    setStatusText("Starting");

                    await SpotifyApi.initDownloading();
                    Song[] songs;

                    setStatusText("Getting songs from spotify");

                    if (uriResult.AbsolutePath.StartsWith("/track"))
                    {
                        songs = await SpotifyApi.getSongsFromURLs([ URL ]);
                    } 
                    else if (uriResult.AbsolutePath.StartsWith("/album"))
                    {
                        songs = await SpotifyApi.getSongsInAlbum(URL);
                    }
                    else if (uriResult.AbsolutePath.StartsWith("/playlist"))
                    {
                        songs = await SpotifyApi.getSongsInPlaylist(URL);
                    } else
                    {
                        songs = [];
                    }

                    setStatusText("Starting search for matches");

                    List<YoutubeMusicSong> foundSongs = [];

                    foreach (Song song in songs)
                    {
                        setStatusText("Finding match for " + String.Join(", ", song.Artists) + " - " + song.Title);
                        var found = await YoutubeMusicApi.findSong(song);
                        if (found != null)
                        {
                            foundSongs.Add(found);
                        }
                    }

                    if (!await FFmpegApi.ensureFFmpegInstalled())
                    {
                        setStatusText("Downloading FFmpeg");
                        await FFmpegApi.downloadLatestFFmpeg();
                    }
                    if (!await YtDlpApi.ensureLatestYtDlpInstalled())
                    {
                        setStatusText("Downloading yt-dlp");
                        await YtDlpApi.downloadLatestYtDlp();
                    }

                    setStatusText("Downloading songs");

                    List<string> downloadedFilenames = [];
                    Directory.CreateDirectory("./downloaded");

                    foreach (YoutubeMusicSong song in foundSongs)
                    {
                        setStatusText("Downloading " + String.Join(", ", song.Artists) + " - " + song.Title);
                        downloadedFilenames.Add(await YtDlpApi.downloadSong(song, "./downloaded"));
                    }

                    setStatusText("Adding metadata and finishing");

                    List<string> newFilenames = [];
                    for (int j = 0; j < downloadedFilenames.Count; j++)
                    {
                        newFilenames.Add("./downloaded/" + String.Join(", ", foundSongs[j].Artists) + " - " + foundSongs[j].Title + "." + downloadedFilenames[j].Split(".").Last());
                        Regex.Replace(newFilenames[j], @"[\\\/:\*\?""<>\|\x00-\x1F]", "_");
                    }
                    for (int j = newFilenames.Count - 1; j >= 0; j--)
                    {
                        var dupe = new List<string>(newFilenames);
                        dupe.RemoveAt(j);
                        if (dupe.Contains(newFilenames[j]))
                        {
                            newFilenames[j] += " (2)";
                        }
                    }

                    int i = 0;
                    foreach (YoutubeMusicSong song in foundSongs)
                    {

                        setStatusText("Adding metadata to " + String.Join(", ", song.Artists) + " - " + song.Title);
                        FileUtils.applyID3ToFile(downloadedFilenames[i], song, song.youtubeSongUrl);

                        setStatusText("Renaming and moving " + String.Join(", ", song.Artists) + " - " + song.Title);
                        File.Move(downloadedFilenames[i], newFilenames[i]);

                        i++;
                    }

                    setStatusText("Writing playlist");

                    var playlist = "#EXTM3U";
                    foreach (var name in newFilenames)
                    {
                        playlist += "\n" + Path.GetFileName(name);
                    }

                    File.WriteAllText("./downloaded/! playlist.m3u8", playlist);

                    setStatusText("Done");

                    // parallel song processing, then tackle all quality of life

                } else
                {
                    setStatusText("Must be a Spotify URL");
                }
            }
            
        }

        public static void setStatusText(string text)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    ((MainWindow)desktop.MainWindow).StatusText.Text = text;
                }
            });
        }

    }
}