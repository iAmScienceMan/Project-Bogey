using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Bogey.Logging;
using Bogey.Renderer.Camera;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Map;
using Bogey.Renderer.RealTime;
using Bogey.Renderer.Text;
using Bogey.Renderer.Ui;
using Bogey.Renderer.Ui.Console;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Screens;
using Bogey.Shared.Changelog;
using Bogey.Shared.Commands;
using Bogey.Shared.Configuration;
using Bogey.Shared.Console;
using Bogey.Shared.Tracks;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Button = Bogey.Renderer.Ui.Controls.Button;

namespace Bogey.Renderer.App;

public sealed class TacticalWindow : IDisposable, IAppControl
{
    private const float ClickThresholdPx = 5f;
    private const float UnitPickRadiusPx = 16f;
    private const float OrderArriveKm = 2f;

    private enum Screen
    {
        MainMenu,
        Options,
        Changelog,
        Tactical,
    }

    private readonly RendererOptions _options;
    private readonly IConfigurationManager _cfg;
    private readonly IChangelogManager _changelog;
    private readonly SimBootFactory _sessionFactory;
    private readonly SimConsoleContext _consoleContext = new();
    private readonly Dictionary<string, Vector2> _pendingOrders = new(StringComparer.Ordinal);

    private IWindow? _window;
    private GL _gl = null!;
    private IInputContext _input = null!;
    private PrimitiveBatch _prims = null!;
    private SpriteBatch _sprites = null!;
    private EntitySprites _entitySprites = null!;
    private TextBatch _text = null!;
    private IFont _font = null!;
    private DevConsole _console = null!;
    private MainMenuScreen _mainMenu = null!;
    private OptionsScreen _optionsScreen = null!;
    private ChangelogScreen _changelogScreen = null!;

    private ISimSession? _session;
    private IDebugOverlay? _debugOverlay;
    private TacticalMapRenderer? _map;
    private Camera2D? _camera;
    private TacticalHud? _hud;

    private Screen _screen = Screen.MainMenu;

    private Vector2 _lastMousePx;
    private Vector2 _leftDownPx;
    private bool _leftDown;
    private bool _dragged;
    private bool _uiCaptured;
    private Button? _pressedButton;
    private Button? _hoveredButton;
    private LineEdit? _focused;
    private string? _selectedUnit;

    public TacticalWindow(
        RendererOptions options,
        IConfigurationManager cfg,
        IChangelogManager changelog,
        SimBootFactory sessionFactory)
    {
        _options = options;
        _cfg = cfg;
        _changelog = changelog;
        _sessionFactory = sessionFactory;
    }

