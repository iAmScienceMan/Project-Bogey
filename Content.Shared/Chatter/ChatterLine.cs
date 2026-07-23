namespace Content.Shared.Chatter;

public readonly struct ChatterLine
{
    public ChatterLine(string text, int voiceSeed)
    {
        Text = text;
        VoiceSeed = voiceSeed;
    }

    public string Text { get; }

    public int VoiceSeed { get; }
}
