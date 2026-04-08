using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Downloader.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Downloader.Api;
using Downloader.Api.Apis;

namespace Downloader
{
    public partial class MainWindow : Window
    {
        
        private static HttpClientHandler _httpClientHandler = new HttpClientHandler();
        static MainWindow()
        {
            _httpClientHandler.AllowAutoRedirect = true;
        }
        
        public static readonly HttpClient HttpClient = new(_httpClientHandler);
        private static bool _ignoreStatusTextChanges = false;

        public MainWindow()
        {

            Logger.Init();
            InitializeComponent();
            DataContext = this;
            
            TitleBarIcon.Source = new Bitmap("icon.ico");
            
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
            
            foreach (var format in Settings.AllPlaylistFormats)
            {
                PlaylistFileFormatSelectionBox.Items.Add(new ComboBoxItem
                {
                    Content = format
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
                Settings.Threads = (int) Math.Floor(args.NewValue ?? Settings.DefaultThreads); 
                Logger.Log("Selected " + Settings.Threads + " concurrent threads");
            };

            AudioSourceSelectionBox.SelectedIndex = Settings.AllSongAudioSources.IndexOf(Settings.SongAudioSource);
            AudioSourceSelectionBox.SelectionChanged += (_, args) =>
            {
                Settings.SongAudioSource = ((ComboBoxItem?) args.AddedItems[0])?.Name?.Replace("AudioSourceSelection_", "") ?? Settings.DefaultSongAudioSource;
                Logger.Log("Selected audio source " + Settings.SongAudioSource);
            };

            CreatePlaylistFileCheckbox.IsChecked = Settings.CreatePlaylistFile;
            CreatePlaylistFileCheckbox.IsCheckedChanged += (_, __) =>
            {
                Settings.CreatePlaylistFile = CreatePlaylistFileCheckbox.IsChecked ?? Settings.DefaultCreatePlaylistFile;
                Logger.Log("Set create playlist file to " + Settings.CreatePlaylistFile);
            };

            DestinationFolderTextBox.Text = Settings.DestinationFolder;
            DestinationFolderTextBox.TextChanged += (_, __) =>
            {
                Settings.DestinationFolder = DestinationFolderTextBox.Text ?? Settings.DefaultDestinationFolder;
                Logger.Log("Set destination folder to " + Settings.DestinationFolder);
            };

            PlaylistFolderNameTextBox.Text = Settings.PlaylistFolderName;
            PlaylistFolderNameTextBox.TextChanged += (_, __) =>
            {
                Settings.PlaylistFolderName = PlaylistFolderNameTextBox.Text ?? Settings.DefaultPlaylistFolderName;
                Logger.Log("Set playlist folder name to " + Settings.PlaylistFolderName);
            };
            
            DestinationFolderSelectButton.Click += async (_, __) =>
            {
                var topLevel = GetTopLevel(this);

                if (topLevel == null)
                {
                    return;
                }

                var files = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                {
                    Title = "Select Destination Folder",
                    AllowMultiple = false,
                    SuggestedStartLocation = await topLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Music)
                });

                if (files.Count <= 0)
                {
                    return;
                }

                Settings.DestinationFolder = DestinationFolderTextBox.Text = files[0].Path.AbsolutePath;

            };

            DestinationSubfolderTextBox.Text = Settings.DestinationSubfolder;
            DestinationSubfolderTextBox.TextChanged += (_, __) =>
            {
                Settings.DestinationSubfolder = DestinationSubfolderTextBox.Text ?? Settings.DefaultDestinationSubfolder;
                Logger.Log("Set subfolder to " + Settings.DestinationSubfolder);
            };

            SongFileNameTextBox.Text = Settings.SongFileName;
            SongFileNameTextBox.TextChanged += (_, __) =>
            {
                Settings.SongFileName = SongFileNameTextBox.Text ?? Settings.DefaultSongFileName;
                Logger.Log("Set song filename to " + Settings.SongFileName);
            };

            PlaylistFileNameTextBox.Text = Settings.PlaylistFileName;
            PlaylistFileNameTextBox.TextChanged += (_, __) =>
            {
                Settings.PlaylistFileName = PlaylistFileNameTextBox.Text ?? Settings.DefaultPlaylistFileName;
                Logger.Log("Set playlist file name to " + Settings.PlaylistFileName);
            };
            
            PlaylistFileFormatSelectionBox.SelectedIndex = Settings.AllPlaylistFormats.IndexOf(Settings.PlaylistFileFormat);
            PlaylistFileFormatSelectionBox.SelectionChanged += (_, args) =>
            {
                Settings.PlaylistFileFormat = ((ComboBoxItem?) args.AddedItems[0])?.Content?.ToString() ?? Settings.DefaultPlaylistFileFormat;
                Logger.Log("Selected playlist file format " + Settings.PlaylistFileFormat);
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
                        Logger.Log("Error occured while processing: " + ex.Message);
                        Logger.Log(ex.StackTrace ?? "");
                        Logger.Log("-------");
                        SetStatusText("Error occurred");
                        Directory.Delete("./downloaded", true);
                    }
                    else
                    {
                        Logger.Log("Cancelled: " + ex.StackTrace);
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

                    if (!Path.Exists(Settings.DestinationFolder))
                    {
                        SetStatusText("Destination folder configured in settings does not exist.");
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            isBusy = false;
                        });
                        return;
                    } 
                    
