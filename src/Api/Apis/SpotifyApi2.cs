using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Downloader.Utils;
using OtpNet;
using SoftCircuits.HtmlMonkey;

namespace Downloader.Api.Apis;

public class SpotifyApi2 : ISongDataSource
{
    
    private string? _accessToken = null; 
    private string? _clientToken = null;

    public async Task Init()
    {
        
        var cookies = new CookieContainer();
        var handler = new HttpClientHandler();
        handler.CookieContainer = cookies;
        
        var httpClient = new HttpClient(handler);
        
        var response = await httpClient.GetAsync("https://open.spotify.com");
        var data = await response.Content.ReadAsStringAsync();
        var document = HtmlDocument.FromHtml(data);

        var script = document.FindOfType<HtmlElementNode>(e => e.TagName == "script" && e.Attributes.Contains("src"))
            .Select(e => e.Attributes.TryGetValue("src", out var src) ? src.Value : null)
            .Where(s => s != null && s.Contains("web-player/web-player") && s.EndsWith(".js")).ToList().First();

        var serverConfig = JsonNode.Parse(Convert.FromBase64String(
            data.Split("<script id=\"appServerConfig\" type=\"text/plain\">")[1].Split("</script>")[0]));

        if (serverConfig == null)
        {
            throw new Exception("Failed to init Spotify API - serverConfig was null");
        }

        var clientVersion = serverConfig["clientVersion"]?.ToString();
        var deviceId = cookies.GetCookies(new Uri("https://open.spotify.com"))
            .Where(c => c.Name == "sp_t")
            .Select(c => c.Value)
            .FirstOrDefault() ?? "";

        response = await httpClient.GetAsync(
            "https://code.thetadev.de/ThetaDev/spotify-secrets/raw/branch/main/secrets/secretDict.json");

        var secretsJson = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var totpVersion = secretsJson?.AsObject().ToList()
            .Select(p => Int32.Parse(p.Key)).Max();
        var totpSecret = secretsJson?[totpVersion?.ToString() ?? ""]?.AsArray()
            .Where(i => i != null)
            .Select(i => i.GetValue<int>())
            .ToArray();

        if (totpSecret == null)
        {
            throw new Exception("Failed to init Spotify API - totpSecret was null");
        }
        
        var newTotpSecret = new int[totpSecret.Length];
        var i = 0;
        foreach (var number in totpSecret)
        {
            newTotpSecret[i] = number ^ (i % 33 + 9);
            i++;
        }
        
        var totp = new Totp(newTotpSecret.Select(n => (byte) n).ToArray());
        var totpGenerated = totp.ComputeTotp();
        
        response = await httpClient.GetAsync("https://open.spotify.com/api/token" +
            "?reason=init" +
            "&productType=web-player" + 
            "&totp=" + totpGenerated +
            "&totpVer=" + totpVersion + 
            "&totpServer=" + totpGenerated);

        var authData = JsonNode.Parse(await response.Content.ReadAsStringAsync());

        if (authData == null)
        {
            throw new Exception("Failed to init Spotify API - authData was null");
        }

        _accessToken = authData["accessToken"]?.ToString();
        var clientId = authData["clientId"]?.ToString();

        response = await httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post, RequestUri = new Uri("https://clienttoken.spotify.com/v1/clienttoken"), Content = new StringContent( @"{ ""client_data"": { ""client_version""" + clientVersion + @""", ""client_id"": """ + clientId + @""", ""js_sdk_data"": { ""device_brand"": ""unknown"", ""device_model"": ""unknown"", ""os"": ""windows"", ""os_version"": ""NT 10.0"", ""device_id"": """ + deviceId + @""", ""device_type"": ""computer"" } } }", Encoding.UTF8, "application/json"), Headers =
            {
                { "Authority", "clienttoken.spotify.com" },
                { "Content-Type", "application/json" },
                { "Accept", "application/json" }
            }
        });

        var tokenData = JsonNode.Parse(await response.Content.ReadAsStringAsync());

        if (tokenData?["response_type"]?.ToString() != "RESPONSE_GRANTED_TOKEN_RESPONSE")
        {
            throw new Exception("Failed to init Spotify API - token was not granted - actual response: " + tokenData);
        }

        _clientToken = tokenData["granted_token"]?["token"]?.ToString();

        if (_clientToken == null)
        {
            throw new Exception("Failed to init Spotify API - _clientToken was null");
        }

    }

    public string GetName()
    {
        return "Spotify";
    }

    public string GetId()
    {
        return "spotify";
    }

    public bool UrlPartOfPlatform(string url)
    {
        throw new System.NotImplementedException();
    }

    public Task<Song[]> GetSongs(string url)
    {
        throw new System.NotImplementedException();
    }
    
}