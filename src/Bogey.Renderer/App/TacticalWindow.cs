using System;
using System.Collections.Generic;
using System.Numerics;
using Bogey.Renderer.Camera;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Map;
using Bogey.Renderer.RealTime;
using Bogey.Renderer.Text;
using Bogey.Renderer.Ui;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Screens;
using Bogey.Shared.Commands;
using Bogey.Shared.Tracks;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Button = Bogey.Renderer.Ui.Controls.Button;

namespace Bogey.Renderer.App;

public sealed class TacticalWindow : IDisposable
{
    private const float ClickThresholdPx = 5f;   
    private const float UnitPickRadiusPx = 16f;   
    private const float OrderArriveKm = 2f;       

    private readonly RendererOptions _options;
    private readonly ISimSession _session;
    private readonly IDebugOverlay? _debugOverlay;
    private readonly Dictionary<string, Vector2> _pendingOrders = new(StringComparer.Ordinal);

    private IWindow? _window;
    private GL _gl = null!;
    private IInputContext _input = null!;
    private PrimitiveBatch _prims = null!;
    private SpriteBatch _sprites = null!;
    private EntitySprites _entitySprites = null!;
    private TextBatch _text = null!;
    private BitmapFont _font = null!;
    private TacticalMapRenderer _map = null!;
    private Camera2D _camera = null!;
    private TacticalHud _hud = null!;

    private Vector2 _lastMousePx;
    private Vector2 _leftDownPx;
    private bool _leftDown;
    private bool _dragged;
    private bool _uiCaptured;
    private Button? _pressedButton;
    private Button? _hoveredButton;
    private string? _selectedUnit;

    public TacticalWindow(RendererOptions options, ISimSession session, IDebugOverlay? debugOverlay = null)
    {
        _options = options;
        _session = session;
        _debugOverlay = debugOverlay;
    }

