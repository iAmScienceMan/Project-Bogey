using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Content.Shared.Net;

namespace Content.Renderer.App;

public sealed class HubClient : IDisposable
{
    private static readonly ServerListing[] Empty = Array.Empty<ServerListing>();

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly string _serversUrl;
    private readonly object _lock = new();
    private ServerListing[]? _pending;

    public HubClient(string hubUrl)
    {
        string baseUrl = hubUrl.EndsWith('/') ? hubUrl : hubUrl + "/";
        _serversUrl = baseUrl + "api/servers";
    }

    public void Refresh() => _ = FetchAsync();

    public bool Poll(out IReadOnlyList<ServerListing> servers)
    {
        lock (_lock)
        {
            if (_pending is null)
            {
                servers = Empty;
                return false;
            }

            servers = _pending;
            _pending = null;
            return true;
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task FetchAsync()
    {
        ServerListing[] result;
        try
        {
            result = await _http.GetFromJsonAsync<ServerListing[]>(_serversUrl) ?? Empty;
        }
        catch (Exception)
        {
            result = Empty;
        }

        lock (_lock)
        {
            _pending = result;
        }
    }
}
