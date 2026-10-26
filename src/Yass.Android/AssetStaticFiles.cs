using System.Diagnostics.CodeAnalysis;
using Android.Content.Res;
using Yass.Bridge;

namespace Yass.Android;

/// <summary>The web build, packed into the APK's assets under <c>wwwroot/</c>.</summary>
[ExcludeFromCodeCoverage(Justification = "Needs an APK's AssetManager.")]
internal sealed class AssetStaticFiles(AssetManager assets) : IStaticFiles
{
    public Stream? Open(string path)
    {
        try
        {
            return assets.Open("wwwroot/" + path);
        }
        catch (Java.IO.FileNotFoundException)
        {
            return null;
        }
    }
}
