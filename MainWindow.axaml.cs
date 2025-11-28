using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Downloader.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Downloader.Api;
using Downloader.Api.Apis;

namespace Downloader
{
    public partial class MainWindow : Window
    {

        public static readonly HttpClient HttpClient = new();
        private static bool _ignoreStatusTextChanges = false;

        public MainWindow()
        {

            Logger.Init();
            InitializeComponent();
            DataContext = this;
            
            foreach (var codec in Settings.AllCodecsAndFormats.Keys)
            {
                CodecSelectionBox.Items.Add(new ComboBoxItem
                {
                    Content = codec
                });
            }
            
            foreach (var api in Settings.AllSongAudioSources)
            {
                AudioSourceSelectionBox.Items.Add(new ComboBoxItem
                {
                    Content = ISongApi.GetApiById(api)?.GetName() ?? "",
                    Name = "AudioSourceSelection_" + api
                });
            }

            CodecSelectionBox.SelectedIndex = new List<string>(Settings.AllCodecsAndFormats.Keys).IndexOf(Settings.Codec);
            CodecSelectionBox.SelectionChanged += (_, args) =>
            {
                Settings.Codec = (string?) ((ComboBoxItem?) args.AddedItems[0])?.Content ?? Settings.DefaultCodec;
                Logger.Log("Selected format " + Settings.Codec);
            };

            ThreadSelection.Value = Settings.Threads;
            ThreadSelection.ValueChanged += (_, args) =>
            {
                Settings.Threads = (int) Math.Floor(args.NewValue ?? Settings.Threads); 
                Logger.Log("Selected " + Settings.Threads + " concurrent threads");
            };

            AudioSourceSelectionBox.SelectedIndex = Settings.AllSongAudioSources.IndexOf(Settings.SongAudioSource);
            AudioSourceSelectionBox.SelectionChanged += (_, args) =>
            {
                Settings.SongAudioSource = ((ComboBoxItem?) args.AddedItems[0])?.Name?.Replace("AudioSourceSelection_", "") ?? Settings.DefaultSongAudioSource;
                Logger.Log("Selected audio source " + Settings.SongAudioSource);
            };

        }

        private void SetThreadCount(int count)
        {
            StatusTextsContainer.Children.Clear();
            for (var i = 0; i < count; i++)
            {
                StatusTextsContainer.Children.Add(new TextBlock());
            }
            Settings.Threads = count;
        }

        private CancellationTokenSource _cts = new();

        private void StartDownload_Click(object? sender, RoutedEventArgs e)
        {
            
            SetThreadCount(Settings.Threads);
            
            var text = DownloadUrlBox.Text;
            Task.Run(async () =>
            {
                try
                {
                    Logger.Log("Downloading started for query " + text);
                    await StartDownload(text);
                } catch (Exception ex)
                {
                    if (ex is not OperationCanceledException)
                    {
                        Debug.WriteLine(ex);
                        Logger.Log("Error occured while processing");
                        Logger.Log(ex.ToString());
                        SetStatusText("Error occurred");
                        Directory.Delete("./downloaded", true);
                    }
                    else
                    {
                        SetStatusText("Cancelled");
                        _ignoreStatusTextChanges = false;
                        _cts = new CancellationTokenSource();
                    }
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusTextsContainer.Children.Clear();
                        isBusy = false;
                    });
                }
            }, _cts.Token);
            
        }

        private void CancelDownload_Click(object? sender, RoutedEventArgs e)
        {
            SetStatusText("Cancelling");
            _ignoreStatusTextChanges = true;
            _cts.Cancel();
        }

        private static readonly StyledProperty<bool> IsBusyProperty = AvaloniaProperty.Register<MainWindow, bool>(nameof(isBusy));
        public bool isBusy
        {
            get => GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        private async Task StartDownload(string? urlBoxText) 
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                isBusy = true;
            });

            var url = urlBoxText ?? "";

            var validUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (!validUrl)
            {
                SetStatusText("Invalid URL");
            } else
            {
                var dataSource = ISongDataSource.AllSongDataSources.Find(s => s.UrlPartOfPlatform(url));
                if (
                    dataSource != null
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
                    if (!await DependencyDownloader.EnsureLatestDenoInstalled())
                    {
                        SetStatusText("Downloading Deno");
                        await DependencyDownloader.DownloadLatestDeno();
                    }

                    Logger.Log("Initializing APIs");
                    
                    var audioSource = ISongAudioSource.FromISongApi(ISongApi.GetApiById(Settings.SongAudioSource) ?? ISongApi.GetApiById(Settings.DefaultSongAudioSource) ?? YoutubeMusicApi.Instance);

                    if (audioSource == null)
                    {
                        throw new Exception("Could not find audio source class");
                    }
                    
                    if (Directory.Exists("./downloaded")) {
                        Directory.Delete("./downloaded", true);
                    }
                    Directory.CreateDirectory("./downloaded");
                    await dataSource.Init();
                    await audioSource.Init();

                    SetStatusText("Getting songs");
                    Logger.Log("Getting songs");
                    
                    var songs = await dataSource.GetSongs(url);

                    Logger.Log("Starting download");
                    SetStatusText("Downloading");

                    var semaphore = new SemaphoreSlim(Settings.Threads);
                    var availableSlots = new ConcurrentQueue<int>(Enumerable.Range(0, Settings.Threads));
                    var tasks = new List<Task<string?>>();
                    _usedFilenames = [];

                    async Task<string?> StartTask(Song song)
                    {
                        await semaphore.WaitAsync();
                        availableSlots.TryDequeue(out var slotId);
                        
                        Exception? exc = null;
                        
                        try
                        {
                            var result = await ProcessSong(audioSource, song, slotId);
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
                        
                            if (exc != null)
                            {
                                Logger.Log("Error occured while processing song: " + exc.Message);
                                Logger.Log(exc.StackTrace ?? "");
                                Logger.Log("-------");
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

                } else
                {
                    SetStatusText("Platform not supported");
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                isBusy = false;
            });

        }

        private static List<string> _usedFilenames = [];

        private async Task<string?> ProcessSong(ISongAudioSource source, Song song, int slotId) 
        {
            
            _cts.Token.ThrowIfCancellationRequested();
            
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
            newFilename = "./downloaded/" + Utils.Utils.SafeFileName(newFilename);
            int dupes = HowManyDupes(_usedFilenames, newFilename);
            if (dupes > 0)
            {
                newFilename += $" ({dupes + 1})";
            }
            _usedFilenames.Add(newFilename);

            Logger.Log("Finishing song " + String.Join(", ", found.Artists) + " - " + found.Title + " in slot " + slotId);
            
            SetStatusText("Adding metadata to " + String.Join(", ", found.Artists) + " - " + found.Title, slotId);
            Utils.Utils.ApplyId3ToFile(downloaded, found, found.SongUrl);

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
            if (_ignoreStatusTextChanges && taskId == -1)
            {
                return;
            }
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    var mw = (MainWindow) desktop.MainWindow;
                    if (taskId == -1)
                    {
                        mw.StatusText.Text = text;
                    } else {
                        ((TextBlock) mw.StatusTextsContainer.Children[taskId]).Text = text;
                    }
                }
            });
        }

    }
}