using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Shared.Configuration;
using Content.Shared.Net;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Controls;
using Lattice.Renderer.Ui.Xaml;
using Lattice.Shared.Changelog;
using Lattice.Shared.Configuration;

namespace Content.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class MainMenuScreen : Control
{
    private readonly IConfigurationManager _cfg;
    private readonly IChangelogManager _changelog;

    private readonly List<ServerListing> _servers = new();
    private ServerListing? _selected;
    private bool _hasPlayers;
    private bool _notFull;
    private bool _sortByPlayers;

    public MainMenuScreen(IConfigurationManager cfg, IChangelogManager changelog)
    {
        _cfg = cfg;
        _changelog = changelog;
        LatticeXaml.Load(this);

        UsernameEdit.Text = cfg.GetCVar(CCVars.PlayerUsername);
        AddressEdit.Text = cfg.GetCVar(CCVars.ClientAddress);

        ConnectButton.OnPressed += DirectConnect;
        AddressEdit.OnSubmit += DirectConnect;
        JoinButton.OnPressed += JoinSelected;
        RefreshButton.OnPressed += () => OnRefresh?.Invoke();
        ChangelogButton.OnPressed += () => OnChangelog?.Invoke();
        OptionsButton.OnPressed += () => OnOptions?.Invoke();
        QuitButton.OnPressed += () => OnQuit?.Invoke();

        SearchEdit.OnTextChanged += _ => RebuildRows();
        PlayersFilterButton.OnPressed += () => ToggleFilter(ref _hasPlayers, PlayersFilterButton);
        NotFullFilterButton.OnPressed += () => ToggleFilter(ref _notFull, NotFullFilterButton);
        SortNameButton.OnPressed += () => SetSort(false);
        SortPlayersButton.OnPressed += () => SetSort(true);

        SetSort(false);
        RefreshChangelogButton();
        RebuildRows();
    }

    public event Action<string, int>? OnConnect;

    public event Action? OnChangelog;

    public event Action? OnOptions;

    public event Action? OnQuit;

    public event Action? OnRefresh;

    public void RefreshChangelogButton()
        => ChangelogButton.Text = _changelog.HasNewEntries ? "CHANGELOG (NEW)" : "CHANGELOG";

    public void SetServers(IReadOnlyList<ServerListing> servers)
    {
        _servers.Clear();
        _servers.AddRange(servers);

        if (_selected is { } selected && _servers.All(s => s.Address != selected.Address))
        {
            _selected = null;
        }

        RebuildRows();
    }

    public override Vector2 Measure() => Root.Measure();

    public override void Arrange(UiRect rect)
    {
        Bounds = rect;
        Root.Arrange(rect);
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

    private void ToggleFilter(ref bool flag, Button button)
    {
        flag = !flag;
        button.Active = flag;
        RebuildRows();
    }

    private void SetSort(bool byPlayers)
    {
        _sortByPlayers = byPlayers;
        SortNameButton.Active = !byPlayers;
        SortPlayersButton.Active = byPlayers;
        RebuildRows();
    }

    private IEnumerable<ServerListing> Filtered()
    {
        IEnumerable<ServerListing> query = _servers;

        if (_hasPlayers)
        {
            query = query.Where(s => s.Players > 0);
        }

        if (_notFull)
        {
            query = query.Where(s => s.MaxPlayers <= 0 || s.Players < s.MaxPlayers);
        }

        string search = SearchEdit.Text.Trim();
        if (search.Length > 0)
        {
            query = query.Where(s =>
                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || s.Address.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return _sortByPlayers
            ? query.OrderByDescending(s => s.Players).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            : query.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase);
    }

    private void RebuildRows()
    {
        ServerList.ClearChildren();

        int shown = 0;
        foreach (ServerListing server in Filtered())
        {
            shown++;
            ServerListing captured = server;
            Button row = new()
            {
                Text = server.Name,
                SecondaryText = $"{server.Players}/{server.MaxPlayers}",
                LeftAlign = true,
                HorizontalExpand = true,
                FontSize = 14,
                Active = _selected is { } sel && sel.Address == server.Address,
            };
            row.OnPressed += () => Select(captured);
            ServerList.AddChild(row);
        }

        StatusLabel.Text = _servers.Count == 0
            ? "No servers found."
            : shown == 1 ? "1 server" : $"{shown} servers";

        JoinButton.Disabled = _selected is null;
    }

    private void Select(ServerListing server)
    {
        _selected = server;
        RebuildRows();
    }

    private void JoinSelected()
    {
        if (_selected is not { } server || !TryParseAddress(server.Address, out string host, out int port))
        {
            return;
        }

        Launch(host, port);
    }

    private void DirectConnect()
    {
        if (!TryParseAddress(AddressEdit.Text, out string host, out int port))
        {
            ErrorLabel.Text = "Invalid server address - use ip or ip:port.";
            return;
        }

        _cfg.SetCVar(CCVars.ClientAddress, AddressEdit.Text.Trim());
        Launch(host, port);
    }

    private void Launch(string host, int port)
    {
        string username = UsernameEdit.Text.Trim();
        if (username.Length == 0)
        {
            username = CCVars.PlayerUsername.DefaultValue;
        }

        ErrorLabel.Text = string.Empty;
        _cfg.SetCVar(CCVars.PlayerUsername, username);
        OnConnect?.Invoke(host, port);
    }
}
