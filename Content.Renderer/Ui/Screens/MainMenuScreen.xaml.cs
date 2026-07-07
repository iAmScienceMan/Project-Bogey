using System;
using System.Globalization;
using Content.Renderer.App;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui.Controls;
using Lattice.Renderer.Ui.Xaml;
using Lattice.Shared.Changelog;
using Lattice.Shared.Configuration;
using Content.Shared.Configuration;
using Content.Shared.Net;

namespace Content.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class MainMenuScreen : Control
{
    private readonly IConfigurationManager _cfg;
    private readonly IChangelogManager _changelog;
    private float _phase;

    public MainMenuScreen(IConfigurationManager cfg, IChangelogManager changelog)
    {
        _cfg = cfg;
        _changelog = changelog;
        LatticeXaml.Load(this);

        UsernameEdit.Text = cfg.GetCVar(CCVars.PlayerUsername);
        AddressEdit.Text = cfg.GetCVar(CCVars.ClientAddress);

        ConnectButton.OnPressed += Connect;
        UsernameEdit.OnSubmit += Connect;
        AddressEdit.OnSubmit += Connect;
        ChangelogButton.OnPressed += () => OnChangelog?.Invoke();
        OptionsButton.OnPressed += () => OnOptions?.Invoke();
        QuitButton.OnPressed += () => OnQuit?.Invoke();

        RefreshChangelogButton();
    }

    public event Action<string, int>? OnConnect;

    public event Action? OnChangelog;

    public event Action? OnOptions;

    public event Action? OnQuit;

    public void RefreshChangelogButton()
        => ChangelogButton.Text = _changelog.HasNewEntries ? "CHANGELOG (NEW)" : "CHANGELOG";

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);
        _phase += dt * 6f;
    }

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        MenuBackground.Draw(prims, Bounds, _phase);
        base.Draw(prims, text);
    }

    public static bool TryParseAddress(string input, out string host, out int port)
    {
        host = string.Empty;
        port = NetDefaults.Port;

        string trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        int colon = trimmed.LastIndexOf(':');
        if (colon < 0)
        {
            host = trimmed;
            return true;
        }

        string portPart = trimmed[(colon + 1)..];
        if (!int.TryParse(portPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort)
            || parsedPort is < 1 or > 65535)
        {
            return false;
        }

        host = trimmed[..colon];
        port = parsedPort;
        return host.Length > 0;
    }

    private void Connect()
    {
        string username = UsernameEdit.Text.Trim();
        if (username.Length == 0)
        {
            username = CCVars.PlayerUsername.DefaultValue;
        }

        if (!TryParseAddress(AddressEdit.Text, out string host, out int port))
        {
            ErrorLabel.Text = "Invalid server address - use ip or ip:port.";
            return;
        }

        ErrorLabel.Text = string.Empty;
        _cfg.SetCVar(CCVars.PlayerUsername, username);
        _cfg.SetCVar(CCVars.ClientAddress, AddressEdit.Text.Trim());
        OnConnect?.Invoke(host, port);
    }
}
