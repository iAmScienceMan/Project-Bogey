using System;
using System.IO;
using Bogey.Renderer.Gl;
using Bogey.Shared.Components;
using Bogey.Shared.Tracks;
using Silk.NET.OpenGL;
using Texture = Bogey.Renderer.Gl.Texture;

namespace Bogey.Renderer.Map;

public sealed class EntitySprites : IDisposable
{
    private readonly Texture? _own;
    private readonly Texture? _air;
    private readonly Texture? _surface;
    private readonly Texture? _subsurface;
    private readonly Texture? _unknown;
    private readonly Texture? _munition;

    private EntitySprites(Texture? own, Texture? air, Texture? surface, Texture? subsurface, Texture? unknown, Texture? munition)
    {
        _own = own;
        _air = air;
        _surface = surface;
        _subsurface = subsurface;
        _unknown = unknown;
        _munition = munition;
    }

    public Texture? OwnUnit => _own;

    public Texture? Munition => _munition;

    public static EntitySprites Load(GL gl, string directory)
    {
        return new EntitySprites(
            TryLoad(gl, directory, "own.png"),
            TryLoad(gl, directory, "air.png"),
            TryLoad(gl, directory, "surface.png"),
            TryLoad(gl, directory, "subsurface.png"),
            TryLoad(gl, directory, "unknown.png"),
            TryLoad(gl, directory, "missile.png"));
    }

    public Texture? ForTrack(Track track)
    {
        ContactDomain domain = track.State is TrackState.Detected or TrackState.Dropped
            ? ContactDomain.Unknown
            : track.DomainGuess;

        return domain switch
        {
            ContactDomain.Air => _air ?? _unknown,
            ContactDomain.Surface => _surface ?? _unknown,
            ContactDomain.Subsurface => _subsurface ?? _unknown,
            ContactDomain.Munition => _munition ?? _unknown,
            _ => _unknown,
        };
    }

    public void Dispose()
    {
        _own?.Dispose();
        _air?.Dispose();
        _surface?.Dispose();
        _subsurface?.Dispose();
        _unknown?.Dispose();
        _munition?.Dispose();
    }

    private static Texture? TryLoad(GL gl, string directory, string file)
    {
        string path = Path.Combine(directory, file);
        if (!File.Exists(path))
        {
            return null;
        }

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