                    try {
                        Path.GetFullPath(Path.Join(Settings.DestinationFolder, Helpers.InsertSubstitutionsForPath(Settings.DestinationSubfolder, new Song("dummy album", ["dummy artist"], "dummy song", -1, -1, -1, 1999, "dummy image url", "dummy song url", "dummy api"), 0)));
                    } catch {
                        SetStatusText("Subfolder configured in settings does not parse to valid folder.");
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            isBusy = false;
                        });
                        return;
                    }
                    
                    var audioSource = ISongAudioSource.FromISongApi(ISongApi.GetApiById(Settings.SongAudioSource) ?? ISongApi.GetApiById(Settings.DefaultSongAudioSource) ?? YoutubeMusicApi.Instance);
                        
                    if (audioSource == null)
                    {
                        throw new Exception("Could not find audio source class");
                    }
                    
                    Logger.Log("Starting dependency download");

                    if (!await DependencyDownloader.EnsureFFmpegInstalled())
                    {
                        SetStatusText("Downloading FFmpeg");
                        await DependencyDownloader.DownloadLatestFFmpeg(progress => SetStatusText("Downloading FFmpeg - " + progress + "%"));
                    }

                    if (dataSource.NeedsDependency(Dependency.YtDlp, false) || 
                        audioSource.NeedsDependency(Dependency.YtDlp, true))
                    {
                        if (!await DependencyDownloader.EnsureLatestYtDlpInstalled())
                        {
                            SetStatusText("Downloading yt-dlp");
                            await DependencyDownloader.DownloadLatestYtDlp(progress => SetStatusText("Downloading yt-dlp - " + progress + "%"));
                        }
                    }

                    if (dataSource.NeedsDependency(Dependency.JavascriptRuntime, false) ||
                        audioSource.NeedsDependency(Dependency.JavascriptRuntime, true))
                    {
                        if (!await DependencyDownloader.EnsureLatestQjsInstalled())
                        {
                            SetStatusText("Downloading QuickJS");
                            await DependencyDownloader.DownloadLatestQjs(progress => SetStatusText("Downloading QuickJS - " + progress + "%"));
                        }
                    }

                    if (dataSource.NeedsDependency(Dependency.Python, false) ||
                        audioSource.NeedsDependency(Dependency.Python, true))
                    {
                        if (!await DependencyDownloader.EnsureLatestEmbeddablePythonInstalled())
                        {
                            SetStatusText("Downloading Python");
                            await DependencyDownloader.DownloadLatestEmbeddablePython(progress => SetStatusText("Downloading Python - " + progress + "%"));
                        }
                    }

                    Logger.Log("Initializing APIs");
                    SetStatusText("Initializing APIs");
                    
                    if (Directory.Exists("./downloaded")) {
                        Directory.Delete("./downloaded", true);
                    }
                    Directory.CreateDirectory("./downloaded");
                    await dataSource.Init();
                    await audioSource.Init();

                    SetStatusText("Fetching songs");
                    Logger.Log("Fetching songs");
                    
                    var songs = await dataSource.GetSongs(url);

                    Logger.Log("Starting download");
                    SetStatusText("Downloading");

                    var semaphore = new SemaphoreSlim(Settings.Threads);
                    var availableSlots = new ConcurrentQueue<int>(Enumerable.Range(0, Settings.Threads));
                    var tasks = new List<Task<string?>>();
                    _usedFilenames = [];

                    async Task<string?> StartTask(Song song, int index)
                    {
                        await semaphore.WaitAsync();
                        availableSlots.TryDequeue(out var slotId);
                        
                        Exception? exc = null;
                        
                        try
                        {
                            var result = await ProcessSong(audioSource, song, slotId, index);
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
                            }
                        }
                        
                        return null;
                    }

                    var index = 0;
                    foreach (var song in songs)
                    {

                        tasks.Add(StartTask(song, index));
                        index += 1;

                    }

                    List<string?> newFilenames = [.. await Task.WhenAll(tasks)];
                    
                    var mixed = ContainsMixedAlbums(songs);

                    if (Settings.CreatePlaylistFile && songs.Length > 1)
                    {

                        Logger.Log("Writing playlist to file");
                        SetStatusText("Writing playlist");

                        var paths = newFilenames.Select(file => Path.GetFileName(file)).ToArray();
                        var playlist = Settings.PlaylistFileFormat switch
                        {
                            "M3U8" => Helpers.SaveM3U8Playlist(paths),
                            "XSPF" => Helpers.SaveXSPFPlaylist(paths, songs, !mixed),
                            _ => null
                        };

                        if (playlist != null)
                        {
                            
                            await File.WriteAllTextAsync("./downloaded/" + Helpers.SafeFileName(Settings.PlaylistFileName) + Settings.PlaylistFileFormat switch
                            {
                                "M3U8" => ".m3u8",
                                "XSPF" => ".xspf",
                                _ => null
                            }, playlist);
                        }

                    }
                    else if (Settings.CreatePlaylistFile)
                    {
                        Logger.Log("Skipping playlist creation due to playlist only containing single song");
                    }

                    SetStatusText("Moving files");
                    Logger.Log("Moving files");

                    var finalFolder = Path.Join(Settings.DestinationFolder, Helpers.SafeFolderName(Helpers.InsertSubstitutionsForPath(mixed ? Settings.PlaylistFolderName : Settings.DestinationSubfolder, mixed ? null : songs[0], null, mixed)));
                    Directory.CreateDirectory(finalFolder); // create all folders leading up to the final folder
                    Directory.Delete(finalFolder, true); // delete last folder for moving files - i do this because i don't think .Move creates all folders leading up to the final folder 
                    Directory.Move("./downloaded/", finalFolder);
                    
                    SetStatusText("Done");
                    Logger.Log("Download finished");

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

        private async Task<string?> ProcessSong(ISongAudioSource source, Song song, int slotId, int index) 
        {
            
            _cts.Token.ThrowIfCancellationRequested();
            
            Logger.Log("Finding match for song " + String.Join(", ", song.Artists) + " - " + song.Title + " in slot " + slotId);
            SetStatusText("Finding match for " + String.Join(", ", song.Artists) + " - " + song.Title, slotId); 
            var found = await source.FindSong(song);

            Logger.Log("Downloading " + String.Join(", ", song.Artists) + " - " + song.Title + " in slot " + slotId);
            SetStatusText("Downloading " + String.Join(", ", song.Artists) + " - " + song.Title, slotId);
            var downloaded = await source.DownloadSong(found, "./downloaded", percentage => 
            {
                SetStatusText("Downloaded " + String.Join(", ", song.Artists) + " - " + song.Title + " - " + percentage + "%", slotId);
            });
            if (downloaded == null)
            {
                return null;
            }
            
            SetStatusText("Encoding " + String.Join(", ", song.Artists) + " - " + song.Title, slotId);
            var reEncoded = await FFMpegApi.ReEncode(downloaded, Settings.Codec, true);
            SetStatusText("Encoded " + String.Join(", ", song.Artists) + " - " + song.Title, slotId);

            var newFilename = Helpers.InsertSubstitutionsForPath(Settings.SongFileName, song, index) + "." + reEncoded.Split(".").Last();
            newFilename = "./downloaded/" + Helpers.SafeFileName(newFilename);
            int dupes = HowManyDupes(_usedFilenames, newFilename);
            if (dupes > 0)
            {
                newFilename += $" ({dupes + 1})";
            }
            _usedFilenames.Add(newFilename);

            Logger.Log("Finishing song " + String.Join(", ", song.Artists) + " - " + song.Title + " in slot " + slotId);
            
            SetStatusText("Adding metadata to " + String.Join(", ", song.Artists) + " - " + song.Title, slotId);
            await Helpers.ApplyId3ToFile(reEncoded, song, found.SongUrl);

            SetStatusText("Renaming and moving " + String.Join(", ", song.Artists) + " - " + song.Title, slotId);
            File.Move(reEncoded, newFilename, true);

            SetStatusText("", slotId);
            Logger.Log("Fully downloaded song " + String.Join(", ", song.Artists) + " - " + song.Title + " in slot " + slotId);
            
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

        private bool ContainsMixedAlbums(Song[] songs)
        {
            if (songs.Length == 0)
            {
                return true;
            }

            var artist = songs[0].Artists[0];
            var album = songs[0].Album;
            
            foreach (var song in songs)
            {
                if (song.Artists[0] != artist || song.Album != album)
                {
                    return true;
                }
            }

            return false;
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