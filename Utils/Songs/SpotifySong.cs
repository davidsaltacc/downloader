using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace downloader.Utils.Songs
{
    internal class SpotifySong(string album, string[] artists, string title, int indexOnDisk, int diskIndex, int releaseYear, string imageUrl, string spotifySongUrl) : Song(album, artists, title, indexOnDisk, diskIndex, releaseYear, imageUrl)
    {
        readonly string spotifySongUrl = spotifySongUrl;
    }
}
