using System;
using System.Globalization;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui.Controls;
using Lattice.Renderer.Ui.Xaml;
using Lattice.Shared.Changelog;

namespace Content.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class ChangelogScreen : Control
{
    private static readonly Rgba AddColor = Rgba.Parse("#6ED18D");
    private static readonly Rgba RemoveColor = Rgba.Parse("#D16E6E");
    private static readonly Rgba FixColor = Rgba.Parse("#D1BA6E");
    private static readonly Rgba TweakColor = Rgba.Parse("#6E96D1");

    private float _phase;

    public ChangelogScreen()
    {
        LatticeXaml.Load(this);
        BackButton.OnPressed += () => OnBack?.Invoke();
    }

    public event Action? OnBack;

    public void Populate(IChangelogManager changelog)
    {
        List.ClearChildren();

        if (changelog.Entries.Count == 0)
        {
            List.AddChild(Text("No changelog entries yet.", 13f, UiTheme.Subtle));
            return;
        }

        int lastRead = changelog.LastReadId;
        DateTime? currentDate = null;
        string? currentAuthor = null;
        bool sawUnread = false;
        bool dividerPlaced = false;

        foreach (ChangelogEntry entry in changelog.Entries)
        {
            DateTime date = entry.Time.ToLocalTime().Date;
            if (currentDate != date)
            {
                currentDate = date;
                currentAuthor = null;
                List.AddChild(Text(FormatDate(date), 14f, UiTheme.Accent));
            }

            if (entry.Id > lastRead)
            {
                sawUnread = true;
            }
            else if (sawUnread && !dividerPlaced)
            {
                dividerPlaced = true;
                currentAuthor = null;
                List.AddChild(Text("-- new since your last visit --", 12f, UiTheme.Accent));
            }

            if (currentAuthor != entry.Author)
            {
                currentAuthor = entry.Author;
                List.AddChild(Text(entry.Author.ToUpperInvariant() + " changed:", 12f, UiTheme.Subtle));
            }

            foreach (ChangelogChange change in entry.Changes)
            {
                List.AddChild(ChangeRow(change));
            }
        }
    }

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

    private static Control ChangeRow(ChangelogChange change)
    {
        BoxContainer row = new() { Orientation = Orientation.Horizontal, DrawBackground = false, Separation = 8f };
        row.AddChild(Text(TagFor(change.Type), 12f, ColorFor(change.Type)));
        row.AddChild(Text(change.Message, 12f, UiTheme.Text));
        return row;
    }

    private static Label Text(string text, float fontSize, Rgba color)
        => new() { Text = text, FontSize = fontSize, Color = color };

    private static string FormatDate(DateTime date)
    {
        DateTime today = DateTime.Now.Date;
        if (date == today)
        {
            return "TODAY";
        }

        if (date == today.AddDays(-1))
        {
            return "YESTERDAY";
        }

        return date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    private static string TagFor(ChangeType type) => type switch
    {
        ChangeType.Add => "ADD",
        ChangeType.Remove => "RMV",
        ChangeType.Fix => "FIX",
        ChangeType.Tweak => "TWK",
        _ => "?",
    };

    private static Rgba ColorFor(ChangeType type) => type switch
    {
        ChangeType.Add => AddColor,
        ChangeType.Remove => RemoveColor,
        ChangeType.Fix => FixColor,
        ChangeType.Tweak => TweakColor,
        _ => UiTheme.Text,
    };
}
