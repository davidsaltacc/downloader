using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Downloader.Utils.Songs;
using Downloader.Utils;
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
using Downloader.Apis;

namespace Downloader
{
    public partial class MainWindow : Window
    {

        public static readonly HttpClient HttpClient = new();

        public MainWindow()
        {

            Logger.Init();
            InitializeComponent();
            DataContext = this;
            
        }

        private void StartDownload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var text = DownloadURLBox.Text;
            Task.Run(async () =>
            {
                try
                {
                    Logger.Log("Downloading started for query " + text);
                    await StartDownload(text);
                } catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    Logger.Log("Error occured while processing");
                    Logger.Log(ex.ToString());
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        IsBusy = false;
                    });
                    SetStatusText("Error occurred");
                    Directory.Delete("./downloaded", true);
                }
            });
        }

        public static readonly StyledProperty<bool> IsBusyProperty = AvaloniaProperty.Register<MainWindow, bool>(nameof(IsBusy));
        public bool IsBusy
        {
            get => GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        private async Task StartDownload(string? urlBoxText) 
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = true;
            });

            var url = urlBoxText ?? "";

            var validUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (!validUrl)
            {
                SetStatusText("Invalid URL");
            } else
            { 
                if (
                    (uriResult?.Host.Contains("spotify", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (uriResult?.Host.Contains("music.youtube", StringComparison.OrdinalIgnoreCase) ?? false)
                )
                {
                    SetStatusText("Starting");
                    
                    Logger.Log("Starting dependency download");

                    if (!await DependencyDownloader.EnsureFFmpegInstalled())
                    {
                        SetStatusText("Downloading FFmpeg");
                        await DependencyDownloader.DownloadLatestFFmpeg();
                    }
                    if (!await DependencyDownloader.EnsureLatestYtDlpInstalled())
                    {
                        SetStatusText("Downloading yt-dlp");
                        await DependencyDownloader.DownloadLatestYtDlp();
                    }
                    if (!await DependencyDownloader.EnsureLatestQjsInstalled())
                    {
                        SetStatusText("Downloading QuickJS");
                        await DependencyDownloader.DownloadLatestQjs();
                    }

                    Logger.Log("Initializing APIs");
                    
                    var dataSource = YoutubeMusicApi.Instance; // TODO decide automatically
                    var audioSource = YoutubeMusicApi.Instance;
                    
                    Directory.CreateDirectory("./downloaded");
                    await dataSource.Init();
                    await audioSource.Init();

                    SetStatusText("Getting songs");
                    Logger.Log("Getting songs");
                    
                    // TODO improve UI (less hardcoded stuff, make it all configurable - nicer frontend)
                    // TODO add support for more services?
                    // TODO make installer & related
                    // TODO release
                    
                    var songs = await dataSource.GetSongs(url);

                    Logger.Log("Starting download");
                    SetStatusText("Downloading");

                    var semaphore = new SemaphoreSlim(5);
                    var availableSlots = new ConcurrentQueue<int>([0, 1, 2, 3, 4]);
                    var tasks = new List<Task<string?>>();
                    _usedFilenames = [];

                    async Task<string?> StartTask(Song song)
                    {
                        await semaphore.WaitAsync();
                        availableSlots.TryDequeue(out var slotId);
                        
                        Exception? exc = null;
                        string? trace = null;
                        
                        try
                        {
                            var result = await ProcessSong(audioSource, song, slotId);
                            return result;
                        }
                        catch (Exception ex)
                        {
                            exc = ex;
                            trace = ex.StackTrace;
                        }
                        finally
                        {
                            availableSlots.Enqueue(slotId);
                            semaphore.Release();
                        
                            if (exc != null)
                            {
                                Logger.Log("Error occured while processing song at:");
                                Logger.Log(trace ?? "");
                                Logger.Log("-------");
                                Debug.WriteLine("Error at:");
                                Debug.WriteLine(trace);
                                Debug.WriteLine("--------");
                                throw exc;
                            }
                        }
                        
                        return null;
                    }

                    foreach (var song in songs)
                    {

                        tasks.Add(StartTask(song));

                    }

                    List<string?> newFilenames = [.. await Task.WhenAll(tasks)];

                    Logger.Log("Writing playlist to file");
                    SetStatusText("Writing playlist");

                    var playlist = "#EXTM3U";
                    foreach (var name in newFilenames)
                    {
                        playlist += "\n" + Path.GetFileName(name);
                    }

                    await File.WriteAllTextAsync("./downloaded/! playlist.m3u8", playlist);

                    SetStatusText("Done");
                    Logger.Log("Finished download");

                    // TODO quality of life

                } else
                {
                    SetStatusText("Must be a Spotify URL");
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
            });

        }

        private static List<string> _usedFilenames = [];

        private static async Task<string?> ProcessSong<T>(ISongAudioSource<T> source, Song song, int slotId) where T : Song
        {
            
            Logger.Log("Finding match for song " + String.Join(", ", song.Artists) + " - " + song.Title + " in slot " + slotId);
            SetStatusText("Finding match for " + String.Join(", ", song.Artists) + " - " + song.Title, slotId); 
            var found = await source.FindSong(song);
            if (found == null)
            {
                return null;
            }

            Logger.Log("Downloading " + String.Join(", ", found.Artists) + " - " + found.Title + " in slot " + slotId);
            SetStatusText("Downloading " + String.Join(", ", found.Artists) + " - " + found.Title, slotId);
            var downloaded = await source.DownloadSong(found, "./downloaded", percentage => 
            {
                SetStatusText("Downloaded " + String.Join(", ", found.Artists) + " - " + found.Title + " - " + percentage + "%", slotId);
            });
            if (downloaded == null)
            {
                return null;
            }

            var newFilename = String.Join(", ", found.Artists) + " - " + found.Title + "." + downloaded.Split(".").Last();
            newFilename = "./downloaded/" + Regex.Replace(newFilename, @"[\\\/:\*\?""<>\|\x00-\x1F]", "_");
            int dupes = HowManyDupes(_usedFilenames, newFilename);
            if (dupes > 0)
            {
                newFilename += $" ({dupes + 1})";
            }
            _usedFilenames.Add(newFilename);

            Logger.Log("Finishing song " + String.Join(", ", found.Artists) + " - " + found.Title + " in slot " + slotId);
            
            SetStatusText("Adding metadata to " + String.Join(", ", found.Artists) + " - " + found.Title, slotId);
            Utils.Utils.ApplyId3ToFile(downloaded, found, source.GetSongSourceUrl(found));

            SetStatusText("Renaming and moving " + String.Join(", ", found.Artists) + " - " + found.Title, slotId);
            File.Move(downloaded, newFilename, true);

            SetStatusText("", slotId);
            Logger.Log("Fully downloaded song " + String.Join(", ", found.Artists) + " - " + found.Title + " in slot " + slotId);
            
            return newFilename;

        }

        private static int HowManyDupes(List<string> names, string searchFor)
        {

            var listDupe = new List<string>(names);
            var dupes = 0;

            listDupe.Remove(searchFor);

            while (listDupe.Contains(searchFor))
            {
                dupes++;
                listDupe.Remove(searchFor);
            }

            return dupes;

        }

        private static void SetStatusText(string text, int taskId = -1)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    var mw = (MainWindow) desktop.MainWindow;
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