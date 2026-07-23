namespace Content.Shared.Chatter;

public readonly struct ChatterRequest
{
    public ChatterRequest(int lengthSymbols, ChatterTone tone)
    {
        LengthSymbols = lengthSymbols;
        Tone = tone;
    }

    public int LengthSymbols { get; }

    public ChatterTone Tone { get; }
}
