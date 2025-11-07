using downloader.Utils.Songs;
using System;
using System.Collections.Generic;
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

        private static string? _token = null;

        public class TokenResponse
        {
            public required string token_type { get; set; }
            public required string access_token { get; set; }
        }

        public static async Task InitDownloading()
        { 

            var response = await MainWindow.HttpClient.GetAsync("https://raw.githubusercontent.com/spotDL/spotify-downloader/refs/heads/master/spotdl/utils/config.py");
            // if you make it public, i hope you don't mind if i use it. thank you spotdl 

            var clientId = "";
            var clientSecret = "";

            foreach (var line in (await response.Content.ReadAsStringAsync()).Split("\n")) {
                var lineFormatted = line.ToLower().Replace(" ", "").Replace("\t", "");
                if (lineFormatted.StartsWith("\"client_id\":\"")) {
                    clientId = lineFormatted[..^2].Replace("\"client_id\":\"", "");
                }
                if (lineFormatted.StartsWith("\"client_secret\":\""))
                {
                    clientSecret = lineFormatted[..^2].Replace("\"client_secret\":\"", "");
                }
            }

            var responseAuth = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage{
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://accounts.spotify.com/api/token"),
                Headers = {
                    { "authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(clientId + ":" + clientSecret)) }
                },
                Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded")
            });

            var tokenData = JsonSerializer.Deserialize<TokenResponse>(await responseAuth.Content.ReadAsStringAsync());

            _token = tokenData?.token_type + " " + tokenData?.access_token;

        }

        private static async Task<HttpResponseMessage> SendApiRequest(string endpoint, HttpMethod method,
            Dictionary<string, string> parameters, HttpContent? content = null)
        {

            if (_token == null)
            {
                throw new Exception("Tried to make spotify API call before initializing and obtaining token");
            }

            var qs = HttpUtility.ParseQueryString("?");

            foreach (var item in parameters)
            {
                qs.Set(item.Key, item.Value);
            }

            var uri = new Uri("https://api.spotify.com/v1" + endpoint + "?" + qs);

            var response = await MainWindow.HttpClient.SendAsync(CreateMessage());

            var retries = 0;
            while (response.StatusCode == HttpStatusCode.TooManyRequests && retries < 5) {
                if (response.Headers.RetryAfter == null)
                {
                    await Task.Delay(5000);
                } else {
                    await Task.Delay(response.Headers.RetryAfter.Delta.GetValueOrDefault(TimeSpan.FromSeconds(5)));
                }
                response = await MainWindow.HttpClient.SendAsync(CreateMessage());
                retries++;
            }

            return response;
            
            HttpRequestMessage CreateMessage()
            {
                return new HttpRequestMessage
                {
                    Method = method,
                    RequestUri = uri,
                    Headers = {
                        { "authorization", _token }
                    },
                    Content = content
                };
            }

        }

        private static string? ExtractIdFromUrl(string url)
        {
            Uri.TryCreate(url, UriKind.Absolute, out var uriResult);
            return uriResult?.AbsolutePath.Split("/")[2];
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

        public static async Task<SpotifySong[]> GetSongsFromURLs(string[] urls)
        {

            var ids = new string?[urls.Length];
            var i = 0;
            foreach (var url in urls) {
                ids[i] = ExtractIdFromUrl(url);
                i++;
            }

            List<GetSongsResponse.Track> allTracks = [];

            List<List<string?>> batches = [];

            i = 0;
            foreach (var id in ids) {
                if (batches.Count == 0)
                {
                    batches.Add([]);
                    batches[0].Add(id);
                }
                else if (batches[i].Count < 50)
                {
                    batches[i].Add(id);
                } else
                {
                    batches.Add([]);
                    i++;
                    batches[i].Add(id);
                }
            }

            foreach (var batch in batches)
            {
                allTracks.AddRange(JsonSerializer.Deserialize<GetSongsResponse>(await (await SendApiRequest("/tracks", HttpMethod.Get, new Dictionary<string, string>([new KeyValuePair<string, string>("ids", String.Join(",", batch))]))).Content.ReadAsStringAsync())?.tracks ?? []);
                // TODO if error here - more specifically, "HTTP 400 - invalid base62 id" - then the song/album/playlist doesnt exist
            }

            var songs = new SpotifySong[allTracks.Count];

            i = 0;
            foreach (var songData in allTracks)
            {
                songs[i] = new SpotifySong(
                    songData.album.name,
                    songData.artists.Select(artist => artist.name).ToArray(),
                    songData.name,
                    songData.duration_ms,
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

        public static async Task<SpotifySong[]> GetSongsInAlbum(string albumUrl)
        {
            List<string> urls = [];

            await Request(0);
            return await GetSongsFromURLs(urls.ToArray());

            async Task Request(int offset)
            {
                var songsData = JsonSerializer.Deserialize<GetSongsInAlbumResponse>(await (await SendApiRequest("/albums/" + ExtractIdFromUrl(albumUrl) + "/tracks", HttpMethod.Get, new Dictionary<string, string>([new KeyValuePair<string, string>("limit", "50"), new KeyValuePair<string, string>("offset", offset.ToString())]))).Content.ReadAsStringAsync());
                if (songsData != null)
                {
                    urls.AddRange(songsData.items.Select(track => track.external_urls.spotify));

                    if (songsData.next != null)
                    {
                        await Request(offset + 50);
                    }
                }
            }
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

        public static async Task<SpotifySong[]> GetSongsInPlaylist(string playlistUrl)
        {
            List<string> urls = [];

            await Request(0);

            return await GetSongsFromURLs(urls.ToArray());

            async Task Request(int offset)
            {
                var songsData = JsonSerializer.Deserialize<GetSongsInPlaylistResponse>(await (await SendApiRequest("/playlists/" + ExtractIdFromUrl(playlistUrl) + "/tracks", HttpMethod.Get, new Dictionary<string, string>([new KeyValuePair<string, string>("limit", "50"), new KeyValuePair<string, string>("offset", offset.ToString())]))).Content.ReadAsStringAsync());
                
                if (songsData != null)
                {
                    urls.AddRange(songsData.items.Select(playlistTrack => playlistTrack.track.external_urls.spotify));

                    if (songsData.next != null)
                    {
                        await Request(offset + 50);
                    }
                }
            }
        }

    }
}