    public void Run()
    {
        WindowOptions windowOptions = WindowOptions.Default with
        {
            Size = new Vector2D<int>(_cfg.GetCVar(CVars.RenderWidth), _cfg.GetCVar(CVars.RenderHeight)),
            Title = _options.Title,
            VSync = _cfg.GetCVar(CVars.RenderVsync),
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

    public void Quit() => _window?.Close();

    private void OnLoad()
    {
        IWindow window = _window ?? throw new InvalidOperationException("Window is not initialized.");
        _gl = window.CreateOpenGL();
        _input = window.CreateInput();

        _gl.ClearColor(0.04f, 0.06f, 0.09f, 1f);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _font = new FreeTypeFont(_gl, _cfg.GetCVar(CVars.RenderFontPath));
        _prims = new PrimitiveBatch(_gl);
        _sprites = new SpriteBatch(_gl);
        _entitySprites = EntitySprites.Load(_gl, _options.SpritesPath);
        _text = new TextBatch(_gl, _font);

        _console = new DevConsole(Logger.LogManager, new object[] { this, _cfg, _consoleContext });

        _mainMenu = new MainMenuScreen(_cfg, _changelog);
        _mainMenu.OnDeploy += Deploy;
        _mainMenu.OnChangelog += ShowChangelog;
        _mainMenu.OnOptions += ShowOptions;
        _mainMenu.OnQuit += () => _window?.Close();

        _optionsScreen = new OptionsScreen(_cfg);
        _optionsScreen.OnBack += ShowMainMenu;

        _changelogScreen = new ChangelogScreen();
        _changelogScreen.OnBack += ShowMainMenu;

        _cfg.OnValueChanged(CVars.RenderVsync, ApplyVsync);
        _cfg.OnValueChanged(CVars.RenderWidth, ApplyWidth);
        _cfg.OnValueChanged(CVars.RenderHeight, ApplyHeight);

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
            keyboard.KeyChar += OnKeyChar;
        }
    }

    private void OnRender(double deltaSeconds)
    {
        Vector2 viewport = LogicalSize();
        TextBatch.PixelScale = Framebuffer().X / viewport.X;

        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        if (_screen == Screen.Tactical)
        {
            RenderTactical(viewport, (float)deltaSeconds);
        }
        else
        {
            RenderMenu(viewport, (float)deltaSeconds);
        }

        SetLayer(RenderLayer.Overlay);
        _console.FrameUpdate((float)deltaSeconds);
        _console.Arrange(new UiRect(0f, 0f, viewport.X, viewport.Y));
        _console.Draw(_prims, _text);

        FlushLayered(viewport);
    }

    private void RenderTactical(Vector2 viewport, float dt)
    {
        _session!.Advance(dt);
        PruneArrivedOrders();
        _camera!.ResizeViewport(viewport);

        SetLayer(RenderLayer.World);
        _map!.Draw(_session, _camera, _prims, _sprites, _entitySprites, _text, dt, _selectedUnit, _pendingOrders);
        _debugOverlay?.Draw(_prims, _text, _camera, viewport);

        SetLayer(RenderLayer.Ui);
        _hud!.SelectedUnit = _selectedUnit;
        _hud.HoveredButton = _hoveredButton;
        _hud.FrameUpdate(dt);
        _hud.Arrange(new UiRect(0f, 0f, viewport.X, viewport.Y));
        _hud.Draw(_prims, _text);
    }

    private void RenderMenu(Vector2 viewport, float dt)
    {
        Control screen = _screen switch
        {
            Screen.Options => _optionsScreen,
            Screen.Changelog => _changelogScreen,
            _ => _mainMenu,
        };

        SetLayer(RenderLayer.Ui);
        screen.FrameUpdate(dt);
        screen.Arrange(new UiRect(0f, 0f, viewport.X, viewport.Y));
        screen.Draw(_prims, _text);
    }

    private Control ActiveRoot => _screen switch
    {
        Screen.Options => _optionsScreen,
        Screen.Changelog => _changelogScreen,
        Screen.MainMenu => _mainMenu,
        _ => _hud!,
    };

    private void SetLayer(RenderLayer layer)
    {
        _prims.Layer = (int)layer;
        _text.Layer = (int)layer;
    }

    private void FlushLayered(Vector2 viewport)
    {
        _sprites.Flush(viewport);

        SortedSet<int> layers = new();
        layers.UnionWith(_prims.UsedLayers);
        layers.UnionWith(_text.UsedLayers);

        foreach (int layer in layers)
        {
            _prims.Flush(viewport, layer);
            _text.Flush(viewport, layer);
        }
    }

    private void Deploy()
    {
        if (_screen == Screen.Tactical)
        {
            return;
        }

        SimBoot boot = _sessionFactory(_cfg);
        _session = boot.Session;
        _debugOverlay = boot.Overlay;
        _consoleContext.Session = _session;
        _consoleContext.Overlay = _debugOverlay;
        _map = new TacticalMapRenderer();
        _camera = new Camera2D(LogicalSize(), Vector2.Zero, _cfg.GetCVar(CVars.RenderZoom));
        _hud = new TacticalHud(_session, _debugOverlay, Recenter, _console.RunCommand);

        SimSpeed speed = _cfg.GetCVar(CVars.GameStartPaused)
            ? SimSpeed.Paused
            : SpeedFromInt(_cfg.GetCVar(CVars.GameDefaultSpeed));
        _session.SetSpeed(speed);
        Recenter();

        ClearInteraction();
        _selectedUnit = null;
        _pendingOrders.Clear();
        _screen = Screen.Tactical;
    }

    private void ShowOptions()
    {
        _optionsScreen.Refresh();
        ClearInteraction();
        _screen = Screen.Options;
    }

    private void ShowChangelog()
    {
        _changelogScreen.Populate(_changelog);
        _changelog.MarkAllRead();
        _mainMenu.RefreshChangelogButton();
        ClearInteraction();
        _screen = Screen.Changelog;
    }

    private void ShowMainMenu()
    {
        ClearInteraction();
        _screen = Screen.MainMenu;
    }

    private void MenuEscape()
    {
        if (_screen is Screen.Options or Screen.Changelog)
        {
            ShowMainMenu();
        }
        else
        {
            _window?.Close();
        }
    }

    private void ClearInteraction()
    {
        _focused?.Blur();
        _focused = null;

        if (_hoveredButton is not null)
        {
            _hoveredButton.IsHovered = false;
            _hoveredButton = null;
        }

        if (_pressedButton is not null)
        {
            _pressedButton.IsPressed = false;
            _pressedButton = null;
        }

        _uiCaptured = false;
        _leftDown = false;
        _dragged = false;
    }

    private void PruneArrivedOrders()
    {
        TrackPictureSnapshot? current = _session?.Current;
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
        if (_console.IsOpen || button != MouseButton.Left)
        {
            return;
        }

        Vector2 px = ToLogical(mouse.Position);

        if (_screen != Screen.Tactical)
        {
            MenuMouseDown(px);
            return;
        }

        Control? overUi = _hud!.HitTestOpaque(px);
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

    private void MenuMouseDown(Vector2 px)
    {
        _uiCaptured = true;
        Control? hit = ActiveRoot.HitTestOpaque(px);

        LineEdit? newFocus = hit as LineEdit;
        if (!ReferenceEquals(newFocus, _focused))
        {
            _focused?.Blur();
            _focused = newFocus;
            _focused?.Focus();
        }

        if (hit is Button button)
        {
            _pressedButton = button;
            button.IsPressed = true;
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (_console.IsOpen)
        {
            return;
        }

        Vector2 px = ToLogical(mouse.Position);

        if (_screen != Screen.Tactical)
        {
            if (button == MouseButton.Left)
            {
                MenuMouseUp(px);
            }

            return;
        }

        if (button == MouseButton.Right)
        {
            if (_hud!.HitTestOpaque(px) is null && _debugOverlay is not null)
            {
                if (_debugOverlay.PickOrPlace(px, _camera!) is { } request)
                {
                    _console.RunCommand(string.Create(CultureInfo.InvariantCulture,
                        $"teleport {request.EntityId} {request.Position.X} {request.Position.Y}"));
                }
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
                if (ReferenceEquals(_hud!.HitTestOpaque(px), _pressedButton))
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

    private void MenuMouseUp(Vector2 px)
    {
        if (_pressedButton is not null)
        {
            _pressedButton.IsPressed = false;
            if (ReferenceEquals(ActiveRoot.HitTestOpaque(px), _pressedButton))
            {
                _pressedButton.Press();
            }

            _pressedButton = null;
        }

        _uiCaptured = false;
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        Vector2 px = ToLogical(position);
        UpdateHover(px);

        if (_screen == Screen.Tactical && _leftDown)
        {
            if (Vector2.Distance(px, _leftDownPx) > ClickThresholdPx)
            {
                _dragged = true;
            }

            if (_dragged)
            {
                _camera!.Pan(px - _lastMousePx);
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

        if (_console.IsOpen)
        {
            _console.HandleScroll(wheel.Y);
            return;
        }

        if (_screen != Screen.Tactical)
        {
            return;
        }

        Vector2 px = ToLogical(mouse.Position);
        if (_hud!.HitTestOpaque(px) is not null)
        {
            return;
        }

        float factor = MathF.Pow(1.15f, wheel.Y);
        _camera!.ZoomAt(factor, px);
    }

    private void UpdateHover(Vector2 px)
    {
        Button? over = ActiveRoot.HitTest(px);
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
        if (key == Key.GraveAccent)
        {
            _console.Toggle();
            return;
        }

        if (_console.IsOpen)
        {
            _console.HandleKey(key);
            return;
        }

        if (_screen != Screen.Tactical)
        {
            if (key == Key.Escape)
            {
                MenuEscape();
                return;
            }

            _focused?.HandleKey(key);
            return;
        }

        switch (key)
        {
            case Key.Space:
                _console.RunCommand(_session!.Speed == SimSpeed.Paused ? "speed normal" : "speed paused");
                break;
            case Key.Number1:
            case Key.Keypad1:
                _console.RunCommand("speed normal");
                break;
            case Key.Number2:
            case Key.Keypad2:
                _console.RunCommand("speed fast");
                break;
            case Key.C:
                Recenter();
                break;
            case Key.G:
                if (_debugOverlay is not null)
                {
                    _console.RunCommand("declutter");
                }

                break;
            case Key.Escape:
            case Key.Q:
                _window?.Close();
                break;
        }
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        if (_console.IsOpen)
        {
            _console.HandleChar(c);
            return;
        }

        if (_screen != Screen.Tactical)
        {
            _focused?.Insert(c);
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

        Vector2 destination = _camera!.ScreenToWorld(px);
        _session!.Enqueue(new MoveCommand { UnitName = _selectedUnit, Destination = destination });
        _pendingOrders[_selectedUnit] = destination;
    }

    private void Recenter()
    {
        if (_camera is null)
        {
            return;
        }

        TrackPictureSnapshot? current = _session?.Current;
        if (current is not null && current.OwnUnits.Count > 0)
        {
            _camera.SetCenter(current.OwnUnits[0].Position);
        }
    }

    private string? PickOwnUnit(Vector2 px)
    {
        if (_camera is null || _session is null)
        {
            return null;
        }

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

    private void ApplyVsync(bool value)
    {
        if (_window is not null)
        {
            _window.VSync = value;
        }
    }

    private void ApplyWidth(int value)
    {
        if (_window is not null)
        {
            _window.Size = new Vector2D<int>(Math.Max(320, value), _window.Size.Y);
        }
    }

    private void ApplyHeight(int value)
    {
        if (_window is not null)
        {
            _window.Size = new Vector2D<int>(_window.Size.X, Math.Max(240, value));
        }
    }

    private static SimSpeed SpeedFromInt(int value) => value switch
    {
        0 => SimSpeed.Paused,
        2 => SimSpeed.Fast,
        _ => SimSpeed.Normal,
    };

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

    private float UiScale
    {
        get
        {
            float scale = _cfg.GetCVar(CVars.UiScale);
            return scale > 0f ? scale : 1f;
        }
    }

    private Vector2 Framebuffer()
    {
        IWindow window = _window ?? throw new InvalidOperationException("Window is not initialized.");
        Vector2D<int> fb = window.FramebufferSize;
        return new Vector2(Math.Max(1, fb.X), Math.Max(1, fb.Y));
    }

    private Vector2 LogicalSize()
    {
        IWindow window = _window ?? throw new InvalidOperationException("Window is not initialized.");
        Vector2D<int> win = window.Size;
        return new Vector2(Math.Max(1, win.X) / UiScale, Math.Max(1, win.Y) / UiScale);
    }

    private Vector2 ToLogical(Vector2 windowPosition) => windowPosition / UiScale;
}
