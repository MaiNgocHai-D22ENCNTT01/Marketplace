using SkiaSharp;
using System.Text.Json;

namespace Marketplace.WebAPI;

internal static class VisualFeatures
{
    private const int Side = 24;
    public const int Dimension = Side * Side * 4;
    public const int MaxBytes = 5 * 1024 * 1024;

    public static double[] Extract(Stream stream)
    {
        using var input = new SKManagedStream(stream);
        using var codec = SKCodec.Create(input);
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new InvalidDataException("Cần ảnh JPG, PNG hoặc WEBP hợp lệ.");
        int width = codec.Info.Width, height = codec.Info.Height;
        if ((long)width * height > 12_000_000 || codec.FrameCount > 1)
            throw new InvalidDataException("Chọn ảnh tĩnh tối đa 12 megapixel.");
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("Dữ liệu ảnh không đầy đủ.");
        double[] result = new double[Dimension];
        double[] background = new double[3];
        for (int y = 0; y < Side; y++)
        for (int x = 0; x < Side; x++)
        {
            var pixel = bitmap.GetPixel((int)((x + .5) * width / Side), (int)((y + .5) * height / Side));
            double alpha = pixel.Alpha / 255d;
            double[] rgb = [pixel.Red * alpha + 255*(1-alpha), pixel.Green * alpha + 255*(1-alpha), pixel.Blue * alpha + 255*(1-alpha)];
            if (x == 0 && y == 0) background = rgb;
            double mask = Math.Min(1, rgb.Zip(background, (a,b) => Math.Abs(a-b)).Max() / 55);
            int offset = (y * Side + x) * 4;
            result[offset] = mask;
            for (int i = 0; i < 3; i++) result[offset+i+1] = mask * rgb[i] / 255;
        }
        double norm = Math.Sqrt(result.Sum(x => x*x));
        if (norm < 1e-8) throw new InvalidDataException("Ảnh không có đủ chi tiết.");
        return result.Select(x => x/norm).ToArray();
    }

    public static double[]? Read(string? json)
    {
        if (json is null) return null;
        try
        {
            var values = JsonSerializer.Deserialize<double[]>(json);
            return values?.Length == Dimension && values.All(double.IsFinite) ? values : null;
        }
        catch (JsonException) { return null; }
    }
}
