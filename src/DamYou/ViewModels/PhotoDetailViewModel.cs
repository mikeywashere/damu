using CommunityToolkit.Mvvm.ComponentModel;
using DamYou.Data;
using DamYou.Data.Entities;
using DamYou.Data.Repositories;
using DamYou.Models;
using Microsoft.EntityFrameworkCore;

namespace DamYou.ViewModels;

/// <summary>
/// ViewModel for displaying detailed photo metadata and relations.
/// Fetches and presents color palettes, detected objects, duplicates, embeddings, and OCR text.
/// </summary>
public sealed partial class PhotoDetailViewModel : ObservableObject
{
    private readonly IPhotoRepository _photoRepository;
    private readonly DamYouDbContext _db;

    [ObservableProperty]
    private PhotoGridItem? _selectedPhoto;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private PhotoColorPalette? _colorPalette;

    [ObservableProperty]
    private List<PhotoDetectedObject> _detectedObjects = [];

    [ObservableProperty]
    private List<PhotoDuplicate> _duplicates = [];

    [ObservableProperty]
    private PhotoEmbedding? _embedding;

    [ObservableProperty]
    private PhotoOcrText? _ocrText;

    public PhotoDetailViewModel(IPhotoRepository photoRepository, DamYouDbContext db)
    {
        _photoRepository = photoRepository;
        _db = db;
    }

    /// <summary>
    /// Loads all metadata relations for the given photo.
    /// </summary>
    public async Task LoadPhotoDetailsAsync(PhotoGridItem photo, CancellationToken ct = default)
    {
        if (photo?.Photo == null)
            return;

        try
        {
            IsLoading = true;
            SelectedPhoto = photo;

            // Fetch all related metadata in parallel
            var tasks = new[]
            {
                LoadColorPaletteAsync(photo.Id, ct),
                LoadDetectedObjectsAsync(photo.Id, ct),
                LoadDuplicatesAsync(photo.Id, ct),
                LoadEmbeddingAsync(photo.Id, ct),
                LoadOcrTextAsync(photo.Id, ct)
            };

            await Task.WhenAll(tasks);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadColorPaletteAsync(int photoId, CancellationToken ct)
    {
        try
        {
            var palette = await _db.Set<PhotoColorPalette>()
                .FirstOrDefaultAsync(p => p.PhotoId == photoId, cancellationToken: ct);
            ColorPalette = palette;
        }
        catch
        {
            ColorPalette = null;
        }
    }

    private async Task LoadDetectedObjectsAsync(int photoId, CancellationToken ct)
    {
        try
        {
            var objects = await _db.Set<PhotoDetectedObject>()
                .Where(o => o.PhotoId == photoId)
                .OrderByDescending(o => o.Confidence)
                .ToListAsync(cancellationToken: ct);
            DetectedObjects = objects;
        }
        catch
        {
            DetectedObjects = [];
        }
    }

    private async Task LoadDuplicatesAsync(int photoId, CancellationToken ct)
    {
        try
        {
            var duplicates = await _db.Set<PhotoDuplicate>()
                .Where(d => d.PhotoId == photoId)
                .OrderBy(d => d.FileName)
                .ToListAsync(cancellationToken: ct);
            Duplicates = duplicates;
        }
        catch
        {
            Duplicates = [];
        }
    }

    private async Task LoadEmbeddingAsync(int photoId, CancellationToken ct)
    {
        try
        {
            var embedding = await _db.Set<PhotoEmbedding>()
                .FirstOrDefaultAsync(e => e.PhotoId == photoId, cancellationToken: ct);
            Embedding = embedding;
        }
        catch
        {
            Embedding = null;
        }
    }

    private async Task LoadOcrTextAsync(int photoId, CancellationToken ct)
    {
        try
        {
            var ocr = await _db.Set<PhotoOcrText>()
                .FirstOrDefaultAsync(o => o.PhotoId == photoId, cancellationToken: ct);
            OcrText = ocr;
        }
        catch
        {
            OcrText = null;
        }
    }

    /// <summary>
    /// Clears all loaded metadata.
    /// </summary>
    public void Clear()
    {
        SelectedPhoto = null;
        ColorPalette = null;
        DetectedObjects = [];
        Duplicates = [];
        Embedding = null;
        OcrText = null;
    }
}
