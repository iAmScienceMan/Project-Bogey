using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace Lattice.Renderer.Gl;

public sealed class PrimitiveBatch : IDisposable
{
    private const int FloatsPerVertex = 6;       
    private const uint Stride = FloatsPerVertex * sizeof(float);

    private const string VertexSource =
        "#version 330 core\n" +
        "layout(location=0) in vec2 aPos;\n" +
        "layout(location=1) in vec4 aColor;\n" +
        "uniform vec2 uViewport;\n" +
        "out vec4 vColor;\n" +
        "void main(){\n" +
        "  vColor = aColor;\n" +
        "  float x = aPos.x / uViewport.x * 2.0 - 1.0;\n" +
        "  float y = 1.0 - aPos.y / uViewport.y * 2.0;\n" +
        "  gl_Position = vec4(x, y, 0.0, 1.0);\n" +
        "}\n";

    private const string FragmentSource =
        "#version 330 core\n" +
        "in vec4 vColor;\n" +
        "out vec4 FragColor;\n" +
        "void main(){ FragColor = vColor; }\n";

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao;
    private readonly uint _vbo;

    private readonly Dictionary<int, LayerBuffers> _layers = new();

    public int Layer { get; set; }

    public PrimitiveBatch(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexSource, FragmentSource);

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, Stride, (void*)0);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, Stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
        }

        _gl.BindVertexArray(0);
    }

    public void Line(Vector2 a, Vector2 b, Rgba color)
    {
        LayerBuffers buffers = Current();
        Push(buffers.Lines, a, color);
        Push(buffers.Lines, b, color);
    }

    
    public void FilledQuad(Vector2 min, Vector2 max, Rgba color)
    {
        Vector2 tr = new(max.X, min.Y);
        Vector2 bl = new(min.X, max.Y);
        FilledTriangle(min, tr, max, color);
        FilledTriangle(min, max, bl, color);
    }

    public void FilledTriangle(Vector2 a, Vector2 b, Vector2 c, Rgba color)
    {
        LayerBuffers buffers = Current();
        Push(buffers.Triangles, a, color);
        Push(buffers.Triangles, b, color);
        Push(buffers.Triangles, c, color);
    }

    public void FilledCircle(Vector2 center, float radius, Rgba color, int segments = 32)
    {
        float step = MathF.Tau / segments;
        Vector2 prev = center + new Vector2(radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = step * i;
            Vector2 next = center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            FilledTriangle(center, prev, next, color);
            prev = next;
        }
    }

    public void Ring(Vector2 center, float radius, Rgba color, int segments = 48)
    {
        float step = MathF.Tau / segments;
        Vector2 prev = center + new Vector2(radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = step * i;
            Vector2 next = center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            Line(prev, next, color);
            prev = next;
        }
    }

    
    public void DashedRing(Vector2 center, float radius, Rgba color, int segments = 48)
    {
        float step = MathF.Tau / segments;
        for (int i = 0; i < segments; i += 2)
        {
            float a0 = step * i;
            float a1 = step * (i + 1);
            Vector2 p0 = center + new Vector2(MathF.Cos(a0) * radius, MathF.Sin(a0) * radius);
            Vector2 p1 = center + new Vector2(MathF.Cos(a1) * radius, MathF.Sin(a1) * radius);
            Line(p0, p1, color);
        }
    }

    public IEnumerable<int> UsedLayers
    {
        get
        {
            foreach ((int layer, LayerBuffers buffers) in _layers)
            {
                if (buffers.Triangles.Count > 0 || buffers.Lines.Count > 0)
                {
                    yield return layer;
                }
            }
        }
    }

    public void Flush(Vector2 viewport, int layer)
    {
        if (!_layers.TryGetValue(layer, out LayerBuffers? buffers)
            || (buffers.Triangles.Count == 0 && buffers.Lines.Count == 0))
        {
            return;
        }

        _shader.Use();
        _shader.SetUniform("uViewport", viewport.X, viewport.Y);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        Draw(buffers.Triangles, PrimitiveType.Triangles);
        Draw(buffers.Lines, PrimitiveType.Lines);

        _gl.BindVertexArray(0);
        buffers.Triangles.Clear();
        buffers.Lines.Clear();
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
    }

    private void Draw(List<float> data, PrimitiveType mode)
    {
        if (data.Count == 0)
        {
            return;
        }

        ReadOnlySpan<float> span = CollectionsMarshal.AsSpan(data);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, span, BufferUsageARB.DynamicDraw);
        _gl.DrawArrays(mode, 0, (uint)(data.Count / FloatsPerVertex));
    }

    private static void Push(List<float> data, Vector2 position, Rgba color)
    {
        data.Add(position.X);
        data.Add(position.Y);
        data.Add(color.R);
        data.Add(color.G);
        data.Add(color.B);
        data.Add(color.A);
    }

    private LayerBuffers Current()
    {
        if (!_layers.TryGetValue(Layer, out LayerBuffers? buffers))
        {
            buffers = new LayerBuffers();
            _layers[Layer] = buffers;
        }

        return buffers;
    }

    private sealed class LayerBuffers
    {
        public List<float> Triangles { get; } = new();

        public List<float> Lines { get; } = new();
    }
}
