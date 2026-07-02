using System.Collections.Generic;
using System.Numerics;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;

namespace Bogey.Renderer.Ui.Controls;


public enum UiAnchor
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    TopCenter,
    BottomCenter,
    Center,
}

public class Control
{
    private readonly List<Control> _children = new();

    public string? Name { get; set; }

    public bool Visible { get; set; } = true;

    public UiAnchor Anchor { get; set; } = UiAnchor.TopLeft;

    public Thickness Margin { get; set; }

    public UiRect Bounds { get; protected set; }

    public IReadOnlyList<Control> Children => _children;

    public void AddChild(Control child) => _children.Add(child);

    public void ClearChildren() => _children.Clear();

    
    public virtual Vector2 Measure() => Vector2.Zero;

    
    public virtual void Arrange(UiRect rect)
    {
        Bounds = rect;
        foreach (Control child in _children)
        {
            if (!child.Visible)
            {
                continue;
            }

            child.Arrange(AnchorWithin(rect, child.Anchor, child.Margin, child.Measure()));
        }
    }

    public virtual void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        foreach (Control child in _children)
        {
            child.Draw(prims, text);
        }
    }

    
    public virtual Button? HitTest(Vector2 point)
    {
        if (!Visible)
        {
            return null;
        }

        for (int i = _children.Count - 1; i >= 0; i--)
        {
            Button? hit = _children[i].HitTest(point);
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    public virtual Control? HitTestFocusable(Vector2 point)
    {
        if (!Visible)
        {
            return null;
        }

        for (int i = _children.Count - 1; i >= 0; i--)
        {
            Control? hit = _children[i].HitTestFocusable(point);
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    protected virtual bool IsOpaque => false;

    public Control? HitTestOpaque(Vector2 point)
    {
        if (!Visible)
        {
            return null;
        }

        for (int i = _children.Count - 1; i >= 0; i--)
        {
            Control? hit = _children[i].HitTestOpaque(point);
            if (hit is not null)
            {
                return hit;
            }
        }

        return IsOpaque && Bounds.Contains(point) ? this : null;
    }

    
    public virtual void FrameUpdate(float dt)
    {
        foreach (Control child in _children)
        {
            child.FrameUpdate(dt);
        }
    }

    
    public IEnumerable<Control> SelfAndDescendants()
    {
        yield return this;
        foreach (Control child in _children)
        {
            foreach (Control node in child.SelfAndDescendants())
            {
                yield return node;
            }
        }
    }

    protected static UiRect AnchorWithin(UiRect container, UiAnchor anchor, Thickness margin, Vector2 size)
    {
        float left = container.X + margin.Left;
        float right = container.Right - size.X - margin.Right;
        float centerX = container.X + ((container.W - size.X) * 0.5f);
        float top = container.Y + margin.Top;
        float bottom = container.Bottom - size.Y - margin.Bottom;
        float centerY = container.Y + ((container.H - size.Y) * 0.5f);

        (float x, float y) = anchor switch
        {
            UiAnchor.TopLeft => (left, top),
            UiAnchor.TopRight => (right, top),
            UiAnchor.BottomLeft => (left, bottom),
            UiAnchor.BottomRight => (right, bottom),
            UiAnchor.TopCenter => (centerX, top),
            UiAnchor.BottomCenter => (centerX, bottom),
            UiAnchor.Center => (centerX, centerY),
            _ => (left, top),
        };

        return new UiRect(x, y, size.X, size.Y);
    }
}
