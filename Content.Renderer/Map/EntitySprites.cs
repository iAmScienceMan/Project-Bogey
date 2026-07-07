using System;
using System.Collections.Generic;
using System.IO;
using Lattice.Renderer.Gl;
using Content.Shared.Components;
using Content.Shared.Tracks;
using Silk.NET.OpenGL;
using Texture = Lattice.Renderer.Gl.Texture;

namespace Content.Renderer.Map;

public sealed class EntitySprites : IDisposable
{
    private readonly Dictionary<string, Texture> _textures;

    private EntitySprites(Dictionary<string, Texture> textures)
    {
        _textures = textures;
    }

    public Texture? Munition => Get("missile.png");

    public static EntitySprites Load(GL gl, string directory)
    {
        Dictionary<string, Texture> textures = new(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(directory))
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*.png"))
            {
                Texture? texture = TryLoad(gl, path);
                if (texture is not null)
                {
                    textures[Path.GetFileName(path)] = texture;
                }
            }
        }

        return new EntitySprites(textures);
    }

    public Texture? Get(string? key)
        => !string.IsNullOrEmpty(key) && _textures.TryGetValue(key, out Texture? texture) ? texture : null;

    public Texture? ForTrack(Track track)
    {
        ContactDomain domain = track.State is TrackState.Detected or TrackState.Dropped
            ? ContactDomain.Unknown
            : track.DomainGuess;

        return domain switch
        {
            ContactDomain.Air => Get("air.png") ?? Get("unknown.png"),
            ContactDomain.Surface => Get("surface.png") ?? Get("unknown.png"),
            ContactDomain.Subsurface => Get("subsurface.png") ?? Get("unknown.png"),
            ContactDomain.Munition => Get("missile.png") ?? Get("unknown.png"),
            _ => Get("unknown.png"),
        };
    }

    public void Dispose()
    {
        foreach (Texture texture in _textures.Values)
        {
            texture.Dispose();
        }
    }

    private static Texture? TryLoad(GL gl, string path)
    {
        try
        {
            return Texture.FromFile(gl, path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            Console.Error.WriteLine($"Failed to load sprite '{path}': {ex.Message}");
            return null;
        }
    }
}
