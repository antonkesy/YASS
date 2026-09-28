using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Yass.Core.Domain;
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

    public async Task RenderAsync(string sourcePath, Rotation rotation, ColorMode color, int maxWidth, Stream destination, CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using Image image = await Image.LoadAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            image.Mutate(ctx =>
            {
                if (rotation != Rotation.R0)
                {
                    ctx.Rotate((float)(int)rotation);
                }

                switch (color)
                {
                    case ColorMode.Grayscale:
                        ctx.Grayscale();
                        break;
                    case ColorMode.Bw:
                        ctx.Grayscale().BinaryThreshold(0.55f);
                        break;
                    case ColorMode.Color:
                    default:
                        break;
                }

                if (ctx.GetCurrentSize().Width > maxWidth)
                {
                    ctx.Resize(maxWidth, 0);
                }
            });

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
