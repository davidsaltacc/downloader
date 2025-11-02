using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace downloader.Utils.Songs
{
    internal class YoutubeMusicSong(string album, string[] artists, string title, int indexOnDisk, int diskIndex, int releaseYear, string imageUrl, string youtubeSongUrl) : Song(album, artists, title, indexOnDisk, diskIndex, releaseYear, imageUrl)
    {
        readonly string youtubeSongUrl = youtubeSongUrl;
    }
}
