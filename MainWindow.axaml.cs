using Avalonia.Controls;
using downloader.Utils.Songs;
using Downloader.Apis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

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
            }
        }

        private async void StartDownload() 
        { 

            string URL = DownloadURLBox.Text ?? "";

            bool validURL = Uri.TryCreate(URL, UriKind.Absolute, out Uri? uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (!validURL)
            {
                StatusText.Text = "Invalid URL";
            } else
            { 
                if (
                    uriResult.Host.ToLower().Contains("spotify")
                )
                {
                    StatusText.Text = "Starting";

                    await SpotifyApi.initDownloading();
                    Song[] songs;

                    StatusText.Text = "Getting songs from spotify";

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

                    StatusText.Text = "Starting search for matches";

                    List<YoutubeMusicSong> foundSongs = [];

                    foreach (Song song in songs)
                    {
                        StatusText.Text = "Finding match for " + String.Join(", ", song.Artists) + " - " + song.Title;
                        var found = await YoutubeMusicApi.findSong(song);
                        if (found != null)
                        {
                            foundSongs.Add(found);
                        }
                    }

                    if (!FFmpegApi.ensureFFmpegInstalled())
                    {
                        StatusText.Text = "Downloading FFmpeg";
                        await FFmpegApi.downloadLatestFFmpeg();
                    }
                    if (!await YtDlpApi.ensureLatestYtDlpInstalled())
                    {
                        StatusText.Text = "Downloading yt-dlp";
                        await YtDlpApi.downloadLatestYtDlp();
                    }

                    StatusText.Text = "Downloading songs";

                    List<string> downloadedFilenames = [];
                    Directory.CreateDirectory("./downloaded");

                    foreach (YoutubeMusicSong song in foundSongs)
                    {
                        StatusText.Text = "Downloading " + String.Join(", ", song.Artists) + " - " + song.Title;
                        downloadedFilenames.Add(await YtDlpApi.downloadSong(song, "./downloaded"));
                    }

                    StatusText.Text = "Done with something idk";

                    // TODO rename and add metadata, create m3u8 playlist, final touches etc

                } else
                {
                    StatusText.Text = "Must be a Spotify URL";
                }
            }
            
        }
    }
}