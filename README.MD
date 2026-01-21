# Downloader (for Music)

A tool to download songs, playlists and albums from various platforms. 

Fully configurable, creates local playlist files, supports various audio formats, parallel downloading, and more.

Note: Not a single line of code was written by AI. (Except for some of the regex stuff. Writing some elvish runes is one of the few things it can be useful for) #fuckvibecoding 

## Disclaimer

This is for educational purposes exclusively. I do not encourage piracy or any illegal distribution or downloading of songs and other media from platforms such as YouTube Music, Spotify and all other platforms supported by this app. 

## Platform Comparison 

|                                                        |    Spotify[^1]     | YouTube Music[^2]  |     SoundCloud     |
|--------------------------------------------------------|:------------------:|:------------------:|:------------------:|
| Song Fetching                                          | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Direct Audio Downloading                               |                    | :white_check_mark: | :white_check_mark: |
| Song Search for Downloading Other Platforms' Playlists |                    | :white_check_mark: |    Partial[^3]     |
| Local Playlist File Creation                           | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Song Metadata Addition                                 | :white_check_mark: | :white_check_mark: |      Partial       |
| Album Track Number Addition                            | :white_check_mark: | :white_check_mark: |                    |
| Album Disk Index Addition                              | :white_check_mark: |                    |                    |
| Playlist Fetching Works without Authentication         | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Downloading Works without Authentication               |                    | :white_check_mark: |    Partial[^4]     |
| Private Playlist Downloading[^5]                       |                    |                    |                    |

[^1]: Spotify is recommended as a playlist or album URL source to input into the downloader.

[^2]: YouTube Music is recommended as the audio source that songs get fetched and downloaded from.

[^3]: Lots of songs are reuploads of others and some are locked behind a paid account.

[^4]: Some songs are locked behind a paid account.

[^5]: User authentication is currently not implemented for any platforms. 

## Features yet to be added:

- Authentication on more platforms
- Volume Normalization
- More Platforms to download from
- Different playlist formats
- Configurable Quality instead of best (downloading and saving)
- Support for other platforms?
- Maybe playlist conversion from platform to platform (moving to a more multitool-approach)
- ... more?