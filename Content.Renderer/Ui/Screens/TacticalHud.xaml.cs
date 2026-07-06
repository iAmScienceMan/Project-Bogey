using System;
using System.Globalization;
using System.Text;
using Content.Renderer.App;
using Content.Renderer.RealTime;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Controls;
using Lattice.Renderer.Ui.Xaml;
using Content.Shared.Components;
using Content.Shared.Tracks;

namespace Content.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class TacticalHud : Control
{
    private readonly ISimSession _session;
    private readonly Action<string> _runCommand;
    private readonly bool _debug;

    private const int MaxSalvoSize = 8;

    private string _weaponKey = string.Empty;
    private int _salvoSize = 1;

    public TacticalHud(ISimSession session, IDebugOverlay? debugOverlay, Action recenter, Action<string> runCommand)
    {
        _session = session;
        _runCommand = runCommand;
        _debug = debugOverlay is not null;

        LatticeXaml.Load(this);

        PauseButton.OnPressed += TogglePause;
        NormalButton.OnPressed += () => _runCommand("speed 1");
        FastButton.OnPressed += () => _runCommand("speed 10");
        RecenterButton.OnPressed += () => recenter();
        DeclutterButton.OnPressed += () => _runCommand("declutter");

        PostureHoldButton.OnPressed += () => SetPosture("hold");
        PostureDefButton.OnPressed += () => SetPosture("defensive");
        PostureFreeButton.OnPressed += () => SetPosture("free");

        LockButton.OnPressed += LockTarget;
        UnlockButton.OnPressed += Unlock;
        SalvoDownButton.OnPressed += () => AdjustSalvo(-1);
        SalvoUpButton.OnPressed += () => AdjustSalvo(1);

        DebugPanel.Visible = _debug;
        EngagePanel.Visible = false;
    }

    public string? SelectedUnit { get; set; }

    public int? SelectedTarget { get; set; }

    public Button? HoveredButton
    {
        get => Tooltip.Target;
        set => Tooltip.Target = value;
    }

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);

        bool hasPicture = _session.Current is not null;
        WaitingLabel.Visible = !hasPicture;
        StatusReadout.Visible = hasPicture;

        TickLabel.Text = "TICK " + _session.Tick.ToString(CultureInfo.InvariantCulture);
        SpeedLabel.Text = "SPEED " + SpeedLabelText(_session.Speed);

        PauseButton.Active = _session.Speed == 0;
        NormalButton.Active = _session.Speed == 1;
        FastButton.Active = _session.Speed == 10;
        DebugPanel.Visible = _debug;

        UpdateEngagePanel();
    }

    public override void Arrange(UiRect rect)
    {
        base.Arrange(rect);
        Tooltip.PlaceWithin(rect);
    }

    private void UpdateEngagePanel()
    {
        OwnUnitView? unit = FindUnit(SelectedUnit);
        if (unit is null)
        {
            EngagePanel.Visible = false;
            _weaponKey = string.Empty;
            WeaponList.ClearChildren();
            return;
        }

        EngagePanel.Visible = true;
        EngageTitle.Text = unit.Name.ToUpperInvariant();

        EngageHull.Text = unit.HullMax > 0f
            ? string.Create(CultureInfo.InvariantCulture, $"HP {unit.HullCurrent:0}/{unit.HullMax:0}")
            : "HP -";

        Track? target = FindTrack(SelectedTarget);
        EngageTarget.Text = target is null
            ? "No target - click a contact"
            : string.Create(CultureInfo.InvariantCulture, $"TARGET T{target.TrackId} · {target.DomainGuess}");

        EngageLock.Text = unit.LockedTrackId is { } locked
            ? string.Create(CultureInfo.InvariantCulture, $"LOCK T{locked}")
            : "LOCK none";

        LockButton.Disabled = target is null;
        LockButton.Active = unit.LockedTrackId is { } activeLock && SelectedTarget == activeLock;
        UnlockButton.Disabled = unit.LockedTrackId is null;

        SalvoLabel.Text = string.Create(CultureInfo.InvariantCulture, $"SALVO {_salvoSize}");

        PostureHoldButton.Active = unit.Posture == WeaponPosture.Hold;
        PostureDefButton.Active = unit.Posture == WeaponPosture.Defensive;
        PostureFreeButton.Active = unit.Posture == WeaponPosture.Free;

        string key = BuildWeaponKey(unit);
        if (key != _weaponKey)
        {
            RebuildWeapons(unit);
            _weaponKey = key;
        }
    }

    private void RebuildWeapons(OwnUnitView unit)
    {
        WeaponList.ClearChildren();

        foreach (WeaponStatusView weapon in unit.Weapons)
        {
            string rounds = weapon.Rounds < 0
                ? string.Empty
                : " (" + weapon.Rounds.ToString(CultureInfo.InvariantCulture) + ")";

            Button button = new()
            {
                FontSize = 12f,
                MinWidth = 150f,
            };

            if (weapon.PointDefense)
            {
                button.Text = weapon.Name + " [auto]";
                button.Disabled = true;
            }
            else
            {
                button.Text = weapon.Name + rounds;
                button.Disabled = !weapon.Ready || weapon.Rounds == 0;
                string weaponName = weapon.Name;
                button.OnPressed += () => FireWeapon(weaponName);
            }

            WeaponList.AddChild(button);
        }
    }

    private void FireWeapon(string weapon)
    {
        if (SelectedUnit is { } unit && SelectedTarget is { } track)
        {
            _runCommand(string.Create(CultureInfo.InvariantCulture, $"engage {unit} {track} {weapon} {_salvoSize}"));
        }
    }

    private void LockTarget()
    {
        if (SelectedUnit is { } unit && SelectedTarget is { } track)
        {
            _runCommand(string.Create(CultureInfo.InvariantCulture, $"lock {unit} {track}"));
        }
    }

    private void Unlock()
    {
        if (SelectedUnit is { } unit)
        {
            _runCommand("lock " + unit + " off");
        }
    }

    private void AdjustSalvo(int delta)
        => _salvoSize = Math.Clamp(_salvoSize + delta, 1, MaxSalvoSize);

    private void SetPosture(string posture)
    {
        if (SelectedUnit is { } unit)
        {
            _runCommand("posture " + unit + " " + posture);
        }
    }

    private OwnUnitView? FindUnit(string? name)
    {
        if (name is null || _session.Current is not { } snapshot)
        {
            return null;
        }

        foreach (OwnUnitView unit in snapshot.OwnUnits)
        {
            if (string.Equals(unit.Name, name, StringComparison.Ordinal))
            {
                return unit;
            }
        }

        return null;
    }

    private Track? FindTrack(int? trackId)
    {
        if (trackId is not { } id || _session.Current is not { } snapshot)
        {
            return null;
        }

        foreach (Track track in snapshot.Tracks)
        {
            if (track.TrackId == id)
            {
                return track;
            }
        }

        return null;
    }

    private static string BuildWeaponKey(OwnUnitView unit)
    {
        StringBuilder builder = new();
        builder.Append(unit.Name).Append('|').Append(unit.Posture).Append('|');
        foreach (WeaponStatusView weapon in unit.Weapons)
        {
            builder.Append(weapon.Name).Append(':').Append(weapon.Rounds).Append(':').Append(weapon.Ready ? '1' : '0').Append(';');
        }

        return builder.ToString();
    }

    private void TogglePause()
        => _runCommand(_session.Speed == 0 ? "speed 1" : "speed 0");

    private static string SpeedLabelText(int speed) => speed switch
    {
        0 => "PAUSED",
        _ => speed + "x",
    };
}
