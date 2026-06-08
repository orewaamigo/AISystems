namespace ImageAI.Models;

public enum CommandType
{
    Rotate, Flip, Resize, ExtractChannel, DetectObjects,
    Blur, Grayscale, StyleTransfer, Adjust, EdgeDetection,
    RemoveRegion, Thermal, Unknown
}

public class ImageCommand
{
    public CommandType Type      { get; set; }
    public string?     Message   { get; set; }
    public string?     Reply     { get; set; }

    public double? Angle      { get; set; }
    public string? Direction  { get; set; }
    public int?    Width      { get; set; }
    public int?    Height     { get; set; }
    public string? Channel    { get; set; }
    public string? Target     { get; set; }
    public string? Style      { get; set; }
    public double? Brightness { get; set; }
    public double? Contrast   { get; set; }
    public double? Threshold1 { get; set; }
    public double? Threshold2 { get; set; }
    public int?    X          { get; set; }
    public int?    Y          { get; set; }
    public int?    Strength   { get; set; }
}
