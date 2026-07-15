using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
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
    private static readonly Rgba AddColor = Rgba.Parse("#8FB39A");
    private static readonly Rgba RemoveColor = Rgba.Parse("#C79AA2");
    private static readonly Rgba FixColor = Rgba.Parse("#BCB48C");
    private static readonly Rgba TweakColor = Rgba.Parse("#9FB2C0");

    public ChangelogScreen()
    {
        LatticeXaml.Load(this);
    }

    public void HandleScroll(float wheelY) => List.HandleScroll(wheelY);

    public override Vector2 Measure() => List.Measure();

    public override void Arrange(UiRect rect)
    {
        Bounds = rect;
        List.Arrange(rect);
    }

    public void Populate(IChangelogManager changelog)
    {
        List.ClearChildren();
        List.ScrollToTop();

        if (changelog.Entries.Count == 0)
        {
            List.AddChild(Text("No changelog entries yet.", 13f, UiTheme.Subtle));
            return;
        }

        int lastRead = changelog.LastReadId;
        DateTime? currentDate = null;
        string? currentAuthor = null;
        bool previousUnread = false;
        bool dividerPlaced = false;

        List<ChangelogEntry> entries = new(changelog.Entries);
        entries.Sort(static (a, b) => b.Id.CompareTo(a.Id));

        foreach (ChangelogEntry entry in entries)
        {
            bool unread = entry.Id > lastRead;

            if (previousUnread && !unread && !dividerPlaced)
            {
                dividerPlaced = true;
                currentDate = null;
                currentAuthor = null;
                List.AddChild(Text("-- everything above is new since your last visit --", 12f, UiTheme.Accent));
            }

            previousUnread = unread;

            DateTime date = entry.Time.ToLocalTime().Date;
            if (currentDate != date)
            {
                currentDate = date;
                currentAuthor = null;
                List.AddChild(Text(FormatDate(date), 14f, UiTheme.Accent));
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

    private const float RowFontSize = 12f;
    private const float MessageWidth = 452f;

    private static Control ChangeRow(ChangelogChange change)
    {
        List<string> lines = Wrap(change.Message, MessageWidth, RowFontSize);

        BoxContainer column = new() { Orientation = Orientation.Vertical, DrawBackground = false, Separation = 2f };

        BoxContainer first = new() { Orientation = Orientation.Horizontal, DrawBackground = false, Separation = 8f };
        first.AddChild(Text(TagFor(change.Type), RowFontSize, ColorFor(change.Type)));
        first.AddChild(Text(lines[0], RowFontSize, UiTheme.Text));
        column.AddChild(first);

        for (int i = 1; i < lines.Count; i++)
        {
            column.AddChild(Text("      " + lines[i], RowFontSize, UiTheme.Text));
        }

        return column;
    }

    private static List<string> Wrap(string text, float maxWidth, float fontSize)
    {
        List<string> lines = new();
        string current = string.Empty;

        foreach (string word in text.Split(' '))
        {
            string trial = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && TextBatch.Measure(trial, fontSize) > maxWidth)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = trial;
            }
        }

        lines.Add(current);
        return lines;
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
