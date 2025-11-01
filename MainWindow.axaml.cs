using Avalonia.Controls;
using downloader.Utils.Songs;
using Downloader.Apis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                    } else if (uriResult.AbsolutePath.StartsWith("/album"))
                    {
                        songs = await SpotifyApi.getSongsInAlbum(URL);
                        Debug.WriteLine(String.Join(", ", songs.Select(song => song.Title)));
                    }
                    else if (uriResult.AbsolutePath.StartsWith("/playlist"))
                    {
                        try
                        {
                            songs = await SpotifyApi.getSongsInPlaylist(URL);
                            Debug.WriteLine(String.Join(", ", songs.Select(song => song.Title)));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }
                    }

                    // TODO convert all SpotifySongs to YoutubeSongs and download those using yt-dlp (convert using ffmpeg if installed (add option to download & install?))
                    // fancy ui and settings and allthat idk

                } else
                {
                    StatusText.Text = "Must be a Spotify URL";
                }
            }
            
        }
    }
}