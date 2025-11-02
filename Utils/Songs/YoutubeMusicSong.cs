
namespace downloader.Utils.Songs
{
    internal class YoutubeMusicSong(string album, string[] artists, string title, int indexOnDisk, int diskIndex, int releaseYear, string imageUrl, string youtubeSongUrl) : Song(album, artists, title, indexOnDisk, diskIndex, releaseYear, imageUrl)
    {
        public readonly string youtubeSongUrl = youtubeSongUrl;
    }
}
