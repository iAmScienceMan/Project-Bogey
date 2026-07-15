using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Shared.Net;

namespace Content.Hub;

public sealed class Program
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<string, Entry> Servers = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> Main(string[] args)
    {
        string prefix = args.Length > 0 ? args[0] : "http://localhost:8080/";
        if (!prefix.EndsWith('/'))
        {
            prefix += "/";
        }

        HttpListener listener = new();
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            Console.Error.WriteLine($"Failed to bind {prefix}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Bogey hub listening on {prefix} - GET api/servers, POST api/servers/advertise.");

        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context));
        }

        return 0;
    }

    private static async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            string path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;

            if (context.Request.HttpMethod == "GET" && path == "/api/servers")
            {
                await WriteJsonAsync(context, ActiveServers());
            }
            else if (context.Request.HttpMethod == "POST" && path == "/api/servers/advertise")
            {
                await HandleAdvertiseAsync(context);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            Console.Error.WriteLine($"Request error: {ex.Message}");
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static async Task HandleAdvertiseAsync(HttpListenerContext context)
    {
        ServerListing? listing;
        try
        {
            listing = await JsonSerializer.DeserializeAsync<ServerListing>(context.Request.InputStream, JsonOptions);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            return;
        }

        if (listing is null || string.IsNullOrWhiteSpace(listing.Address))
        {
            context.Response.StatusCode = 400;
            return;
        }

        Servers[listing.Address] = new Entry(listing, DateTime.UtcNow);
        context.Response.StatusCode = 204;
    }

    private static ServerListing[] ActiveServers()
    {
        DateTime cutoff = DateTime.UtcNow - Ttl;

        foreach ((string key, Entry entry) in Servers)
        {
            if (entry.SeenAt < cutoff)
            {
                Servers.TryRemove(key, out _);
            }
        }

        return Servers.Values
            .Where(e => e.SeenAt >= cutoff)
            .Select(e => e.Listing)
            .ToArray();
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, object body)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
    }

    private readonly record struct Entry(ServerListing Listing, DateTime SeenAt);
}
