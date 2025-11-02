using Avalonia.Controls;
using downloader.Utils.Songs;
using Downloader.Apis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private async void StartDownload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

                    List<YoutubeMusicSong> foundSongs = [];

                    foreach (Song song in songs)
                    {
                        var found = await YoutubeMusicApi.findSong(song);
                        if (found != null) { 
                            foundSongs.Add(found);
                        }
                    }

                    // TODO songs are there, now download with yt-dlp (download yt-dlp first? idk, need to implement the YtDlpApi class anyway)

                } else
                {
                    StatusText.Text = "Must be a Spotify URL";
                }
            }
            
        }
    }
}