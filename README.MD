# Downloader (for Music)

(update 24.03.2026 - i am a dumbass, i thought i had changed the repository to public since the beginning. here i was sending 404 links to people... anyway, its still under development before a stable version can be made and released. when will that be? no clue! i am aiming for april/early june to have a stable fully working release with an installer)

A tool to download songs, playlists and albums from various platforms. 

Fully configurable, creates local playlist files, supports various audio formats, parallel downloading, and more.

[Issues may rarely occur while downloading, just try again.](https://en.wikipedia.org/wiki/Ostrich_algorithm)

[![Written By a Human Being](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/scriptex/written-by-a-human-being/main/badge.json)](https://github.com/scriptex/written-by-a-human-being)

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

|                                                        |    Spotify[^1]     |   YouTube Music    |     SoundCloud     |        Tidal[^2]        |
|--------------------------------------------------------|:------------------:|:------------------:|:------------------:|:-----------------------:|
| Song Fetching                                          | :white_check_mark: | :white_check_mark: | :white_check_mark: |                         |
| Direct Audio Downloading                               |                    | :white_check_mark: | :white_check_mark: |   :white_check_mark:    |
| Lossless Audio                                         |                    |                    |                    |   :white_check_mark:    |
| Song Search for Downloading Other Platforms' Playlists |                    | :white_check_mark: |    Partial[^3]     |   :white_check_mark:    |
| Local Playlist File Creation                           | :white_check_mark: | :white_check_mark: | :white_check_mark: |   :white_check_mark:    |
| Song Metadata Addition                                 | :white_check_mark: | :white_check_mark: |      Partial       | :white_check_mark: [^6] |
| Album Track Number Addition                            | :white_check_mark: | :white_check_mark: |                    |   :white_check_mark:    |
| Album Disk Index Addition                              | :white_check_mark: |                    |                    |   :white_check_mark:    |
| Playlist Fetching Works without Authentication         | :white_check_mark: | :white_check_mark: | :white_check_mark: |                         |
| Downloading Works without Authentication               |                    | :white_check_mark: |    Partial[^4]     |   :white_check_mark:    |
| Private Playlist Downloading[^5]                       |                    |                    |                    |                         |

[^1]: Spotify is recommended as a playlist or album URL source to input into the downloader.

[^2]: Tidal is recommended as the audio source that songs get fetched and downloaded from. Fetching Tidal playlist or songs is not (yet) supported.

[^3]: Lots of songs are reuploads of others and some are locked behind a paid account.

[^4]: Some songs are locked behind a paid account.

[^5]: User authentication is currently not implemented for any platforms.

[^6]: Even if other platforms provide original metadata, downloading from Tidal will overwrite that metadata with the one from Tidal, as it may be more accurate.

## Features yet to be added:

- Authentication on more platforms
- Volume Normalization
- More Platforms to download from
- Different playlist formats
- Configurable Quality instead of best (downloading and saving)
- Support for other platforms?
- Maybe playlist conversion from platform to platform (moving to a more multitool-approach)
- Downloading Tidal songs/albums/playlists directly
- Playlist/Album track index in file name substitutes
- Direct in-app search functionality
- UI restructuring and user-friendliness
- Per-API settings (auth, quality, ...)
- ... more?

## Not planned

- Downloading audio from Spotify directly. Reason: Spotify is a shitty company. (And no libraries or tools seem to fully support downloading from Spotify because Spotify is one of the most stubborn companies when it comes to restricting users from downloading or modding anything that they do not like.)
