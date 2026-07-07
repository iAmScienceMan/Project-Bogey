using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Lattice.Logging;
using Lattice.Renderer.Camera;
using Lattice.Renderer.Gl;
using Content.Renderer.Map;
using Content.Renderer.RealTime;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Console;
using Lattice.Renderer.Ui.Controls;
using Content.Renderer.Ui.Screens;
using Lattice.Shared.Changelog;
using Content.Shared;
using Content.Shared.Commands;
using Lattice.Shared.Configuration;
using Content.Shared.Configuration;
using Lattice.Shared.Console;
using Content.Shared.Tracks;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Button = Lattice.Renderer.Ui.Controls.Button;
using HueSlider = Content.Renderer.Ui.Controls.HueSlider;

namespace Content.Renderer.App;

public sealed class TacticalWindow : IDisposable, IAppControl
{
    private const float ClickThresholdPx = 5f;
    private const float UnitPickRadiusPx = 16f;
    private const float ContactPickRadiusPx = 16f;
    private const float OrderArriveKm = 2f;

    private enum Screen
    {
        MainMenu,
        Options,
        Changelog,
        Connecting,
        Lobby,
        Tactical,
    }

    private readonly RendererOptions _options;
    private readonly IConfigurationManager _cfg;
    private readonly IChangelogManager _changelog;
    private readonly GameSessionFactory _sessionFactory;
    private readonly IReadOnlyList<string> _prototypeIds;
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
    private ConnectingScreen _connectingScreen = null!;
    private LobbyScreen _lobbyScreen = null!;

    private IGameSession? _session;
    private TacticalMapRenderer? _map;
    private Camera2D? _camera;
    private TacticalHud? _hud;

    private readonly NetGraph _netGraph = new();
    private readonly GroundTruthOverlayView _groundTruth = new();

    private Screen _screen = Screen.MainMenu;
    private Screen _optionsReturn = Screen.MainMenu;
    private string? _pendingHost;
    private int _pendingPort;
    private bool _wasConnected;

    private Vector2 _lastMousePx;
    private Vector2 _leftDownPx;
    private bool _leftDown;
    private bool _dragged;
    private bool _uiCaptured;
    private Button? _pressedButton;
    private Button? _hoveredButton;
    private LineEdit? _focused;
    private HueSlider? _draggedSlider;
    private string? _selectedUnit;
    private int? _selectedTarget;

