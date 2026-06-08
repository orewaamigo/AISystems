using ImageAI.Models;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Size  = OpenCvSharp.Size;

namespace ImageAI.Services;

public class ImageProcessor
{
    public Mat Process(Mat src, ImageCommand cmd) => cmd.Type switch
    {
        CommandType.Rotate         => Rotate(src,  cmd.Angle      ?? 90),
        CommandType.Flip           => Flip(src,    cmd.Direction  ?? "horizontal"),
        CommandType.Resize         => Resize(src,  cmd.Width      ?? 0, cmd.Height ?? 0),
        CommandType.ExtractChannel => ExtractChannel(src, cmd.Channel ?? "red"),
        CommandType.DetectObjects  => DetectObjects(src, cmd.Target  ?? "contours"),
        CommandType.Blur           => Blur(src,    cmd.Strength   ?? 5),
        CommandType.Grayscale      => ToGrayscale(src),
        CommandType.StyleTransfer  => ApplyStyle(src,  cmd.Style  ?? "cartoon"),
        CommandType.Adjust         => AdjustBC(src, cmd.Brightness ?? 0, cmd.Contrast ?? 1.0),
        CommandType.EdgeDetection  => DetectEdges(src, cmd.Threshold1 ?? 50, cmd.Threshold2 ?? 150),
        CommandType.RemoveRegion   => RemoveRegion(src, cmd.X ?? 0, cmd.Y ?? 0, cmd.Width ?? 100, cmd.Height ?? 100),
        CommandType.Thermal        => ApplyThermal(src),
        _                          => src.Clone()
    };

