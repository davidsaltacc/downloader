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

        public static readonly HttpClient HttpClient = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartDownload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                Task.Run(() => StartDownload());
            } catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // TODO log to ui, delete ./downloaded/ folder
            }
        }

        private async Task StartDownload() 
        { 

            var url = DownloadURLBox.Text ?? "";

            var validUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (!validUrl)
            {
                SetStatusText("Invalid URL");
            } else
            { 
                if (
                    uriResult?.Host.Contains("spotify", StringComparison.InvariantCultureIgnoreCase) ?? false
                )
                {
                    SetStatusText("Starting");

                    await SpotifyApi.InitDownloading();
                    Song[] songs;

                    SetStatusText("Getting songs from spotify");

                    if (uriResult.AbsolutePath.StartsWith("/track"))
                    {
                        songs = await SpotifyApi.GetSongsFromURLs([ url ]);
                    } 
                    else if (uriResult.AbsolutePath.StartsWith("/album"))
                    {
                        songs = await SpotifyApi.GetSongsInAlbum(url);
                    }
                    else if (uriResult.AbsolutePath.StartsWith("/playlist"))
                    {
                        songs = await SpotifyApi.GetSongsInPlaylist(url);
                    } else
                    {
                        songs = [];
                    }

                    SetStatusText("Starting search for matches");

                    List<YoutubeMusicSong> foundSongs = [];

                    foreach (var song in songs)
                    {
                        SetStatusText("Finding match for " + String.Join(", ", song.Artists) + " - " + song.Title);
                        var found = await YoutubeMusicApi.FindSong(song);
                        if (found != null)
                        {
                            foundSongs.Add(found);
                        }
                    }

                    if (!await FFmpegApi.EnsureFFmpegInstalled())
                    {
                        SetStatusText("Downloading FFmpeg");
                        await FFmpegApi.DownloadLatestFFmpeg();
                    }
                    if (!await YtDlpApi.EnsureLatestYtDlpInstalled())
                    {
                        SetStatusText("Downloading yt-dlp");
                        await YtDlpApi.DownloadLatestYtDlp();
                    }

                    SetStatusText("Downloading songs");

                    List<string> downloadedFilenames = [];
                    Directory.CreateDirectory("./downloaded");

                    foreach (var song in foundSongs)
                    {
                        SetStatusText("Downloading " + String.Join(", ", song.Artists) + " - " + song.Title);
                        downloadedFilenames.Add(await YtDlpApi.DownloadSong(song, "./downloaded"));
                    }

                    SetStatusText("Adding metadata and finishing");

                    List<string> newFilenames = [];
                    for (var j = 0; j < downloadedFilenames.Count; j++)
                    {
                        newFilenames.Add("./downloaded/" + String.Join(", ", foundSongs[j].Artists) + " - " + foundSongs[j].Title + "." + downloadedFilenames[j].Split(".").Last());
                        Regex.Replace(newFilenames[j], @"[\\\/:\*\?""<>\|\x00-\x1F]", "_");
                    }
                    for (var j = newFilenames.Count - 1; j >= 0; j--)
                    {
                        var dupe = new List<string>(newFilenames);
                        dupe.RemoveAt(j);
                        if (dupe.Contains(newFilenames[j]))
                        {
                            newFilenames[j] += " (2)";
                        }
                    }

                    var i = 0;
                    foreach (var song in foundSongs)
                    {

                        SetStatusText("Adding metadata to " + String.Join(", ", song.Artists) + " - " + song.Title);
                        FileUtils.ApplyId3ToFile(downloadedFilenames[i], song, song.YoutubeSongUrl);

                        SetStatusText("Renaming and moving " + String.Join(", ", song.Artists) + " - " + song.Title);
                        File.Move(downloadedFilenames[i], newFilenames[i]);

                        i++;
                    }

                    SetStatusText("Writing playlist");

                    var playlist = "#EXTM3U";
                    foreach (var name in newFilenames)
                    {
                        playlist += "\n" + Path.GetFileName(name);
                    }

                    await File.WriteAllTextAsync("./downloaded/! playlist.m3u8", playlist);

                    SetStatusText("Done");

                    // parallel song processing, then tackle all quality of life

                } else
                {
                    SetStatusText("Must be a Spotify URL");
                }
            }
            
        }

        public static void SetStatusText(string text)
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