    public TacticalWindow(
        RendererOptions options,
        IConfigurationManager cfg,
        IChangelogManager changelog,
        GameSessionFactory sessionFactory,
        IReadOnlyList<string> prototypeIds)
    {
        _options = options;
        _cfg = cfg;
        _changelog = changelog;
        _sessionFactory = sessionFactory;
        _prototypeIds = prototypeIds;
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
        _mainMenu.OnConnect += (host, port) => _console.RunCommand(
            string.Create(CultureInfo.InvariantCulture, $"connect {host}:{port}"));
        _mainMenu.OnChangelog += ShowChangelog;
        _mainMenu.OnOptions += () => ShowOptions(Screen.MainMenu);
        _mainMenu.OnQuit += () => _window?.Close();

        _optionsScreen = new OptionsScreen(_cfg);
        _optionsScreen.OnBack += CloseOptions;

        _changelogScreen = new ChangelogScreen();
        _changelogScreen.OnBack += ShowMainMenu;

        _connectingScreen = new ConnectingScreen();
        _connectingScreen.OnCancel += LeaveServer;
        _connectingScreen.OnRetry += RetryConnect;

        _lobbyScreen = new LobbyScreen();
        _lobbyScreen.OnReadyToggle += ready => _session?.SetReady(ready);
        _lobbyScreen.OnJoin += () => _session?.JoinGame();
        _lobbyScreen.OnLeave += () => _console.RunCommand("disconnect");
        _lobbyScreen.OnOptions += () => ShowOptions(Screen.Lobby);
        _lobbyScreen.OnApplyColor += ApplyColor;

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
            keyboard.KeyUp += OnKeyUp;
            keyboard.KeyChar += OnKeyChar;
        }
    }

    private void OnRender(double deltaSeconds)
    {
        Vector2 viewport = LogicalSize();
        TextBatch.PixelScale = Framebuffer().X / viewport.X;

        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        AdvanceSession((float)deltaSeconds);

        if (_screen == Screen.Tactical)
        {
            RenderTactical(viewport, (float)deltaSeconds);
        }
        else
        {
            RenderMenu(viewport, (float)deltaSeconds);
        }

        if (_session is not null && _cfg.GetCVar(CCVars.NetGraph))
        {
            SetLayer(RenderLayer.Ui);
            _netGraph.Update(_session.Stats, (float)deltaSeconds);
            _netGraph.Draw(_prims, _text, viewport, _session.Stats);
        }

        SetLayer(RenderLayer.Overlay);
        _console.FrameUpdate((float)deltaSeconds);
        _console.Arrange(new UiRect(0f, 0f, viewport.X, viewport.Y));
        _console.Draw(_prims, _text);

        FlushLayered(viewport);
    }

    private void AdvanceSession(float dt)
    {
        if (_session is null)
        {
            return;
        }

        _session.Advance(dt);

        while (_session.TryDequeueNotice(out string? notice))
        {
            _console.WriteLine(LogLevel.Warning, notice);
        }

        switch (_session.Phase)
        {
            case GamePhase.Lobby when _screen == Screen.Connecting:
                EnterLobby();
                break;
            case GamePhase.Lobby when _screen == Screen.Tactical:
                TearDownTactical();
                EnterLobby();
                break;
            case GamePhase.InGame when _screen is Screen.Connecting or Screen.Lobby:
                EnterTactical();
                break;
            case GamePhase.Disconnected when _screen != Screen.Connecting:
                ShowDisconnected();
                break;
            case GamePhase.Disconnected when _screen == Screen.Connecting:
                _connectingScreen.ShowFailure(_session.DisconnectReason, _wasConnected);
                break;
        }
    }

    private void RenderTactical(Vector2 viewport, float dt)
    {
        PruneArrivedOrders();
        _camera!.ResizeViewport(viewport);

        uint ownColor = ColorRgbUtil.ParseHex(
            _cfg.GetCVar(CCVars.PlayerColor),
            ColorRgbUtil.ParseHex(CCVars.PlayerColor.DefaultValue, 0x4DC3FF));

        SetLayer(RenderLayer.World);
        _map!.Draw(_session!, _camera, _prims, _sprites, _entitySprites, _text, dt, _selectedUnit, _selectedTarget, _pendingOrders, ownColor);

        if (_cfg.GetCVar(CCVars.DebugOverlay) && _session!.GroundTruth is { } groundTruth)
        {
            _groundTruth.Draw(_prims, _text, _camera, groundTruth);
        }

        SetLayer(RenderLayer.Ui);
        _hud!.SelectedUnit = _selectedUnit;
        _hud.SelectedTarget = _selectedTarget;
        _hud.HoveredButton = _hoveredButton;
        _hud.FrameUpdate(dt);
        _hud.Arrange(new UiRect(0f, 0f, viewport.X, viewport.Y));
        _hud.Draw(_prims, _text);
    }

    private void RenderMenu(Vector2 viewport, float dt)
    {
        if (_screen == Screen.Lobby)
        {
            _lobbyScreen.Update(_session?.Lobby, _session?.Username ?? string.Empty);
        }

        Control screen = ActiveRoot;

        SetLayer(RenderLayer.Ui);
        screen.FrameUpdate(dt);
        screen.Arrange(new UiRect(0f, 0f, viewport.X, viewport.Y));
        screen.Draw(_prims, _text);
    }

    private Control ActiveRoot => _screen switch
    {
        Screen.Options => _optionsScreen,
        Screen.Changelog => _changelogScreen,
        Screen.Connecting => _connectingScreen,
        Screen.Lobby => _lobbyScreen,
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

    public void ConnectTo(string host, int port)
    {
        _pendingHost = host;
        _pendingPort = port;
        CreateSession();
    }

    public void DisconnectFromServer() => LeaveServer();

    public bool HasSession => _session is not null;

    private void RetryConnect()
    {
        if (_pendingHost is not null)
        {
            CreateSession();
        }
        else
        {
            ShowMainMenu();
        }
    }

    private void CreateSession()
    {
        _session?.Disconnect();
        TearDownTactical();

        _session = _sessionFactory(_pendingHost!, _pendingPort);
        _consoleContext.Session = _session;
        _consoleContext.Prototypes = _prototypeIds;
        _wasConnected = false;

        _connectingScreen.ShowConnecting($"{_pendingHost}:{_pendingPort}");
        ClearInteraction();
        _screen = Screen.Connecting;
    }

    private void EnterLobby()
    {
        _wasConnected = true;
        _lobbyScreen.ColorHue = ColorRgbUtil.ToHue(
            ColorRgbUtil.ParseHex(_cfg.GetCVar(CCVars.PlayerColor), 0x4DC3FF));
        ClearInteraction();
        _screen = Screen.Lobby;
    }

    private void EnterTactical()
    {
        _map = new TacticalMapRenderer();
        _camera = new Camera2D(LogicalSize(), Vector2.Zero, _cfg.GetCVar(CVars.RenderZoom));
        _hud = new TacticalHud(_session!, Recenter, _console.RunCommand);

        _selectedUnit = null;
        _selectedTarget = null;
        _pendingOrders.Clear();
        ClearInteraction();
        _screen = Screen.Tactical;
        Recenter();
    }

    private void ApplyColor(uint rgb)
    {
        _cfg.SetCVar(CCVars.PlayerColor, ColorRgbUtil.ToHex(rgb));
        _session?.SetColor(rgb);
    }

    private void ShowDisconnected()
    {
        TearDownTactical();
        _connectingScreen.ShowFailure(_session?.DisconnectReason, _wasConnected);
        ClearInteraction();
        _screen = Screen.Connecting;
    }

    private void ShowOptions(Screen returnTo)
    {
        _optionsReturn = returnTo;
        _optionsScreen.Refresh();
        ClearInteraction();
        _screen = Screen.Options;
    }

    private void CloseOptions()
    {
        ClearInteraction();
        _screen = _optionsReturn;
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

    private void LeaveServer()
    {
        _session?.Disconnect();
        _session = null;
        _consoleContext.Session = null;
        TearDownTactical();
        ShowMainMenu();
    }

    private void TearDownTactical()
    {
        _map = null;
        _camera = null;
        _hud = null;
        _selectedUnit = null;
        _selectedTarget = null;
        _pendingOrders.Clear();
        _groundTruth.Clear();
        _cfg.SetCVar(CCVars.DebugOverlay, false);
    }

    private void MenuEscape()
    {
        switch (_screen)
        {
            case Screen.Options:
                CloseOptions();
                break;
            case Screen.Changelog:
                ShowMainMenu();
                break;
            case Screen.Connecting:
                LeaveServer();
                break;
            case Screen.Lobby:
                break;
            default:
                _window?.Close();
                break;
        }
    }

    private void ClearInteraction()
    {
        _focused?.Blur();
        _focused = null;
        _draggedSlider = null;

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

        if (hit is HueSlider slider)
        {
            _draggedSlider = slider;
            slider.SetFromPosition(px);
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
            if (_cfg.GetCVar(CCVars.DebugOverlay)
                && _session?.GroundTruth is { } groundTruth
                && _hud!.HitTestOpaque(px) is null
                && _groundTruth.PickOrPlace(px, _camera!, groundTruth) is { } request)
            {
                _console.RunCommand(string.Create(CultureInfo.InvariantCulture,
                    $"teleport {request.EntityId} {request.World.X:0.##} {request.World.Y:0.##}"));
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
        _draggedSlider = null;

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

        if (_draggedSlider is not null)
        {
            _draggedSlider.SetFromPosition(px);
        }

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

        if (_screen == Screen.Changelog)
        {
            _changelogScreen.HandleScroll(wheel.Y);
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
                _console.RunCommand(_session!.Speed == 0 ? "speed 1" : "speed 0");
                break;
            case Key.Number1:
            case Key.Keypad1:
                _console.RunCommand("speed 1");
                break;
            case Key.Number2:
            case Key.Keypad2:
                _console.RunCommand("speed 10");
                break;
            case Key.C:
                Recenter();
                break;
            case Key.Escape:
                if (_selectedUnit is not null || _selectedTarget is not null)
                {
                    _selectedUnit = null;
                    _selectedTarget = null;
                    break;
                }

                _console.RunCommand("disconnect");
                break;
        }
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        if (_console.IsOpen)
        {
            _console.HandleKeyUp(key);
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
        string? hitUnit = PickOwnUnit(px);
        if (hitUnit is not null)
        {
            _selectedUnit = string.Equals(_selectedUnit, hitUnit, StringComparison.Ordinal) ? null : hitUnit;
            return;
        }

        int? hitContact = PickContact(px);
        if (hitContact is not null)
        {
            _selectedTarget = _selectedTarget == hitContact ? null : hitContact;
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

    private int? PickContact(Vector2 px)
    {
        if (_camera is null || _session?.Current is not { } snapshot)
        {
            return null;
        }

        int? best = null;
        float bestDistance = ContactPickRadiusPx;

        foreach (Track track in snapshot.Tracks)
        {
            Vector2 screen = _camera.WorldToScreen(track.EstimatedPosition);
            float distance = Vector2.Distance(screen, px);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = track.TrackId;
            }
        }

        return best;
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

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _gl.Viewport(0, 0, (uint)Math.Max(1, size.X), (uint)Math.Max(1, size.Y));
    }

    private void OnClosing()
    {
        _session?.Disconnect();
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
