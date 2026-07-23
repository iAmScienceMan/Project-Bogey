using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Content.Renderer.Audio.Backends;

namespace Content.Renderer.Audio;

public sealed class AudioManager : IDisposable
{
    public const string LobbyTrack = "moonlight_bogey.wav";

    private readonly object _lock = new();
    private readonly List<Voice> _voices = new();
    private readonly string _audioPath;
    private readonly IAudioBackend? _backend;
    private readonly int _rate;

    private short[]? _music;
    private int _musicPosition;
    private bool _musicLoop;
    private string? _musicName;
    private float _masterVolume = 1f;
    private float _musicVolume = 1f;

    public AudioManager(string resourcesPath)
    {
        _audioPath = Path.Combine(resourcesPath, "Audio");

        IAudioBackend? backend = null;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                backend = new MacAudioQueueBackend();
                backend.Start(Render);
            }
        }
        catch
        {
            backend?.Dispose();
            backend = null;
        }

        _backend = backend;
        _rate = _backend?.SampleRate ?? 44100;
    }

    public bool Available => _backend is not null;

    public int SampleRate => _rate;

    public string? CurrentMusicName
    {
        get
        {
            lock (_lock)
            {
                return _musicName;
            }
        }
    }

    public float MasterVolume
    {
        get
        {
            lock (_lock)
            {
                return _masterVolume;
            }
        }

        set
        {
            lock (_lock)
            {
                _masterVolume = Math.Clamp(value, 0f, 1f);
            }
        }
    }

    public float MusicVolume
    {
        get
        {
            lock (_lock)
            {
                return _musicVolume;
            }
        }

        set
        {
            lock (_lock)
            {
                _musicVolume = Math.Clamp(value, 0f, 1f);
            }
        }
    }

    public bool PlayMusic(string fileName, bool loop = true)
    {
        if (!TryLoadMono(fileName, out short[] mono))
        {
            return false;
        }

        string name = Path.GetFileNameWithoutExtension(fileName);
        lock (_lock)
        {
            _music = mono;
            _musicPosition = 0;
            _musicLoop = loop;
            _musicName = name;
        }

        return true;
    }

    public void StopMusic()
    {
        lock (_lock)
        {
            _music = null;
            _musicPosition = 0;
            _musicName = null;
        }
    }

    public bool PlayVoiceFile(string fileName)
    {
        if (!TryLoadMono(fileName, out short[] mono))
        {
            return false;
        }

        PlayVoice(mono, _rate);
        return true;
    }

    private bool TryLoadMono(string fileName, out short[] mono)
    {
        try
        {
            mono = ToMono(WavFile.Load(Path.Combine(_audioPath, fileName)), _rate);
            return true;
        }
        catch (Exception)
        {
            mono = Array.Empty<short>();
            return false;
        }
    }

    public void PlayVoice(short[] pcm, int rate)
    {
        if (pcm.Length == 0)
        {
            return;
        }

        short[] mono = rate == _rate ? pcm : Resample(pcm, rate, _rate);

        lock (_lock)
        {
            _voices.Add(new Voice(mono));
        }
    }

    public void Dispose() => _backend?.Dispose();

    private void Render(Span<short> output)
    {
        lock (_lock)
        {
            for (int i = 0; i < output.Length; i++)
            {
                int sample = 0;

                if (_music is { Length: > 0 } music)
                {
                    sample += (int)(music[_musicPosition] * _musicVolume);
                    _musicPosition++;
                    if (_musicPosition >= music.Length)
                    {
                        if (_musicLoop)
                        {
                            _musicPosition = 0;
                        }
                        else
                        {
                            _music = null;
                            _musicName = null;
                        }
                    }
                }

                for (int v = 0; v < _voices.Count; v++)
                {
                    Voice voice = _voices[v];
                    if (voice.Position < voice.Samples.Length)
                    {
                        sample += voice.Samples[voice.Position];
                        voice.Position++;
                    }
                }

                output[i] = (short)Math.Clamp((int)(sample * _masterVolume), short.MinValue, short.MaxValue);
            }

            _voices.RemoveAll(static v => v.Position >= v.Samples.Length);
        }
    }

    private static short[] ToMono(WavData wav, int targetRate)
    {
        short[] samples = wav.Samples;
        short[] mono;

        if (wav.Channels <= 1)
        {
            mono = samples;
        }
        else
        {
            int frames = samples.Length / wav.Channels;
            mono = new short[frames];
            for (int i = 0; i < frames; i++)
            {
                int sum = 0;
                for (int c = 0; c < wav.Channels; c++)
                {
                    sum += samples[i * wav.Channels + c];
                }

                mono[i] = (short)(sum / wav.Channels);
            }
        }

        return wav.SampleRate == targetRate ? mono : Resample(mono, wav.SampleRate, targetRate);
    }

    private static short[] Resample(short[] input, int sourceRate, int targetRate)
    {
        if (input.Length == 0 || sourceRate == targetRate)
        {
            return input;
        }

        int outLength = (int)((long)input.Length * targetRate / sourceRate);
        short[] output = new short[Math.Max(1, outLength)];
        double step = (double)sourceRate / targetRate;

        for (int i = 0; i < output.Length; i++)
        {
            double src = i * step;
            int i0 = (int)src;
            int i1 = Math.Min(i0 + 1, input.Length - 1);
            double frac = src - i0;
            output[i] = (short)(input[i0] + (input[i1] - input[i0]) * frac);
        }

        return output;
    }

    private sealed class Voice
    {
        public Voice(short[] samples) => Samples = samples;

        public short[] Samples { get; }

        public int Position;
    }
}
