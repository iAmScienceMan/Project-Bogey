using System;
using System.Runtime.InteropServices;

namespace Content.Renderer.Audio.Backends;

public sealed unsafe class MacAudioQueueBackend : IAudioBackend
{
    private const string Lib = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const uint FormatLinearPcm = 0x6C70636D;
    private const uint FlagsSignedPacked = 0x4 | 0x8;
    private const int BufferFrames = 4096;
    private const int BufferCount = 3;

    private readonly OutputCallback _callback;
    private readonly IntPtr[] _buffers = new IntPtr[BufferCount];
    private AudioRender? _render;
    private IntPtr _queue;
    private bool _disposed;

    public MacAudioQueueBackend()
    {
        _callback = HandleBuffer;
    }

    public int SampleRate => 44100;

    public void Start(AudioRender render)
    {
        _render = render;

        AudioStreamBasicDescription format = new()
        {
            SampleRate = 44100.0,
            FormatID = FormatLinearPcm,
            FormatFlags = FlagsSignedPacked,
            BytesPerPacket = 2,
            FramesPerPacket = 1,
            BytesPerFrame = 2,
            ChannelsPerFrame = 1,
            BitsPerChannel = 16,
            Reserved = 0,
        };

        Check(AudioQueueNewOutput(ref format, _callback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out _queue),
            nameof(AudioQueueNewOutput));

        for (int i = 0; i < BufferCount; i++)
        {
            Check(AudioQueueAllocateBuffer(_queue, BufferFrames * 2, out _buffers[i]),
                nameof(AudioQueueAllocateBuffer));
            Fill(_buffers[i]);
        }

        Check(AudioQueueStart(_queue, IntPtr.Zero), nameof(AudioQueueStart));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_queue != IntPtr.Zero)
        {
            AudioQueueStop(_queue, 1);
            AudioQueueDispose(_queue, 1);
            _queue = IntPtr.Zero;
        }

        _render = null;
    }

    private void HandleBuffer(IntPtr userData, IntPtr queue, IntPtr buffer)
    {
        try
        {
            Fill(buffer);
        }
        catch
        {
        }
    }

    private void Fill(IntPtr buffer)
    {
        AudioQueueBuffer* buf = (AudioQueueBuffer*)buffer;
        int frames = (int)(buf->AudioDataBytesCapacity / 2);
        Span<short> output = new(buf->AudioData, frames);
        output.Clear();
        _render?.Invoke(output);
        buf->AudioDataByteSize = (uint)(frames * 2);
        AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero);
    }

    private static void Check(int status, string call)
    {
        if (status != 0)
        {
            throw new InvalidOperationException($"{call} failed with OSStatus {status}.");
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OutputCallback(IntPtr userData, IntPtr queue, IntPtr buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioStreamBasicDescription
    {
        public double SampleRate;
        public uint FormatID;
        public uint FormatFlags;
        public uint BytesPerPacket;
        public uint FramesPerPacket;
        public uint BytesPerFrame;
        public uint ChannelsPerFrame;
        public uint BitsPerChannel;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioQueueBuffer
    {
        public uint AudioDataBytesCapacity;
        public void* AudioData;
        public uint AudioDataByteSize;
        public void* UserData;
        public uint PacketDescriptionCapacity;
        public void* PacketDescriptions;
        public uint PacketDescriptionCount;
    }

    [DllImport(Lib)]
    private static extern int AudioQueueNewOutput(
        ref AudioStreamBasicDescription format,
        OutputCallback callback,
        IntPtr userData,
        IntPtr runLoop,
        IntPtr runLoopMode,
        uint flags,
        out IntPtr queue);

    [DllImport(Lib)]
    private static extern int AudioQueueAllocateBuffer(IntPtr queue, uint byteSize, out IntPtr buffer);

    [DllImport(Lib)]
    private static extern int AudioQueueEnqueueBuffer(
        IntPtr queue,
        IntPtr buffer,
        uint numPacketDescs,
        IntPtr packetDescs);

    [DllImport(Lib)]
    private static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);

    [DllImport(Lib)]
    private static extern int AudioQueueStop(IntPtr queue, byte immediate);

    [DllImport(Lib)]
    private static extern int AudioQueueDispose(IntPtr queue, byte immediate);
}
