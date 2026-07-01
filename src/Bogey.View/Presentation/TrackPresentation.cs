using Bogey.Shared.Components;
using Bogey.Shared.Tracks;

namespace Bogey.View.Presentation;


public enum MarkerStyle
{
    Unknown,     
    Classifying, 
    Identified,  
    Stale,       
    Dropped,     
}

public static class TrackPresentation
{
    public static MarkerStyle StyleFor(Track track) => track.State switch
    {
        TrackState.Detected => MarkerStyle.Unknown,
        TrackState.Classifying => MarkerStyle.Classifying,
        TrackState.Identified => MarkerStyle.Identified,
        TrackState.Stale => MarkerStyle.Stale,
        TrackState.Dropped => MarkerStyle.Dropped,
        _ => MarkerStyle.Unknown,
    };

    public static char ScopeGlyph(Track track) => track.State switch
    {
        TrackState.Detected => '?',
        TrackState.Classifying => DomainLetter(track.DomainGuess, upper: false),
        TrackState.Identified => DomainLetter(track.DomainGuess, upper: true),
        TrackState.Stale => '~',
        TrackState.Dropped => 'x',
        _ => '?',
    };

    public static char DomainLetter(ContactDomain domain, bool upper)
    {
        char letter = domain switch
        {
            ContactDomain.Air => 'a',
            ContactDomain.Surface => 's',
            ContactDomain.Subsurface => 'u',
            _ => '?',
        };

        return upper && letter != '?' ? char.ToUpperInvariant(letter) : letter;
    }

    public static string DomainText(ContactDomain domain) => domain switch
    {
        ContactDomain.Air => "AIR",
        ContactDomain.Surface => "SURFACE",
        ContactDomain.Subsurface => "SUBSURFACE",
        _ => "UNKNOWN",
    };

    public static string StateLabel(TrackState state) => state switch
    {
        TrackState.Detected => "DETECTED",
        TrackState.Classifying => "CLASSIFYING",
        TrackState.Identified => "IDENTIFIED",
        TrackState.Stale => "STALE",
        TrackState.Dropped => "DROPPED",
        _ => state.ToString().ToUpperInvariant(),
    };

    public static string DescribeGuess(Track track) => track.State switch
    {
        TrackState.Identified when track.TypeGuess is not null => track.TypeGuess,
        TrackState.Classifying => DomainText(track.DomainGuess) + " (class)",
        TrackState.Stale when track.TypeGuess is not null => track.TypeGuess,
        TrackState.Stale when track.DomainGuess != ContactDomain.Unknown => DomainText(track.DomainGuess) + " (class)",
        _ => "UNKNOWN",
    };
}
