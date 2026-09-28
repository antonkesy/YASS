namespace Yass.Export;

/// <summary>What the non-date tokens of a filename template stand for.</summary>
/// <param name="Preset">Name of the preset the scan ran with, or "Custom".</param>
/// <param name="Counter">A number that makes repeated names distinct.</param>
/// <param name="Dpi">Resolution the scan ran at.</param>
/// <param name="Color">Colour mode the scan ran in.</param>
public sealed record TemplateFacts(string Preset = "Scan", int Counter = 1, int Dpi = 300, string Color = "Color");
