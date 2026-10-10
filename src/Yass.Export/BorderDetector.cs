using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Yass.Core.Domain;

namespace Yass.Export;

/// <summary>Finds the dark frame (lid or bed past the paper's edge) as a <see cref="CropBox"/> on the scan as it came.</summary>
public static class BorderDetector
{
    public const int ProbeSize = 600;

    public const double MaxEdgePercent = 30;

    private const double MarginPercent = 0.5;

    private const double DarkFactor = 0.6;

    // More than this share of dark pixels is frame, even where the paper starts mid-line.
    private const double DarkShare = 0.6;

    public static CropBox? Detect(Image source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using Image<L8> probe = source.CloneAs<L8>();
        probe.Mutate(ctx => ctx.Resize(new ResizeOptions { Size = new Size(ProbeSize, ProbeSize), Mode = ResizeMode.Max }));
        return Detect(probe);
    }

    public static CropBox? Detect(Image<L8> image)
    {
        ArgumentNullException.ThrowIfNull(image);
        int width = image.Width;
        int height = image.Height;
        byte[] pixels = new byte[width * height];
        image.CopyPixelDataTo(pixels);

        double paper = Paper(pixels, width, height);
        double dark = paper * DarkFactor;

        // Smoothed over neighbours so one stray line of dust or shadow does not decide the edge.
        bool[] rows = Smooth(Enumerable.Range(0, height).Select(y => IsFrame(pixels, y * width, 1, width, dark)).ToArray());
        bool[] cols = Smooth(Enumerable.Range(0, width).Select(x => IsFrame(pixels, x, width, height, dark)).ToArray());

        double top = Edge(rows, fromEnd: false);
        double bottom = Edge(rows, fromEnd: true);
        double left = Edge(cols, fromEnd: false);
        double right = Edge(cols, fromEnd: true);

        // Frame on every line means no page to find; take nothing rather than guess.
        if (top + bottom > CropBox.MaxTotalPercent || left + right > CropBox.MaxTotalPercent)
        {
            return null;
        }

        var box = new CropBox(top, right, bottom, left);
        return box.IsEmpty() ? null : box;
    }

    private static double Paper(byte[] pixels, int width, int height)
    {
        var middle = new List<byte>((width / 2) * (height / 2));
        for (int y = height / 4; y < height * 3 / 4; y++)
        {
            for (int x = width / 4; x < width * 3 / 4; x++)
            {
                middle.Add(pixels[(y * width) + x]);
            }
        }

        if (middle.Count == 0)
        {
            return 255;
        }

        middle.Sort();
        return middle[middle.Count / 2];
    }

    private static bool IsFrame(byte[] pixels, int start, int stride, int count, double dark)
    {
        long sum = 0;
        int darkCount = 0;
        for (int i = 0, at = start; i < count; i++, at += stride)
        {
            byte v = pixels[at];
            sum += v;
            if (v < dark)
            {
                darkCount++;
            }
        }

        return count > 0 && ((double)sum / count < dark || (double)darkCount / count > DarkShare);
    }

    private static bool[] Smooth(bool[] lines)
    {
        bool[] smoothed = new bool[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            int votes = 0;
            int seen = 0;
            for (int j = Math.Max(0, i - 1); j <= Math.Min(lines.Length - 1, i + 1); j++)
            {
                seen++;
                if (lines[j])
                {
                    votes++;
                }
            }

            smoothed[i] = votes * 2 > seen;
        }

        return smoothed;
    }

    private static double Edge(bool[] lines, bool fromEnd)
    {
        int run = 0;
        while (run < lines.Length && lines[fromEnd ? lines.Length - 1 - run : run])
        {
            run++;
        }

        if (run == 0)
        {
            return 0;
        }

        double percent = (run * 100.0 / lines.Length) + MarginPercent;
        return Math.Round(Math.Min(MaxEdgePercent, percent), 2);
    }
}
