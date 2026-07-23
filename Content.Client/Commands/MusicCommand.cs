using Content.Renderer.App;
using Content.Renderer.Audio;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class MusicCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "music";

    public override string Description => "Controls lobby music playback on this client.";

    public override string Help => "music <play|stop|now>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Audio is not { Available: true } audio)
        {
            shell.WriteError("Audio output is not available on this platform.");
            return;
        }

        string action = args.Length > 0 ? args[0].ToLowerInvariant() : "now";
        switch (action)
        {
            case "play":
                if (audio.PlayMusic(AudioManager.LobbyTrack))
                {
                    shell.WriteLine($"Playing {audio.CurrentMusicName}.");
                }
                else
                {
                    shell.WriteError($"Could not load {AudioManager.LobbyTrack}.");
                }

                break;
            case "stop":
                audio.StopMusic();
                shell.WriteLine("Music stopped.");
                break;
            case "now":
                shell.WriteLine(audio.CurrentMusicName is { } name
                    ? $"Now playing: {name}"
                    : "No music playing.");
                break;
            default:
                shell.WriteError("usage: " + Help);
                break;
        }
    }
}
