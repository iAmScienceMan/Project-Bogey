using Content.Renderer.App;
using Content.Renderer.Audio;
using Content.Shared.Chatter;
using Content.Sim.Chatter;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class SayCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "say";

    public override string Description => "Synthesizes radio voice from your own text (gibberish keyed to the text).";

    public override string Help => "say <text>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Audio is not { Available: true } audio)
        {
            shell.WriteError("Audio output is not available on this platform.");
            return;
        }

        if (argStr.Length == 0)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        ChatterLine line = ChatterGenerator.FromText(argStr);
        audio.PlayVoice(ChatterVoice.Synthesize(line), audio.SampleRate);
        shell.WriteLine($"\"{line.Text}\"  (voice {line.VoiceSeed})");
    }
}