    private static Mat Rotate(Mat src, double angle)
    {
        var center = new Point2f(src.Width / 2f, src.Height / 2f);
        using var m = Cv2.GetRotationMatrix2D(center, angle, 1.0);
        double rad  = Math.Abs(angle * Math.PI / 180.0);
        int newW = (int)(src.Height * Math.Abs(Math.Sin(rad)) + src.Width  * Math.Abs(Math.Cos(rad)));
        int newH = (int)(src.Height * Math.Abs(Math.Cos(rad)) + src.Width  * Math.Abs(Math.Sin(rad)));
        m.At<double>(0, 2) += (newW - src.Width)  / 2.0;
        m.At<double>(1, 2) += (newH - src.Height) / 2.0;
        var dst = new Mat();
        Cv2.WarpAffine(src, dst, m, new Size(newW, newH), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        return dst;
    }

    private static Mat Flip(Mat src, string direction)
    {
        var dst  = new Mat();
        var mode = direction.ToLowerInvariant() switch
        {
            "vertical" => FlipMode.X,
            "both"     => FlipMode.XY,
            _          => FlipMode.Y
        };
        Cv2.Flip(src, dst, mode);
        return dst;
    }

    private static Mat Resize(Mat src, int width, int height)
    {
        if (width <= 0 && height <= 0) return src.Clone();
        int w = width  > 0 ? width  : (int)(src.Width  * ((double)height / src.Height));
        int h = height > 0 ? height : (int)(src.Height * ((double)width  / src.Width));
        var dst = new Mat();
        Cv2.Resize(src, dst, new Size(w, h));
        return dst;
    }

    private static Mat ExtractChannel(Mat src, string channel)
    {
        Mat[] bgr = Cv2.Split(src);
        Mat result;
        switch (channel.ToLowerInvariant())
        {
            case "red":   result = Merge3(bgr, false, false, true);  break;
            case "green": result = Merge3(bgr, false, true,  false); break;
            case "blue":  result = Merge3(bgr, true,  false, false); break;
            default:
            {
                using var hsv = new Mat();
                Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
                Mat[] h = Cv2.Split(hsv);
                int idx = channel.ToLowerInvariant() switch { "hue" => 0, "saturation" => 1, _ => 2 };
                result = new Mat();
                Cv2.CvtColor(h[idx], result, ColorConversionCodes.GRAY2BGR);
                foreach (var c in h) c.Dispose();
                break;
            }
        }
        foreach (var c in bgr) c.Dispose();
        return result;
    }

    private static Mat Merge3(Mat[] bgr, bool b, bool g, bool r)
    {
        using var z = new Mat(bgr[0].Size(), bgr[0].Type(), Scalar.Black);
        var res = new Mat();
        Cv2.Merge(new[] { b ? bgr[0] : z, g ? bgr[1] : z, r ? bgr[2] : z }, res);
        return res;
    }

    private Mat DetectObjects(Mat src, string target)
    {
        var result = src.Clone();
        switch (target.ToLowerInvariant())
        {
            case "red_objects":   HighlightColor(src, result, "red");   break;
            case "green_objects": HighlightColor(src, result, "green"); break;
            case "blue_objects":  HighlightColor(src, result, "blue");  break;
            case "skin":          HighlightColor(src, result, "skin");  break;
            default:              DrawContours(src, result);            break;
        }
        return result;
    }

    private static Mat ApplyThermal(Mat src)
    {
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        var result = new Mat();
        Cv2.ApplyColorMap(gray, result, ColormapTypes.Inferno);
        return result;
    }

    private static void DrawContours(Mat src, Mat result)
    {
        using var gray    = new Mat();
        using var blurred = new Mat();
        using var edges   = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        Cv2.Canny(blurred, edges, 50, 150);
        Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var big = contours.Where(c => Cv2.ContourArea(c) > 500).ToArray();
        Cv2.DrawContours(result, big, -1, Scalar.LimeGreen, 2);
    }

    private static void HighlightColor(Mat src, Mat result, string color)
    {
        using var hsv  = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
        switch (color)
        {
            case "red":
                using (var m1 = new Mat()) using (var m2 = new Mat())
                {
                    Cv2.InRange(hsv, new Scalar(0,   120, 70), new Scalar(10,  255, 255), m1);
                    Cv2.InRange(hsv, new Scalar(170, 120, 70), new Scalar(180, 255, 255), m2);
                    Cv2.BitwiseOr(m1, m2, mask);
                }
                break;
            case "green": Cv2.InRange(hsv, new Scalar(36, 100, 100), new Scalar(86,  255, 255), mask); break;
            case "blue":  Cv2.InRange(hsv, new Scalar(100, 150,  0), new Scalar(140, 255, 255), mask); break;
            case "skin":  Cv2.InRange(hsv, new Scalar(0,   20,  70), new Scalar(20,  255, 255), mask); break;
        }
        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        foreach (var c in contours.Where(c => Cv2.ContourArea(c) > 300))
            Cv2.Rectangle(result, Cv2.BoundingRect(c), Scalar.Yellow, 3);
    }

    private static Mat Blur(Mat src, int strength)
    {
        int k   = Math.Clamp(strength, 1, 20) * 2 + 1;
        var dst = new Mat();
        Cv2.GaussianBlur(src, dst, new Size(k, k), 0);
        return dst;
    }

    private static Mat ToGrayscale(Mat src)
    {
        using var gray = new Mat();
        var dst = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(gray, dst, ColorConversionCodes.GRAY2BGR);
        return dst;
    }

    private static Mat ApplyStyle(Mat src, string style) => style.ToLowerInvariant() switch
    {
        "anime"        => ApplyAnime(src),
        "disney"       => ApplyDisney(src),
        "sketch"       => ApplySketch(src),
        "oil_painting" => ApplyOil(src),
        "watercolor"   => ApplyWatercolor(src),
        _              => ApplyCartoon(src)
    };

    private static Mat ApplyCartoon(Mat src)
    {
        Mat color = src.Clone();
        for (int i = 0; i < 4; i++) { var t = new Mat(); Cv2.BilateralFilter(color, t, 9, 75, 75); color.Dispose(); color = t; }
        using var gray = new Mat(); using var blurred = new Mat(); using var edges = new Mat(); using var edgesBGR = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.MedianBlur(gray, blurred, 7);
        Cv2.AdaptiveThreshold(blurred, edges, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 9, 9);
        Cv2.CvtColor(edges, edgesBGR, ColorConversionCodes.GRAY2BGR);
        var result = new Mat(); Cv2.BitwiseAnd(color, edgesBGR, result); color.Dispose(); return result;
    }

    private static Mat ApplyAnime(Mat src)
    {
        using var cartoon = ApplyCartoon(src);
        using var hsv = new Mat();
        Cv2.CvtColor(cartoon, hsv, ColorConversionCodes.BGR2HSV);
        Mat[] ch = Cv2.Split(hsv);
        var b = new Mat(); ch[1].ConvertTo(b, -1, 1.6, 0); ch[1].Dispose(); ch[1] = b;
        using var merged = new Mat(); Cv2.Merge(ch, merged);
        var result = new Mat(); Cv2.CvtColor(merged, result, ColorConversionCodes.HSV2BGR);
        foreach (var c in ch) c.Dispose(); return result;
    }

    private static Mat ApplyDisney(Mat src)
    {
        Mat color = src.Clone();
        for (int i = 0; i < 7; i++) { var t = new Mat(); Cv2.BilateralFilter(color, t, 15, 80, 80); color.Dispose(); color = t; }
        using var gray = new Mat(); using var blurred = new Mat(); using var edges = new Mat(); using var edgesBGR = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        Cv2.AdaptiveThreshold(blurred, edges, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11, 5);
        Cv2.CvtColor(edges, edgesBGR, ColorConversionCodes.GRAY2BGR);
        using var hsv = new Mat(); Cv2.CvtColor(color, hsv, ColorConversionCodes.BGR2HSV);
        Mat[] ch = Cv2.Split(hsv);
        var s = new Mat(); ch[1].ConvertTo(s, -1, 1.3,  0);  ch[1].Dispose(); ch[1] = s;
        var v = new Mat(); ch[2].ConvertTo(v, -1, 1.1, 15);  ch[2].Dispose(); ch[2] = v;
        using var merged = new Mat(); Cv2.Merge(ch, merged);
        using var bright = new Mat(); Cv2.CvtColor(merged, bright, ColorConversionCodes.HSV2BGR);
        foreach (var c in ch) c.Dispose();
        var result = new Mat(); Cv2.BitwiseAnd(bright, edgesBGR, result); color.Dispose(); return result;
    }

    private static Mat ApplySketch(Mat src)
    {
        using var gray = new Mat(); using var inv = new Mat(); using var blurInv = new Mat(); using var denom = new Mat(); using var sketch = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.BitwiseNot(gray, inv);
        Cv2.GaussianBlur(inv, blurInv, new Size(21, 21), 0);
        Cv2.BitwiseNot(blurInv, denom);
        Cv2.Divide(gray, denom, sketch, 256.0);
        var result = new Mat(); Cv2.CvtColor(sketch, result, ColorConversionCodes.GRAY2BGR); return result;
    }

    private static Mat ApplyOil(Mat src)
    {
        Mat r = src.Clone();
        for (int i = 0; i < 8; i++) { var t = new Mat(); Cv2.BilateralFilter(r, t, 15, 150, 150); r.Dispose(); r = t; }
        return r;
    }

    private static Mat ApplyWatercolor(Mat src)
    {
        Mat color = src.Clone();
        for (int i = 0; i < 3; i++) { var t = new Mat(); Cv2.BilateralFilter(color, t, 9, 75, 75); color.Dispose(); color = t; }
        using var hsv = new Mat(); Cv2.CvtColor(color, hsv, ColorConversionCodes.BGR2HSV);
        Mat[] ch = Cv2.Split(hsv);
        var d = new Mat(); ch[1].ConvertTo(d, -1, 0.75, 0); ch[1].Dispose(); ch[1] = d;
        using var merged = new Mat(); Cv2.Merge(ch, merged);
        using var desat  = new Mat(); Cv2.CvtColor(merged, desat, ColorConversionCodes.HSV2BGR);
        foreach (var c in ch) c.Dispose();
        using var gray = new Mat(); using var edges = new Mat(); using var edgesBGR = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Canny(gray, edges, 30, 100);
        Cv2.CvtColor(edges, edgesBGR, ColorConversionCodes.GRAY2BGR);
        var result = new Mat(); Cv2.AddWeighted(desat, 0.92, edgesBGR, 0.08, 0, result);
        color.Dispose(); return result;
    }

    private static Mat AdjustBC(Mat src, double brightness, double contrast)
    {
        var dst = new Mat(); src.ConvertTo(dst, -1, contrast, brightness); return dst;
    }

    private static Mat DetectEdges(Mat src, double t1, double t2)
    {
        using var gray = new Mat(); using var blurred = new Mat(); using var edges = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        Cv2.Canny(blurred, edges, t1, t2);
        var result = new Mat(); Cv2.CvtColor(edges, result, ColorConversionCodes.GRAY2BGR); return result;
    }

    private static Mat RemoveRegion(Mat src, int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, src.Width  - 1); y = Math.Clamp(y, 0, src.Height - 1);
        w = Math.Clamp(w, 1, src.Width  - x); h = Math.Clamp(h, 1, src.Height - y);
        using var mask = new Mat(src.Size(), MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mask, new Rect(x, y, w, h), Scalar.White, -1);
        var result = new Mat(); Cv2.Inpaint(src, mask, result, 5, InpaintMethod.Telea); return result;
    }

}
