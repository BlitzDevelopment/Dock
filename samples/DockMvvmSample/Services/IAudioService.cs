using OpenTK.Audio.OpenAL;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Blitz;

public class AudioService
{
    private static readonly Lazy<AudioService> _instance = new Lazy<AudioService>(() => new AudioService());
    private ALDevice _device;
    private ALContext _context;
    private int _source;

    // Private constructor to prevent external instantiation
    public AudioService() { }

    // Public property to access the singleton instance
    public static AudioService Instance => _instance.Value;

    public void InitializeOpenAL()
    {
        // Open the default audio device
        _device = ALC.OpenDevice(null);
        if (_device == ALDevice.Null)
        {
            throw new Exception("Failed to open the default audio device.");
        }

        // Create an OpenAL context
        _context = ALC.CreateContext(_device, (int[])null);
        if (_context == ALContext.Null)
        {
            throw new Exception("Failed to create an OpenAL context.");
        }

        // Make the context current
        if (!ALC.MakeContextCurrent(_context))
        {
            throw new Exception("Failed to make the OpenAL context current.");
        }
        
        _source = AL.GenSource();
    }

    // Helper method to load WAV data
    public (ALFormat Format, byte[] Data, int SampleRate) LoadWave(Stream stream)
    {
        using (BinaryReader reader = new BinaryReader(stream))
        {
            // Read WAV header
            string chunkId = new string(reader.ReadChars(4));
            if (chunkId != "RIFF")
                throw new FormatException("Invalid WAV file");

            reader.ReadInt32(); // Chunk size
            string format = new string(reader.ReadChars(4));
            if (format != "WAVE")
                throw new FormatException("Invalid WAV file");

            // Read format chunk
            string subChunk1Id = new string(reader.ReadChars(4));
            reader.ReadInt32(); // Subchunk1 size
            reader.ReadInt16(); // Audio format
            short numChannels = reader.ReadInt16();
            int sampleRate = reader.ReadInt32();
            reader.ReadInt32(); // Byte rate
            reader.ReadInt16(); // Block align
            short bitsPerSample = reader.ReadInt16();

            // Read data chunk
            string subChunk2Id = new string(reader.ReadChars(4));
            int subChunk2Size = reader.ReadInt32();
            byte[] data = reader.ReadBytes(subChunk2Size);

            // Determine OpenAL format
            ALFormat alFormat = (numChannels == 1 && bitsPerSample == 8) ? ALFormat.Mono8 :
                                (numChannels == 1 && bitsPerSample == 16) ? ALFormat.Mono16 :
                                (numChannels == 2 && bitsPerSample == 8) ? ALFormat.Stereo8 :
                                (numChannels == 2 && bitsPerSample == 16) ? ALFormat.Stereo16 :
                                throw new NotSupportedException("Unsupported WAV format");

            return (alFormat, data, sampleRate);
        }
    }

    public void Play(byte[] audioData, ALFormat format, int sampleRate)
    {
        int buffer = AL.GenBuffer();
        int source = AL.GenSource();

        // Pin the byte array in memory
        GCHandle handle = GCHandle.Alloc(audioData, GCHandleType.Pinned);
        try
        {
            // Pass the pointer to AL.BufferData
            AL.BufferData(buffer, format, handle.AddrOfPinnedObject(), audioData.Length, sampleRate);
        }
        finally
        {
            // Free the pinned memory
            handle.Free();
        }

        // Attach buffer to source and play
        AL.Source(source, ALSourcei.Buffer, buffer);
        AL.SourcePlay(source);
    }

}
