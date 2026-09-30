using System.Globalization;

namespace Yass.Fake;

/// <summary>
/// Scripts what the <see cref="FakeScannerBackend"/> does. Parsable from a compact string for the
/// <c>--fake</c> dev mode, e.g. <c>pages=5,blank=3,error=4,delay=200ms,nodevice</c>.
/// </summary>
public sealed record FakeScenario
{
    /// <summary>Pages in the feeder; the enumerable ends after this many (feeder empty).</summary>
    public int PageCount { get; init; } = 5;

    /// <summary>1-based page numbers that come out blank.</summary>
    public IReadOnlySet<int> BlankPages { get; init; } = new HashSet<int> { 3 };

    /// <summary>Throw after this many pages have been produced; <c>null</c> = never.</summary>
    public int? ErrorAfter { get; init; }

    /// <summary>Simulated time per page.</summary>
    public TimeSpan PageDelay { get; init; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Report no device at all.</summary>
    public bool NoDevice { get; init; }

    public string DeviceName { get; init; } = "YASS Fake ADF";

    /// <summary>
    /// Extra scanners the fake reports beside <see cref="DeviceName"/>, so the picker has more
    /// than one thing to pick. They all feed the same pages: the point is exercising the choice,
    /// not pretending each imaginary machine scans differently.
    /// </summary>
    public IReadOnlyList<string> ExtraDevices { get; init; } = [];

    /// <summary>Every device this fake reports, the configured one first.</summary>
    public IReadOnlyList<string> AllDevices => [DeviceName, .. ExtraDevices];

    public static FakeScenario Default { get; } = new();

    /// <summary>Instant, deterministic scenario for unit tests.</summary>
    public static FakeScenario Pages(int count, params int[] blank) =>
        new() { PageCount = count, BlankPages = new HashSet<int>(blank), PageDelay = TimeSpan.Zero };

    /// <summary>Parses <c>key=value</c> pairs separated by commas. Unknown keys throw.</summary>
    public static FakeScenario Parse(string? spec)
    {
        var scenario = new FakeScenario();
        if (string.IsNullOrWhiteSpace(spec))
        {
            return scenario;
        }

        foreach (string part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] kv = part.Split('=', 2);
            string key = kv[0].ToLowerInvariant();
            string value = kv.Length > 1 ? kv[1] : "";
            scenario = key switch
            {
                "pages" => scenario with { PageCount = Int(value) },
                "blank" => scenario with { BlankPages = value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Int).ToHashSet() },
                "error" => scenario with { ErrorAfter = Int(value) },
                "delay" => scenario with { PageDelay = Duration(value) },
                "nodevice" => scenario with { NoDevice = true },
                "device" => scenario with { DeviceName = value },
                "devices" => scenario with { ExtraDevices = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) },
                _ => throw new FormatException($"Unknown fake scenario key '{key}'. Known: pages, blank, error, delay, nodevice, device, devices."),
            };
        }

        return scenario;
    }

    private static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static TimeSpan Duration(string value) =>
        value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromMilliseconds(Int(value[..^2]))
            : value.EndsWith('s')
                ? TimeSpan.FromSeconds(Int(value[..^1]))
                : TimeSpan.FromMilliseconds(Int(value));
}
