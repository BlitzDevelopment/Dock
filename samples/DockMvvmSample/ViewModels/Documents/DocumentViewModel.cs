﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CsXFL;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Blitz.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls;

namespace Blitz.ViewModels.Documents;

public partial class DocumentViewModel : Dock.Model.Mvvm.Controls.Document, IDisposable
{
    #region Document Properties
    public int DocumentIndex { get; set; }
    private CsXFL.Document _workingCsXFLDoc;
    private bool _isXFL;
    private string _documentPath;
    private ZipArchive? _zipArchive;
    #endregion

    #region Caches
    private Dictionary<string, byte[]> _bitmapCache;
    private Dictionary<string, byte[]> _soundCache;
    #endregion

    public DocumentViewModel(bool isXFL, string documentPath)
    {
        _isXFL = isXFL;
        _documentPath = documentPath;
        _bitmapCache = new Dictionary<string, byte[]>();
        _soundCache = new Dictionary<string, byte[]>();

        if (!_isXFL)
        {
            InitializeZipArchive();
        }

        App.EventAggregator.Subscribe<ActiveDocumentChangedEvent>(OnActiveDocumentChanged);
    }

    private void OnActiveDocumentChanged(ActiveDocumentChangedEvent e)
    {
        _workingCsXFLDoc = An.GetDocument(e.Document.DocumentIndex);
    }

    [RelayCommand]
    private void CenterStage()
    {
        App.EventAggregator.Publish(new CanvasActionCenterEvent(DocumentIndex));
    }

    [RelayCommand]
    private void ClipCanvas()
    {
        App.EventAggregator.Publish(new CanvasActionToggleClipEvent(DocumentIndex));
    }

    public void InitializeZipArchive()
    {
        // Logic to reinstantiate the _zipArchive
        _zipArchive = ZipFile.Open(_documentPath, ZipArchiveMode.Read);
    }


    // MARK: Bitmap Retrieval
    public byte[] GetBitmapData(BitmapItem bitmap)
    {
        string href = bitmap.Href;
        byte[] data;

        // Check if the bitmap data is already cached
        if (_bitmapCache.TryGetValue(href, out var cachedData))
        {
            return cachedData;
        }

        if (_isXFL)
        {
            // Retrieve data from the file system
            string imgPath = Path.Combine(Path.GetDirectoryName(_documentPath)!, CsXFL.Library.LIBRARY_PATH, href);
            if (File.Exists(imgPath))
            {
                data = File.ReadAllBytes(imgPath);
            }
            else
            {
                Log.Error($"[DocumentViewModel:XFL] Bitmap file not found: {imgPath}. Attempting .dat decryption.");
                return DecryptBitmapDat(bitmap);
            }
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
                    Log.Error($"[DocumentViewModel:FLA] Bitmap file not found in archive: {imgPath}. Attempting .dat decryption.");
                    return DecryptBitmapDat(bitmap);
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

    public byte[] DecryptBitmapDat(BitmapItem bitmap)
    {
        string href = bitmap.BitmapDataHRef;
        byte[] data;

        if (_isXFL)
        {
            // Retrieve data from the file system
            string imgPath = Path.Combine(Path.GetDirectoryName(_documentPath)!, "bin", href);
            if (File.Exists(imgPath))
            {
                data = File.ReadAllBytes(imgPath);
            }
            else
            {
                Log.Error($"[DocumentViewModel:XFL] Bitmap file not found: {imgPath}. Cannot display bitmap.");
                return GetEmptyPng();
            }
        }
        else
        {
            // Retrieve data from the ZipArchive
            string imgPath = Path.Combine("bin", href).Replace("\\", "/");
            ZipArchiveEntry? entry = _zipArchive?.GetEntry(imgPath);

            if (entry is null)
            {
                // Try to find it by normalizing slashes
                imgPath = imgPath.Replace('/', '\\').Replace('\\', '_');
                entry = _zipArchive?.Entries.FirstOrDefault(x => x.FullName.Replace('/', '\\').Replace('\\', '_') == imgPath);

                if (entry is null)
                {
                    Log.Error($"[DocumentViewModel:FLA] Bitmap file not found in archive: {imgPath}. Cannot display bitmap.");
                    return GetEmptyPng();
                }
            }

            using (var ms = new MemoryStream())
            {
                entry.Open().CopyTo(ms);
                data = ms.ToArray();
            }
        }

        using (var image = CsXFL.ImageUtils.ConvertDatToRawImage(data))
        {
            using (var ms = new MemoryStream())
            {
                // Save the image as a PNG to the memory stream using PngEncoder
                image.Save(ms, new PngEncoder());
                data = ms.ToArray();
            }
        }

        // Cache the retrieved data
        _bitmapCache[href] = data;

        return data;
    }

    private byte[] GetEmptyPng()
    {
        using (var image = new Image<Rgba32>(1, 1))
        {
            image[0, 0] = new Rgba32(0, 0, 0, 0); // Set the single pixel to transparent
            using (var ms = new MemoryStream())
            {
                image.Save(ms, new PngEncoder()); // Save the image as PNG
                return ms.ToArray();
            }
        }
    }

    // MARK: Sound Retrieval
    public byte[] GetAudioData(SoundItem sound)
    {
        string href = sound.Href;
        byte[] audioData;

        if (string.IsNullOrEmpty(href))
        {
            throw new ArgumentException("The audio file path (Href) is null or empty.");
        }

        // Check if the audio data is already cached
        if (_soundCache.TryGetValue(href, out var cachedData))
        {
            return cachedData;
        }

        if (_isXFL)
        {
            // Retrieve audio data from the file system
            string fullPath = Path.Combine(Path.GetDirectoryName(_documentPath)!, CsXFL.Library.LIBRARY_PATH, href);
            if (!File.Exists(fullPath))
            {
                Log.Error($"[DocumentViewModel:XFL] Audio file not found in archive: {fullPath}. Attempting .dat decryption.");
                return DecryptAudioDat(sound);
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
                    Log.Error($"[DocumentViewModel:FLA] Audio file not found in archive: {zipPath}. Attempting .dat decryption.");
                    return DecryptAudioDat(sound);
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

    public byte[] DecryptAudioDat(SoundItem sound)
    {
        string href = sound.SoundDataHRef;
        byte[] audioData;

        // Check if the audio data is already cached
        if (_soundCache.TryGetValue(href, out var cachedData))
        {
            return cachedData;
        }

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
            string zipPath = Path.Combine("bin", href).Replace("\\", "/");
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
        _bitmapCache.Clear();
        _soundCache.Clear();
        _zipArchive?.Dispose();
        _zipArchive = null;
    }
}