    public void Run()
    {
        WindowOptions windowOptions = WindowOptions.Default with
        {
            Size = new Vector2D<int>(_options.Width, _options.Height),
            Title = _options.Title,
            VSync = true,
            API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3)),
        };

        _window = Window.Create(windowOptions);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
        _window.Run();
    }

    public void Dispose() => _window?.Dispose();

    private void OnLoad()
    {
        IWindow window = _window ?? throw new InvalidOperationException("Window is not initialized.");
        _gl = window.CreateOpenGL();
        _input = window.CreateInput();

        _gl.ClearColor(0.04f, 0.06f, 0.09f, 1f);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _font = new BitmapFont(_gl);
        _prims = new PrimitiveBatch(_gl);
        _sprites = new SpriteBatch(_gl);
        _entitySprites = EntitySprites.Load(_gl, _options.SpritesPath);
        _text = new TextBatch(_gl, _font);
        _map = new TacticalMapRenderer();

        Vector2 framebuffer = Framebuffer();
        _camera = new Camera2D(framebuffer, Vector2.Zero, _options.InitialZoomPxPerKm);

        _hud = new TacticalHud(_session, _debugOverlay, Recenter);

        foreach (IMouse mouse in _input.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnScroll;
        }

        foreach (IKeyboard keyboard in _input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
        }
    }

    private void OnRender(double deltaSeconds)
    {
        _session.Advance(deltaSeconds);
        PruneArrivedOrders();

        Vector2 viewport = Framebuffer();
        _camera.ResizeViewport(viewport);

        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        _map.Draw(_session, _camera, _prims, _sprites, _entitySprites, _text, (float)deltaSeconds, _selectedUnit, _pendingOrders);

        _debugOverlay?.Draw(_prims, _text, _camera, viewport);

        _hud.SelectedUnit = _selectedUnit;
        _hud.HoveredButton = _hoveredButton;
        _hud.FrameUpdate((float)deltaSeconds);
        _hud.Arrange(new UiRect(0f, 0f, viewport.X, viewport.Y));
        _hud.Draw(_prims, _text);

        _sprites.Flush(viewport);
        _prims.Flush(viewport);
        _text.Flush(viewport);
    }

    
    private void PruneArrivedOrders()
    {
        TrackPictureSnapshot? current = _session.Current;
        if (current is null || _pendingOrders.Count == 0)
        {
            return;
        }

        List<string>? arrived = null;
        foreach ((string name, Vector2 destination) in _pendingOrders)
        {
            foreach (OwnUnitView unit in current.OwnUnits)
            {
                if (string.Equals(unit.Name, name, StringComparison.Ordinal)
                    && Vector2.Distance(unit.Position, destination) <= OrderArriveKm)
                {
                    (arrived ??= new List<string>()).Add(name);
                    break;
                }
            }
        }

        if (arrived is not null)
        {
            foreach (string name in arrived)
            {
                _pendingOrders.Remove(name);
            }
        }
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left)
        {
            return;
        }

        Vector2 px = ToFramebuffer(mouse.Position);

        Control? overUi = _hud.HitTestOpaque(px);
        if (overUi is not null)
        {
            _uiCaptured = true;
            if (overUi is Button uiButton)
            {
                _pressedButton = uiButton;
                uiButton.IsPressed = true;
            }

            return;
        }

        _uiCaptured = false;
        _leftDown = true;
        _dragged = false;
        _leftDownPx = px;
        _lastMousePx = px;
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        Vector2 px = ToFramebuffer(mouse.Position);

        if (button == MouseButton.Right)
        {
            if (_hud.HitTestOpaque(px) is null)
            {
                _debugOverlay?.HandleClick(px, _camera);
            }

            return;
        }

        if (button != MouseButton.Left)
        {
            return;
        }

        if (_uiCaptured)
        {
            if (_pressedButton is not null)
            {
                _pressedButton.IsPressed = false;
                if (ReferenceEquals(_hud.HitTestOpaque(px), _pressedButton))
                {
                    _pressedButton.Press();
                }

                _pressedButton = null;
            }

            _uiCaptured = false;
            return;
        }

        _leftDown = false;
        if (!_dragged)
        {
            HandleClick(px);
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        Vector2 px = ToFramebuffer(position);
        UpdateHover(px);

        if (_leftDown)
        {
            if (Vector2.Distance(px, _leftDownPx) > ClickThresholdPx)
            {
                _dragged = true;
            }

            if (_dragged)
            {
                _camera.Pan(px - _lastMousePx);
            }
        }

        _lastMousePx = px;
    }

    private void OnScroll(IMouse mouse, ScrollWheel wheel)
    {
        if (wheel.Y == 0f)
        {
            return;
        }

        Vector2 px = ToFramebuffer(mouse.Position);
        if (_hud.HitTestOpaque(px) is not null)
        {
            return; 
        }

        float factor = MathF.Pow(1.15f, wheel.Y);
        _camera.ZoomAt(factor, px);
    }

    private void UpdateHover(Vector2 px)
    {
        Button? over = _hud.HitTest(px);
        if (!ReferenceEquals(_hoveredButton, over))
        {
            if (_hoveredButton is not null)
            {
                _hoveredButton.IsHovered = false;
            }

            _hoveredButton = over;
            if (over is not null)
            {
                over.IsHovered = true;
            }
        }

        if (_pressedButton is not null)
        {
            _pressedButton.IsPressed = _pressedButton.Bounds.Contains(px);
        }
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        switch (key)
        {
            case Key.Space:
                _session.SetSpeed(_session.Speed == SimSpeed.Paused ? SimSpeed.Normal : SimSpeed.Paused);
                break;
            case Key.Number1:
            case Key.Keypad1:
                _session.SetSpeed(SimSpeed.Normal);
                break;
            case Key.Number2:
            case Key.Keypad2:
                _session.SetSpeed(SimSpeed.Fast);
                break;
            case Key.C:
                Recenter();
                break;
            case Key.G:
                
                _debugOverlay?.CycleDisplay();
                break;
            case Key.Escape:
            case Key.Q:
                _window?.Close();
                break;
        }
    }

    private void HandleClick(Vector2 px)
    {
        string? hit = PickOwnUnit(px);
        if (hit is not null)
        {
            _selectedUnit = hit;
            return;
        }

        if (_selectedUnit is null)
        {
            return;
        }

        Vector2 destination = _camera.ScreenToWorld(px);
        _session.Enqueue(new MoveCommand { UnitName = _selectedUnit, Destination = destination });
        _pendingOrders[_selectedUnit] = destination;
    }

    private void Recenter()
    {
        TrackPictureSnapshot? current = _session.Current;
        if (current is not null && current.OwnUnits.Count > 0)
        {
            _camera.SetCenter(current.OwnUnits[0].Position);
        }
    }

    private string? PickOwnUnit(Vector2 px)
    {
        string? best = null;
        float bestDistance = UnitPickRadiusPx;

        foreach ((OwnUnitView unit, Vector2 worldPos) in Interp.OwnUnits(_session))
        {
            Vector2 screen = _camera.WorldToScreen(worldPos);
            float distance = Vector2.Distance(screen, px);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = unit.Name;
            }
        }

        return best;
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _gl.Viewport(0, 0, (uint)Math.Max(1, size.X), (uint)Math.Max(1, size.Y));
    }

    private void OnClosing()
    {
        _prims.Dispose();
        _sprites.Dispose();
        _entitySprites.Dispose();
        _text.Dispose();
        _font.Dispose();
        _input.Dispose();
        _gl.Dispose();
    }

    private Vector2 Framebuffer()
    {
        IWindow window = _window ?? throw new InvalidOperationException("Window is not initialized.");
        Vector2D<int> fb = window.FramebufferSize;
        return new Vector2(Math.Max(1, fb.X), Math.Max(1, fb.Y));
    }

    private Vector2 ToFramebuffer(Vector2 windowPosition)
    {
        IWindow window = _window ?? throw new InvalidOperationException("Window is not initialized.");
        Vector2D<int> win = window.Size;
        Vector2D<int> fb = window.FramebufferSize;
        float sx = win.X > 0 ? (float)fb.X / win.X : 1f;
        float sy = win.Y > 0 ? (float)fb.Y / win.Y : 1f;
        return new Vector2(windowPosition.X * sx, windowPosition.Y * sy);
    }
}
