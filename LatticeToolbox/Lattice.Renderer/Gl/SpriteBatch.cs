using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace Lattice.Renderer.Gl;

public sealed class SpriteBatch : IDisposable
{
    private const int FloatsPerVertex = 8;
    private const uint Stride = FloatsPerVertex * sizeof(float);

    private const string VertexSource =
        "#version 330 core\n" +
        "layout(location=0) in vec2 aPos;\n" +
        "layout(location=1) in vec2 aUV;\n" +
        "layout(location=2) in vec4 aColor;\n" +
        "uniform vec2 uViewport;\n" +
        "out vec2 vUV;\n" +
        "out vec4 vColor;\n" +
        "void main(){\n" +
        "  vUV = aUV;\n" +
        "  vColor = aColor;\n" +
        "  float x = aPos.x / uViewport.x * 2.0 - 1.0;\n" +
        "  float y = 1.0 - aPos.y / uViewport.y * 2.0;\n" +
        "  gl_Position = vec4(x, y, 0.0, 1.0);\n" +
        "}\n";

    private const string FragmentSource =
        "#version 330 core\n" +
        "in vec2 vUV;\n" +
        "in vec4 vColor;\n" +
        "out vec4 FragColor;\n" +
        "uniform sampler2D uTex;\n" +
        "void main(){ FragColor = texture(uTex, vUV) * vColor; }\n";

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly Dictionary<Texture, List<float>> _batches = new();

    public SpriteBatch(GL gl)
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
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, Stride, (void*)(4 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
        }

        _gl.BindVertexArray(0);
    }

    public void Draw(Texture texture, Vector2 center, float sizePx, Rgba tint)
        => Draw(texture, center, new Vector2(sizePx, sizePx), tint);

    public void Draw(Texture texture, Vector2 center, Vector2 sizePx, Rgba tint)
    {
        if (!_batches.TryGetValue(texture, out List<float>? vertices))
        {
            vertices = new List<float>();
            _batches[texture] = vertices;
        }

        float x0 = center.X - (sizePx.X * 0.5f);
        float y0 = center.Y - (sizePx.Y * 0.5f);
        float x1 = center.X + (sizePx.X * 0.5f);
        float y1 = center.Y + (sizePx.Y * 0.5f);

        PushVertex(vertices, x0, y0, 0f, 0f, tint);
        PushVertex(vertices, x1, y0, 1f, 0f, tint);
        PushVertex(vertices, x1, y1, 1f, 1f, tint);

        PushVertex(vertices, x0, y0, 0f, 0f, tint);
        PushVertex(vertices, x1, y1, 1f, 1f, tint);
        PushVertex(vertices, x0, y1, 0f, 1f, tint);
    }

    public void Draw(Texture texture, Vector2 center, Vector2 sizePx, Rgba tint, float rotationRadians)
    {
        if (!_batches.TryGetValue(texture, out List<float>? vertices))
        {
            vertices = new List<float>();
            _batches[texture] = vertices;
        }

        float hx = sizePx.X * 0.5f;
        float hy = sizePx.Y * 0.5f;
        float cos = MathF.Cos(rotationRadians);
        float sin = MathF.Sin(rotationRadians);

        Vector2 Corner(float ox, float oy)
            => new(center.X + (ox * cos) - (oy * sin), center.Y + (ox * sin) + (oy * cos));

        Vector2 topLeft = Corner(-hx, -hy);
        Vector2 topRight = Corner(hx, -hy);
        Vector2 bottomRight = Corner(hx, hy);
        Vector2 bottomLeft = Corner(-hx, hy);

        PushVertex(vertices, topLeft.X, topLeft.Y, 0f, 0f, tint);
        PushVertex(vertices, topRight.X, topRight.Y, 1f, 0f, tint);
        PushVertex(vertices, bottomRight.X, bottomRight.Y, 1f, 1f, tint);

        PushVertex(vertices, topLeft.X, topLeft.Y, 0f, 0f, tint);
        PushVertex(vertices, bottomRight.X, bottomRight.Y, 1f, 1f, tint);
        PushVertex(vertices, bottomLeft.X, bottomLeft.Y, 0f, 1f, tint);
    }

    public void Flush(Vector2 viewport)
    {
        if (_batches.Count == 0)
        {
            return;
        }

        _shader.Use();
        _shader.SetUniform("uViewport", viewport.X, viewport.Y);
        _shader.SetUniform("uTex", 0);

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        foreach ((Texture texture, List<float> vertices) in _batches)
        {
            if (vertices.Count == 0)
            {
                continue;
            }

            texture.Bind(TextureUnit.Texture0);
            ReadOnlySpan<float> span = CollectionsMarshal.AsSpan(vertices);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, span, BufferUsageARB.DynamicDraw);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(vertices.Count / FloatsPerVertex));
            vertices.Clear();
        }

        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
    }

    private static void PushVertex(List<float> vertices, float x, float y, float u, float v, Rgba color)
    {
        vertices.Add(x);
        vertices.Add(y);
        vertices.Add(u);
        vertices.Add(v);
        vertices.Add(color.R);
        vertices.Add(color.G);
        vertices.Add(color.B);
        vertices.Add(color.A);
    }
}
