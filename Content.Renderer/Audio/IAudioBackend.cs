using System;

namespace Content.Renderer.Audio;

public delegate void AudioRender(Span<short> output);

public interface IAudioBackend : IDisposable
{
    int SampleRate { get; }

    void Start(AudioRender render);
}
