using OpenTK.Audio.OpenAL;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using SkiaSharp;

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

    // Helper method to load headerless WAV data
    public (ALFormat Format, byte[] Data, int SampleRate) LoadWave(Stream stream, int numChannels, int sampleRate, int bitsPerSample)
    {
        using (BinaryReader reader = new BinaryReader(stream))
        {
            // Read the raw audio data
            byte[] data = reader.ReadBytes((int)stream.Length);

            // Determine OpenAL format
            ALFormat alFormat = (numChannels == 1 && bitsPerSample == 8) ? ALFormat.Mono8 :
                                (numChannels == 1 && bitsPerSample == 16) ? ALFormat.Mono16 :
                                (numChannels == 2 && bitsPerSample == 8) ? ALFormat.Stereo8 :
                                (numChannels == 2 && bitsPerSample == 16) ? ALFormat.Stereo16 :
                                throw new NotSupportedException("Unsupported WAV format");

            return (alFormat, data, sampleRate);
        }
    }

    public (SkiaSharp.SKPicture Picture, SkiaSharp.SKColor AnalogousColor, SkiaSharp.SKColor LighterColor) GenerateWaveform(float[] amplitudes, int width, int height, string LibCanvasColor)
    {
        using var pictureRecorder = new SkiaSharp.SKPictureRecorder();
        var canvas = pictureRecorder.BeginRecording(new SkiaSharp.SKRect(0, 0, width, height));

        canvas.Clear(SkiaSharp.SKColors.Transparent);
        SKColor canvasColor = SKColor.Parse(LibCanvasColor);

        // Two-tone waveform is an analogous color to the inverse color of the canvas.
        // This means there is always contrast against the background without being ugly.
        SKColor inverseColor = new SKColor(
            (byte)(255 - canvasColor.Red),
            (byte)(255 - canvasColor.Green),
            (byte)(255 - canvasColor.Blue),
            canvasColor.Alpha
        );

        SKColor analogousColor = new SKColor(
            (byte)((inverseColor.Red + 30) % 256),
            (byte)((inverseColor.Green + 15) % 256),
            (byte)((inverseColor.Blue - 20 + 256) % 256),
            inverseColor.Alpha
        );

        SKColor lighterColor = new SKColor(
            (byte)Math.Min(analogousColor.Red + 50, 255),
            (byte)Math.Min(analogousColor.Green + 50, 255),
            (byte)Math.Min(analogousColor.Blue + 50, 255),
            analogousColor.Alpha
        );

        var paint = new SkiaSharp.SKPaint
        {
            StrokeWidth = 1,
            IsAntialias = true
        };

        var centerY = height / 2;
        var step = (float)width / amplitudes.Length; // Stepsize for each sample

        // Draw the waveform with analogousColor
        paint.Color = analogousColor;
        for (int i = 0; i < amplitudes.Length - 1; i++)
        {
            var x1 = i * step;
            var y1 = centerY - amplitudes[i] * centerY;
            var x2 = (i + 1) * step;
            var y2 = centerY - amplitudes[i + 1] * centerY;

            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        // Draw a slightly less tall waveform with lighterColor
        float scale = 0.4f;
        paint.Color = lighterColor;
        for (int i = 0; i < amplitudes.Length - 1; i++)
        {
            var x1 = i * step;
            var y1 = centerY - amplitudes[i] * centerY * scale;
            var x2 = (i + 1) * step;
            var y2 = centerY - amplitudes[i + 1] * centerY * scale;

            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        var picture = pictureRecorder.EndRecording();
        return (picture, analogousColor, lighterColor);
    }

    public float[] GetAudioAmplitudes(byte[] audioData, int bitsPerSample, int channels, int downsampleFactor = 10)
    {
        var samples = new List<float>();
        int bytesPerSample = bitsPerSample / 8;
        int blockAlign = bytesPerSample * channels;

        for (int i = 0; i < audioData.Length; i += blockAlign * downsampleFactor)
        {
            switch (bitsPerSample)
            {
                case 16:
                    // 16-bit PCM: Convert two bytes to a short and normalize
                    if (i + 2 <= audioData.Length)
                    {
                        short sample16 = BitConverter.ToInt16(audioData, i);
                        samples.Add(sample16 / 32768f); // Normalize to range [-1, 1]
                    }
                    break;

                case 8:
                    // 8-bit PCM: Normalize byte to range [-1, 1]
                    byte sample8 = audioData[i];
                    samples.Add((sample8 - 128) / 128f);
                    break;

                case 32:
                    // 32-bit float PCM: Directly convert
                    if (i + 4 <= audioData.Length)
                    {
                        float sample32 = BitConverter.ToSingle(audioData, i);
                        samples.Add(sample32);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unsupported bit depth: {bitsPerSample}");
            }
        }

        return samples.ToArray();
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