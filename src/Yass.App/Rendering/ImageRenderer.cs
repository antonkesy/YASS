using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using Yass.App.Config;
using Yass.Core.Domain;
using Yass.Export;

namespace Yass.App.Rendering;

/// <summary>At most <c>max_concurrent_renders</c> bitmaps are decoded at once.</summary>
public sealed class ImageRenderer : IDisposable
{
    private readonly SemaphoreSlim _slots;

    public ImageRenderer(ConfigStore config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _slots = new SemaphoreSlim(config.Current.Processing.MaxConcurrentRenders);
    }

    public async Task RenderAsync(string sourcePath, Page page, ColorMode scanColor, int maxWidth, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using Image image = await Image.LoadAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            PagePipeline.Apply(image, RenderSpec.Of(page, scanColor), maxWidth);

            var encoder = new WebpEncoder { Quality = 80, FileFormat = WebpFileFormatType.Lossy };
            await image.SaveAsync(destination, encoder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    public async Task<CropBox?> DetectBordersAsync(string sourcePath, CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using Image image = await Image.LoadAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            return BorderDetector.Detect(image);
        }
        finally
        {
            _slots.Release();
        }
    }

    // For work that decodes pages elsewhere (export), so it counts against the same limit.
    public async Task<IDisposable> ReserveAsync(CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Slot(_slots);
    }

    public void Dispose() => _slots.Dispose();

    private sealed class Slot(SemaphoreSlim slots) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                slots.Release();
            }
        }
    }
}
