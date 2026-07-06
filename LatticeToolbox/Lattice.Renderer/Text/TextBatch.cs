using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Lattice.Renderer.Gl;
using Silk.NET.OpenGL;

namespace Lattice.Renderer.Text;

public sealed class TextBatch : IDisposable
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
        "uniform sampler2D uFont;\n" +
        "void main(){\n" +
        "  float a = texture(uFont, vUV).r;\n" +
        "  FragColor = vec4(vColor.rgb, vColor.a * a);\n" +
        "}\n";

    private readonly GL _gl;
    private readonly Gl.Shader _shader;
    private readonly IFont _font;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly Dictionary<int, List<float>> _layers = new();

    public int Layer { get; set; }

    public static IFont? ActiveFont { get; private set; }

    public static float PixelScale { get; set; } = 1f;

    public TextBatch(GL gl, IFont font)
    {
        _gl = gl;
        _font = font;
        ActiveFont = font;
        _shader = new Gl.Shader(gl, VertexSource, FragmentSource);

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

    public static float Measure(string text, float pixelSize)
        => text.Length * CharWidth(pixelSize);

    public static float CharWidth(float pixelSize)
        => ActiveFont is { } font ? font.AdvancePx(Round(pixelSize * PixelScale)) / PixelScale : pixelSize;

    public void Text(Vector2 origin, float pixelSize, Rgba color, string text)
    {
        float scale = PixelScale;
        float inv = 1f / scale;
        int size = Round(pixelSize * scale);

        List<float> data = Current();
        float penX = origin.X;
        float baseline = origin.Y + (_font.Ascent(size) * inv);

        foreach (char c in text)
        {
            Glyph glyph = _font.GetGlyph(c, size);
            if (glyph.HasPixels)
            {
                float x0 = penX + (glyph.BearingLeft * inv);
                float y0 = baseline - (glyph.BearingTop * inv);
                float x1 = x0 + (glyph.Width * inv);
                float y1 = y0 + (glyph.Height * inv);
                Vector4 uv = glyph.Uv;

                PushVertex(data, x0, y0, uv.X, uv.Y, color);
                PushVertex(data, x1, y0, uv.Z, uv.Y, color);
                PushVertex(data, x1, y1, uv.Z, uv.W, color);

                PushVertex(data, x0, y0, uv.X, uv.Y, color);
                PushVertex(data, x1, y1, uv.Z, uv.W, color);
                PushVertex(data, x0, y1, uv.X, uv.W, color);
            }

            penX += glyph.Advance * inv;
        }
    }

    private static int Round(float pixelSize) => Math.Max(1, (int)MathF.Round(pixelSize));

    public IEnumerable<int> UsedLayers
    {
        get
        {
            foreach ((int layer, List<float> data) in _layers)
            {
                if (data.Count > 0)
                {
                    yield return layer;
                }
            }
        }
    }

    public void Flush(Vector2 viewport, int layer)
    {
        if (!_layers.TryGetValue(layer, out List<float>? data) || data.Count == 0)
        {
            return;
        }

        _shader.Use();
        _shader.SetUniform("uViewport", viewport.X, viewport.Y);
        _shader.SetUniform("uFont", 0);
        _font.Bind(TextureUnit.Texture0);

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        ReadOnlySpan<float> span = CollectionsMarshal.AsSpan(data);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, span, BufferUsageARB.DynamicDraw);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(data.Count / FloatsPerVertex));

        _gl.BindVertexArray(0);
        data.Clear();
    }

    public void Dispose()
    {
        _shader.Dispose();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
    }

    private List<float> Current()
    {
        if (!_layers.TryGetValue(Layer, out List<float>? data))
        {
            data = new List<float>();
            _layers[Layer] = data;
        }

        return data;
    }

    private static void PushVertex(List<float> data, float x, float y, float u, float v, Rgba color)
    {
        data.Add(x);
        data.Add(y);
        data.Add(u);
        data.Add(v);
        data.Add(color.R);
        data.Add(color.G);
        data.Add(color.B);
        data.Add(color.A);
    }
}
