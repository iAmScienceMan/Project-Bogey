using System;
using Content.Renderer.App;
using Content.Renderer.Audio;
using Content.Shared.Chatter;
using Content.Sim.Chatter;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class ChatterCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "chatter";

    public override string Description => "Generates a radio chatter line and plays its synthesized voice.";

    public override string Help => "chatter [lengthSymbols] [routine|urgent|calm]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Audio is not { Available: true } audio)
        {
            shell.WriteError("Audio output is not available on this platform.");
            return;
        }

        int length = 40;
        if (args.Length > 0 && int.TryParse(args[0], out int parsed))
        {
            length = Math.Clamp(parsed, 4, 240);
        }

        ChatterTone tone = args.Length > 1
            ? args[1].ToLowerInvariant() switch
            {
                "urgent" => ChatterTone.Urgent,
                "calm" => ChatterTone.Calm,
                _ => ChatterTone.Routine,
            }
            : ChatterTone.Routine;

        ChatterLine line = ChatterGenerator.Generate(new Random(), new ChatterRequest(length, tone));
        audio.PlayVoice(ChatterVoice.Synthesize(line), audio.SampleRate);
        shell.WriteLine($"\"{line.Text}\"  (voice {line.VoiceSeed})");
    }
}
