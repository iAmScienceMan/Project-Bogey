using System;
using Silk.NET.OpenGL;

namespace Bogey.Renderer.Gl;

public sealed class Shader : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;

    public Shader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        uint vert = Compile(ShaderType.VertexShader, vertexSource);
        uint frag = Compile(ShaderType.FragmentShader, fragmentSource);

        _handle = _gl.CreateProgram();
        _gl.AttachShader(_handle, vert);
        _gl.AttachShader(_handle, frag);
        _gl.LinkProgram(_handle);

        _gl.GetProgram(_handle, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = _gl.GetProgramInfoLog(_handle);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }

        
        _gl.DetachShader(_handle, vert);
        _gl.DetachShader(_handle, frag);
        _gl.DeleteShader(vert);
        _gl.DeleteShader(frag);
    }

    public void Use() => _gl.UseProgram(_handle);

    public void SetUniform(string name, float x, float y)
    {
        int location = Location(name);
        _gl.Uniform2(location, x, y);
    }

    public void SetUniform(string name, int value)
    {
        int location = Location(name);
        _gl.Uniform1(location, value);
    }

    public void Dispose() => _gl.DeleteProgram(_handle);

    private int Location(string name)
    {
        int location = _gl.GetUniformLocation(_handle, name);
        if (location < 0)
        {
            throw new InvalidOperationException($"Uniform '{name}' not found in shader program.");
        }

        return location;
    }

    private uint Compile(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"{type} compile failed: {log}");
        }

        return shader;
    }
}
