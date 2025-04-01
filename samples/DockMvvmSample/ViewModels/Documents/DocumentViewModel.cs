using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Dock.Model.Mvvm.Controls;
using CsXFL;

namespace Blitz.ViewModels.Documents;

public class DocumentViewModel : Dock.Model.Mvvm.Controls.Document, IDisposable
{
    public int? DocumentIndex { get; set; }

    private ZipArchive? _zipArchive;
    private Dictionary<string, byte[]> _bitmapCache;
    private Dictionary<string, byte[]> _soundCache;
    private bool _isXFL;
    private string _documentPath;

    public DocumentViewModel(bool isXFL, string documentPath)
    {
        _isXFL = isXFL;
        _documentPath = documentPath;
        _bitmapCache = new Dictionary<string, byte[]>();
        _soundCache = new Dictionary<string, byte[]>();

        if (!_isXFL)
        {
            // Initialize ZipArchive for non-XFL documents
            _zipArchive = ZipFile.Open(_documentPath, ZipArchiveMode.Read);
        }
    }

    public byte[] GetBitmapData(BitmapItem bitmap)
    {
        string href = bitmap.Href;

        // Check if the bitmap data is already cached
        if (_bitmapCache.TryGetValue(href, out var cachedData))
        {
            return cachedData;
        }

        byte[] data;

        if (_isXFL)
        {
            // Retrieve data from the file system
            string imgPath = Path.Combine(Path.GetDirectoryName(_documentPath)!, CsXFL.Library.LIBRARY_PATH, href);
            data = File.ReadAllBytes(imgPath);
        }
        else
        {
            // Retrieve data from the ZipArchive
            string imgPath = Path.Combine(CsXFL.Library.LIBRARY_PATH, href).Replace("\\", "/");
            ZipArchiveEntry? entry = _zipArchive?.GetEntry(imgPath);

            if (entry is null)
            {
                // Try to find it by normalizing slashes
                imgPath = imgPath.Replace('/', '\\').Replace('\\', '_');
                entry = _zipArchive?.Entries.FirstOrDefault(x => x.FullName.Replace('/', '\\').Replace('\\', '_') == imgPath);

                if (entry is null)
                {
                    throw new Exception($"Bitmap not found: {imgPath}");
                }
            }

            using (var ms = new MemoryStream())
            {
                entry.Open().CopyTo(ms);
                data = ms.ToArray();
            }
        }

        // Cache the retrieved data
        _bitmapCache[href] = data;

        return data;
    }

    public byte[] GetAudioData(SoundItem sound)
    {
        string href = sound.Href;

        if (string.IsNullOrEmpty(href))
        {
            throw new ArgumentException("The audio file path (Href) is null or empty.");
        }

        // Check if the audio data is already cached
        if (_soundCache.TryGetValue(href, out var cachedData))
        {
            return cachedData;
        }

        byte[] audioData;

        if (_isXFL)
        {
            // Retrieve audio data from the file system
            string fullPath = Path.Combine(Path.GetDirectoryName(_documentPath)!, CsXFL.Library.LIBRARY_PATH, href);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Audio file not found: {fullPath}");
            }
            audioData = File.ReadAllBytes(fullPath);
        }
        else
        {
            // Retrieve audio data from the ZipArchive
            string zipPath = Path.Combine(CsXFL.Library.LIBRARY_PATH, href).Replace("\\", "/");
            ZipArchiveEntry? entry = _zipArchive?.GetEntry(zipPath);

            if (entry is null)
            {
                // Try to find the entry by normalizing slashes
                zipPath = zipPath.Replace('/', '\\').Replace('\\', '_');
                entry = _zipArchive?.Entries.FirstOrDefault(x => x.FullName.Replace('/', '\\').Replace('\\', '_') == zipPath);

                if (entry is null)
                {
                    throw new Exception($"Audio file not found in archive: {zipPath}");
                }
            }

            using (MemoryStream ms = new MemoryStream())
            {
                entry.Open().CopyTo(ms);
                audioData = ms.ToArray();
            }
        }

        // Cache the retrieved audio data
        _soundCache[href] = audioData;

        return audioData;
    }

    public void Dispose()
    {
        _zipArchive?.Dispose();
    }
}
