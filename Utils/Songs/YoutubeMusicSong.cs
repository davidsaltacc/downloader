
namespace Downloader.Utils.Songs
{
    internal class YoutubeMusicSong(string album, string[] artists, string title, int durationMs, int indexOnDisk, int diskIndex, int releaseYear, string imageUrl, string youtubeSongUrl) : Song(album, artists, title, durationMs, indexOnDisk, diskIndex, releaseYear, imageUrl)
    {
        public readonly string YoutubeSongUrl = youtubeSongUrl;

        public override int GetHashCode() => (base.GetHashCode(), YoutubeSongUrl).GetHashCode();

    }
}
