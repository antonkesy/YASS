using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using NAPS2.Wia;

namespace Yass.Scanning;

/// <summary>NAPS2's WIA ids carry no type, so this asks WIA for each device's type to filter out cameras.</summary>
public static class WiaDeviceKinds
{
    private const int StiDigitalCamera = 2;
    private const int StiStreamingVideo = 3;

    // DIP_DEV_TYPE holds the STI device type in its high word: 1 scanner, 2 camera, 3 video.
    public static bool IsCamera(int deviceType) => (deviceType >> 16) is StiDigitalCamera or StiStreamingVideo;

    // Read on an STA thread, as COM wants. Throws what WIA throws.
    [SupportedOSPlatform("windows")]
    [ExcludeFromCodeCoverage(Justification = "Needs Windows and its WIA service.")]
    public static IReadOnlySet<string> CameraIds(TimeSpan timeout)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var manager = new WiaDeviceManager();
                foreach (WiaDeviceInfo info in manager.GetDeviceInfos())
                {
                    using (info)
                    {
                        object? type = info.Properties.GetOrNull(WiaPropertyId.DIP_DEV_TYPE)?.Value;
                        if (type is not null
                            && IsCamera(Convert.ToInt32(type, System.Globalization.CultureInfo.InvariantCulture))
                            && info.Properties.GetOrNull(WiaPropertyId.DIP_DEV_ID)?.Value is string id)
                        {
                            ids.Add(id);
                        }
                    }
                }
            }
#pragma warning disable CA1031 // Rethrown on the caller's thread below.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failure = ex;
            }
        })
        { IsBackground = true, Name = "WIA device types" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(timeout))
        {
            throw new TimeoutException("WIA did not list its devices in time.");
        }

        return failure is null ? ids : throw new InvalidOperationException("WIA could not list its devices.", failure);
    }
}
