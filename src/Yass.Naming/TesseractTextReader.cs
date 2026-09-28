using Microsoft.Extensions.Logging;
using NAPS2.Images.ImageSharp;
using NAPS2.Ocr;
using NAPS2.Scan;
using Yass.Core.Config;

namespace Yass.Naming;

/// <param name="Text">The line as read.</param>
/// <param name="FontSize">Point size Tesseract measured, which is how a heading stands out.</param>
/// <param name="Top">Distance from the top of the page, in pixels.</param>
public sealed record PageLine(string Text, int FontSize, int Top);

/// <summary>
/// Reads text off scanned pages with NAPS2's Tesseract engine, which shells out to the
/// <c>tesseract</c> binary. The binary is a runtime dependency, not a package: when it is
/// not on PATH this reader reports itself unavailable rather than failing a scan.
/// </summary>
public sealed class TesseractTextReader : IDisposable
{
    private readonly ScanningContext _context;
    private readonly TesseractOcrEngine? _engine;
    private readonly ILogger<TesseractTextReader> _logger;

    public TesseractTextReader(ILogger<TesseractTextReader> logger)
    {
        _logger = logger;
        _context = new ScanningContext(new ImageSharpImageContext()) { Logger = logger };
        if (Locate("tesseract") is null)
        {
            logger.LogInformation("tesseract is not on PATH; page text will not be read");
        }
        else
        {
            _engine = TesseractOcrEngine.System();
        }
    }

    public bool Available => _engine is not null;

    /// <summary>
    /// The lines of one page with the size they were set in, biggest first among the top of
    /// the page. Font size is what separates a title from body text, and it is the only
    /// signal here that does not need a model.
    /// </summary>
    public async Task<IReadOnlyList<PageLine>> ReadLinesAsync(string imagePath, NamingConfig naming, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(naming);
        if (_engine is null)
        {
            return [];
        }

        try
        {
            var options = new OcrParams(naming.OcrLanguage, OcrMode.Fast, naming.TimeoutSeconds);
            OcrResult? result = await _engine.ProcessImage(_context, imagePath, options, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return [];
            }

            return [.. result.Lines
                .Select(l => new PageLine(l.Text.Trim(), l.FontSize, l.Bounds.Item2))
                .Where(l => l.Text.Length > 0)];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not read text from {Path}", imagePath);
            return [];
        }
    }

    /// <summary>Text of one page, or an empty string when nothing could be read.</summary>
    public async Task<string> ReadAsync(string imagePath, NamingConfig naming, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(naming);
        if (_engine is null)
        {
            return "";
        }

        try
        {
            var options = new OcrParams(naming.OcrLanguage, OcrMode.Fast, naming.TimeoutSeconds);
            OcrResult? result = await _engine.ProcessImage(_context, imagePath, options, cancellationToken).ConfigureAwait(false);
            return result is null ? "" : string.Join('\n', result.Lines.Select(l => l.Text));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Failing to read a page is a worse suggestion, not a broken scan.
            _logger.LogWarning(ex, "Could not read text from {Path}", imagePath);
            return "";
        }
    }

    private static string? Locate(string binary)
    {
        string? paths = Environment.GetEnvironmentVariable("PATH");
        foreach (string dir in (paths ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(dir, binary);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public void Dispose() => _context.Dispose();
}
