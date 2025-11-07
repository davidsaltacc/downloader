using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using downloader.Utils;
using downloader.Utils.Songs;
using Downloader.Apis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public partial class MainWindow : Window
    {

        public static readonly HttpClient HttpClient = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void StartDownload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                Task.Run(async () => await StartDownload());
            } catch (Exception ex)
            {
                Debug.WriteLine(ex);
                setStatusText("Error occurred");
                Directory.Delete("./downloaded", true);
            }
        }

        public static readonly StyledProperty<bool> IsBusyProperty = AvaloniaProperty.Register<MainWindow, bool>(nameof(IsBusy));
        public bool IsBusy
        {
            get => GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        private async Task StartDownload() 
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = true;
            });

            var url = DownloadURLBox.Text ?? "";

            var validUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (!validUrl)
            {
                setStatusText("Invalid URL");
            } else
            { 
                if (
                    uriResult?.Host.ToLower().Contains("spotify") ?? false
                )
                {
                    setStatusText("Starting");

                    if (!await FFmpegApi.EnsureFFmpegInstalled())
                    {
                        setStatusText("Downloading FFmpeg");
                        await FFmpegApi.DownloadLatestFFmpeg();
                    }
                    if (!await YtDlpApi.EnsureLatestYtDlpInstalled())
                    {
                        setStatusText("Downloading yt-dlp");
                        await YtDlpApi.DownloadLatestYtDlp();
                    }

                    await SpotifyApi.InitDownloading();
                    Song[] songs;

                    setStatusText("Getting songs from spotify");

                    if (uriResult.AbsolutePath.StartsWith("/track"))
                    {
                        songs = await SpotifyApi.GetSongsFromURLs([ url ]); // TODO dont make playlist for single track
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

                    setStatusText("Downloading");

                    var semaphore = new SemaphoreSlim(5);
                    var availableSlots = new ConcurrentQueue<int>([0, 1, 2, 3, 4]);
                    var tasks = new List<Task<string?>>();
                    _usedFilenames = [];

                    async Task<string?> StartTask(Song song)
                    {
                        await semaphore.WaitAsync();
                        availableSlots.TryDequeue(out var slotId);
                        Exception? exc = null;
                        try
                        {
                            var result = await ProcessSong(song, slotId);
                            return result;
                        }
                        catch (Exception ex)
                        {
                            exc = ex;
                        }
                        finally
                        {
                            availableSlots.Enqueue(slotId);
                            semaphore.Release();
                        }
                        if (exc != null)
                        {
                            throw exc;
                        }
                        return null;
                    }

                    foreach (var song in songs)
                    {

                        tasks.Add(StartTask(song));

                    }

                    List<string?> newFilenames = [.. await Task.WhenAll(tasks)];

                    setStatusText("Writing playlist");

                    var playlist = "#EXTM3U";
                    foreach (var name in newFilenames)
                    {
                        playlist += "\n" + Path.GetFileName(name);
                    }

                    await File.WriteAllTextAsync("./downloaded/! playlist.m3u8", playlist);

                    setStatusText("Done");

                    // TODO quality of life

                } else
                {
                    setStatusText("Must be a Spotify URL");
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
            });

        }

        private static List<string> _usedFilenames = [];

        private static async Task<string?> ProcessSong(Song song, int slotId)
        {

            setStatusText("Finding match for " + String.Join(", ", song.Artists) + " - " + song.Title, slotId); 
            var found = await YoutubeMusicApi.FindSong(song);
            if (found == null)
            {
                return null;
            }

            Directory.CreateDirectory("./downloaded");
            setStatusText("Downloading " + String.Join(", ", found.Artists) + " - " + found.Title, slotId);
            var downloaded = await YtDlpApi.DownloadSong(found, "./downloaded", percentage => 
            {
                setStatusText("Downloaded " + String.Join(", ", found.Artists) + " - " + found.Title + " - " + percentage + "%", slotId);
            });

            var newFilename = String.Join(", ", found.Artists) + " - " + found.Title + "." + downloaded.Split(".").Last();
            newFilename = "./downloaded/" + Regex.Replace(newFilename, @"[\\\/:\*\?""<>\|\x00-\x1F]", "_");
            int dupes = howManyDupes(_usedFilenames, newFilename);
            if (dupes > 0)
            {
                newFilename += $" ({dupes + 1})";
            }
            _usedFilenames.Add(newFilename);

            setStatusText("Adding metadata to " + String.Join(", ", found.Artists) + " - " + found.Title, slotId);
            FileUtils.ApplyId3ToFile(downloaded, found, found.YoutubeSongUrl);

            setStatusText("Renaming and moving " + String.Join(", ", found.Artists) + " - " + found.Title, slotId);
            File.Move(downloaded, newFilename);

            setStatusText("");
            
            return newFilename;

        }

        private static int howManyDupes(List<string> names, string searchFor)
        {

            var listDupe = new List<string>(names);
            int dupes = 0;

            listDupe.Remove(searchFor);

            while (listDupe.Contains(searchFor))
            {
                dupes++;
                listDupe.Remove(searchFor);
            }

            return dupes;

        }

        public static void setStatusText(string text, int taskId = -1)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    var mw = ((MainWindow) desktop.MainWindow);
                    switch (taskId)
                    {
                        default:
                            {
                                mw.StatusText.Text = text;
                                break;
                            }
                        case 0:
                            {
                                mw.StatusText1.Text = text;
                                break;
                            }
                        case 1:
                            {
                                mw.StatusText2.Text = text;
                                break;
                            }
                        case 2:
                            {
                                mw.StatusText3.Text = text;
                                break;
                            }
                        case 3:
                            {
                                mw.StatusText4.Text = text;
                                break;
                            }
                        case 4:
                            {
                                mw.StatusText5.Text = text;
                                break;
                            }
                    }
                }
            });
        }

    }
}