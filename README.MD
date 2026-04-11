# Downloader (for Music)

A tool to download songs, playlists and albums from various platforms. 

Fully configurable, creates local playlist files, supports various audio formats, parallel downloading, and more.

[Issues may rarely occur while downloading, just try again.](https://en.wikipedia.org/wiki/Ostrich_algorithm)

[![Written By a Human Being](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/scriptex/written-by-a-human-being/main/badge.json)](https://github.com/scriptex/written-by-a-human-being)

# Download 

| Installer                                                                                                       | Portable                                                                                                       |
|-----------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------|
| [Download Latest](https://github.com/davidsaltacc/downloader/releases/latest/download/Downloader.Installer.exe) | [Download Latest](https://github.com/davidsaltacc/downloader/releases/latest/download/Downloader.Portable.zip) |

## Notable features

- High quality audio downloading
- Support for lossless audio
- Support for many platforms
- Proper metadata addition 
- Proper playlist file creation
- Supports multiple audio codecs / formats
- Parallel downloading
- No login required on most platforms for downloading[^7]
- High accuracy song search for downloading from other sites

[^7]: Spotify does this by fetching the latest secrets and getting a TOTP key from the web version while impersonating a real browser. YouTube Music's API that it uses behind the Web UI requires no auth, SoundCloud has a free open API, Tidal is implemented over the unofficial Hi-Fi API by searching for a working instance.

## Disclaimer

This is for educational purposes exclusively. I do not encourage piracy or any illegal distribution or downloading of songs and other media from platforms such as YouTube Music, Spotify and all other platforms supported by this app. 

## Platform Comparison 

|                                                        |      Spotify       |   YouTube Music    |     SoundCloud     |        Tidal[^2]       |
|--------------------------------------------------------|:------------------:|:------------------:|:------------------:|:----------------------:|
| Song Fetching                                          | :white_check_mark: | :white_check_mark: | :white_check_mark: |   :white_check_mark:   |
| Direct Audio Downloading                               |                    | :white_check_mark: | :white_check_mark: |   :white_check_mark:   |
| High Quality (320+kb/s) Audio                          |                    |                    |                    |   :white_check_mark:   |
| Lossless Audio                                         |                    |                    |                    |   :white_check_mark:   |
| Song Search for Downloading Other Platforms' Playlists |                    | :white_check_mark: |    Partial[^3]     |   :white_check_mark:   |
| Local Playlist File Creation                           | :white_check_mark: | :white_check_mark: | :white_check_mark: |   :white_check_mark:   |
| Song Metadata Addition                                 | :white_check_mark: | :white_check_mark: |      Partial       |   :white_check_mark:   |
| Album Track Number Addition                            | :white_check_mark: | :white_check_mark: |                    |   :white_check_mark:   |
| Album Disk Index Addition                              | :white_check_mark: |                    |                    |   :white_check_mark:   |
| Playlist Fetching Works without Authentication         | :white_check_mark: | :white_check_mark: | :white_check_mark: |   :white_check_mark:   |
| Downloading Works without Authentication               |                    | :white_check_mark: |    Partial[^4]     |   :white_check_mark:   |
| Private Playlist Downloading[^5]                       |                    |                    |                    |                        |

[^2]: Tidal is recommended as the audio source that songs get fetched and downloaded from.

[^3]: Lots of songs are reuploads of others and some are locked behind a paid account.

[^4]: Some songs are locked behind a paid account.

[^5]: User authentication is currently not implemented for any platforms.

## Stuff yet to be added:

- Auto-Updater
- Song caching during downloading (if an album gets fetched, add all songs to cache, then instead of searching platform directly, first search cache)
- Download wizard configuring ideal settings for non-power-users
- Config saving (to file)
- Authentication on more platforms
- More Platforms to download from
- Support for other platforms?
- Maybe playlist conversion from platform to platform (moving to a more multitool-approach)
- Direct in-app search functionality
- UI restructuring and user-friendliness
- Per-API settings (auth, quality, ...)
- ... more?

## Not planned

- Downloading audio from Spotify directly. Reason: Spotify is a shitty company. (And no libraries or tools seem to fully support downloading from Spotify because Spotify is one of the most stubborn companies when it comes to restricting users from downloading or modding anything that they do not like.)
