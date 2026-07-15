using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Content.Shared.Net;

namespace Content.Server;

public sealed class HubAdvertiser : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly string _advertiseUrl;

    public HubAdvertiser(string hubUrl)
    {
        string baseUrl = hubUrl.EndsWith('/') ? hubUrl : hubUrl + "/";
        _advertiseUrl = baseUrl + "api/servers/advertise";
    }

    public void Advertise(ServerListing listing) => _ = PostAsync(listing);

    public void Dispose() => _http.Dispose();

    private async Task PostAsync(ServerListing listing)
    {
        try
        {
            await _http.PostAsJsonAsync(_advertiseUrl, listing);
        }
        catch (Exception)
        {
            // Hub unreachable; advertising is best-effort.
        }
    }
}
