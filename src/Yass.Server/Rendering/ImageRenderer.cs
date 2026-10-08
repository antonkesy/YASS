using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Yass.Core.Domain;
using Yass.Export;
using Yass.Server.Config;

namespace Yass.Server.Rendering;

/// <summary>Turns a source PNG plus edits into a WebP of bounded width. At most <c>max_concurrent_renders</c> bitmaps are decoded at once.</summary>
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
            PagePipeline.Apply(image, RenderSpec.Of(page, scanColor));
            if (image.Width > maxWidth)
            {
                image.Mutate(ctx => ctx.Resize(maxWidth, 0));
            }

            var encoder = new WebpEncoder { Quality = 80, FileFormat = WebpFileFormatType.Lossy };
            await image.SaveAsync(destination, encoder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    public void Dispose() => _slots.Dispose();
}
