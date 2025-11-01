using downloader.Utils.Songs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Downloader.Apis
{
    internal class SpotifyApi
    {

        private static string? token = null;

        public static async Task initDownloading()
        {

            var response = await MainWindow.httpClient.GetAsync("https://raw.githubusercontent.com/spotDL/spotify-downloader/refs/heads/master/spotdl/utils/config.py");
            // if you make it public, i hope you don't mind if i use it. thank you spotdl 

            string clientId = "";
            string clientSecret = "";

            foreach (string line in (await response.Content.ReadAsStringAsync()).Split("\n")) {
                var lineFormatted = line.ToLower().Replace(" ", "").Replace("\t", "");
                if (lineFormatted.StartsWith("\"client_id\":\"")) {
                    clientId = lineFormatted[..^2].Replace("\"client_id\":\"", "");
                }
                if (lineFormatted.StartsWith("\"client_secret\":\""))
                {
                    clientSecret = lineFormatted[..^2].Replace("\"client_secret\":\"", "");
                }
            }

            var responseAuth = await MainWindow.httpClient.SendAsync(new HttpRequestMessage{
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://accounts.spotify.com/api/token"),
                Headers = {
                    { "authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(clientId + ":" + clientSecret)) }
                },
                Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded")
            });

            var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(await responseAuth.Content.ReadAsStringAsync());

            token = tokenData["token_type"] + " " + tokenData["access_token"];

        }

        public static async Task<HttpResponseMessage> sendApiRequest(string endpoint, HttpMethod method, Dictionary<string, string> params_, HttpContent? content = null)
        {

            if (token == null)
            {
                throw new Exception("Tried to make spotify API call before intializing and obtaining token");
            }

            var qs = HttpUtility.ParseQueryString("?");

            foreach (var item in params_)
            {
                qs.Set(item.Key, item.Value);
            }

            Uri uri = new Uri("https://api.spotify.com/v1" + endpoint + "?" + qs.ToString());

            HttpRequestMessage message = new HttpRequestMessage
            {
                Method = method,
                RequestUri = uri,
                Headers = {
                    { "authorization", token }
                },
                Content = content
            };

            HttpResponseMessage response = await MainWindow.httpClient.SendAsync(message);

            int retries = 0;
            while (response.StatusCode == HttpStatusCode.TooManyRequests && retries < 5) {
                Thread.Sleep(response.Headers.RetryAfter.Delta.GetValueOrDefault(TimeSpan.FromSeconds(5)));
                response = await MainWindow.httpClient.SendAsync(message);
                retries++;
            }

            return response;

        }

        public static string extractIdFromUrl(string url)
        {
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult);
            return uriResult.AbsolutePath.Split("/")[2];
        }

        public class GetSongsResponse { 
            public class Track
            {
                public class Album
                {
                    public class Image
                    {
                        public required string url { get; set; }
                        public required int width { get; set; }
                        public required int height { get; set; }
                    }

                    public required Image[] images { get; set; }
                    public required string name { get; set; }
                    public required string release_date { get; set; }
                }
                public class Artist
                {
                    public required string name { get; set; }
                }

                public required Album album { get; set; }
                public required Artist[] artists { get; set; }
                public required int disc_number { get; set; }
                public required int duration_ms { get; set; }
                public required string name { get; set; }
                public required int track_number { get; set; }
            }

            public required Track[] tracks { get; set; }
        }

        public static async Task<SpotifySong[]> getSongsFromURLs(string[] urls)
        {

            string[] ids = new string[urls.Length];
            int i = 0;
            foreach (string url in urls) {
                ids[i] = extractIdFromUrl(url);
                i++;
            }

            var songsData = JsonSerializer.Deserialize<GetSongsResponse>(await (await sendApiRequest("/tracks", HttpMethod.Get, new([ new("ids", String.Join(",", ids)) ]))).Content.ReadAsStringAsync());
            var songs = new SpotifySong[songsData.tracks.Length];
            // TODO split if more than 50 ids

            i = 0;
            foreach (var songData in songsData.tracks)
            {
                songs[i] = new SpotifySong(
                    songData.album.name,
                    songData.artists.Select(artist => artist.name).ToArray(),
                    songData.name,
                    songData.track_number,
                    songData.disc_number,
                    int.Parse(songData.album.release_date.Split("-")[0]),
                    songData.album.images[0].url,
                    urls[i]
                );
                i++;
            }

            return songs;
        }

        public class GetSongsInAlbumResponse {
            public class Track
            {
                public class ExternalUrls
                {
                    public required string spotify { get; set; }
                }

                public required ExternalUrls external_urls { get; set; }
            }

            public required string? next { get; set; }
            public required Track[] items { get; set; }
        }

        public static async Task<SpotifySong[]> getSongsInAlbum(string albumUrl)
        {
            List<string> urls = new();

            async Task req(int offset)
            {
                var songsData = JsonSerializer.Deserialize<GetSongsInAlbumResponse>(await (await sendApiRequest("/albums/" + extractIdFromUrl(albumUrl) + "/tracks", HttpMethod.Get, new([new("limit", "50"), new("offset", offset.ToString())]))).Content.ReadAsStringAsync());
                urls.AddRange(songsData.items.Select(track => track.external_urls.spotify));

                if (songsData.next != null)
                {
                    await req(offset + 50);
                }
            }

            await req(0);

            return await getSongsFromURLs(urls.ToArray());
        }

        public class GetSongsInPlaylistResponse
        {
            public class PlaylistTrack
            {
                public class Track
                {
                    public class ExternalUrls
                    {
                        public required string spotify { get; set; }
                    }

                    public required ExternalUrls external_urls { get; set; }
                }

                public required Track track { get; set; }
            }

            public required string? next { get; set; }
            public required PlaylistTrack[] items { get; set; }
        }

        public static async Task<SpotifySong[]> getSongsInPlaylist(string playlistUrl)
        {
            List<string> urls = new();

            async Task req(int offset)
            {
                var songsData = JsonSerializer.Deserialize<GetSongsInPlaylistResponse>(await (await sendApiRequest("/playlists/" + extractIdFromUrl(playlistUrl) + "/tracks", HttpMethod.Get, new([new("limit", "50"), new("offset", offset.ToString())]))).Content.ReadAsStringAsync());
                urls.AddRange(songsData.items.Select(playlistTrack => playlistTrack.track.external_urls.spotify));

                if (songsData.next != null)
                {
                    await req(offset + 50);
                }
            }

            await req(0);

            return await getSongsFromURLs(urls.ToArray());
        }

    }
}
