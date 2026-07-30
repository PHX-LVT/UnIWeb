using System.Buffers.Binary;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FullProject.Security;

public static class CustomIconUploadPolicy
{
    public const long MaximumRasterBytes = 2L * 1024 * 1024;
    public const long MaximumSvgBytes = 512L * 1024;
    public const int MaximumDimension = 2048;
    private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".webp", ".jpg", ".jpeg" };
    private static readonly HashSet<string> AllowedSvgElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "title", "desc", "defs", "linearGradient", "radialGradient", "stop", "clipPath", "mask", "use", "symbol"
    };
    private static readonly HashSet<string> AllowedSvgAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "viewBox", "width", "height", "x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry",
        "d", "points", "fill", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "fill-rule", "clip-rule",
        "opacity", "transform", "gradientUnits", "offset", "stop-color", "stop-opacity", "href", "preserveAspectRatio",
        "role", "aria-label", "focusable"
    };

    public static async Task<CustomIconUploadResult> ReadAndValidateAsync(
        Stream stream,
        string fileName,
        string? contentType,
        long length,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var isSvg = extension == ".svg" || string.Equals(contentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase);
        var maximum = isSvg ? MaximumSvgBytes : MaximumRasterBytes;
        if (length <= 0 || length > maximum)
            return CustomIconUploadResult.Invalid($"Custom Icons must be {maximum / 1024}KB or smaller.");
        if (!isSvg && !RasterExtensions.Contains(extension))
            return CustomIconUploadResult.Invalid("Custom Icons must be PNG, WebP, JPEG, or SVG files.");

        using var buffer = new MemoryStream((int)Math.Min(length, maximum));
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0 || bytes.LongLength > maximum)
            return CustomIconUploadResult.Invalid("Custom Icon file size is invalid.");

        if (isSvg)
            return SanitizeSvg(bytes, fileName);

        var canonicalType = extension switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        if (!HasRasterSignature(bytes, extension))
            return CustomIconUploadResult.Invalid("Custom Icon contents do not match the file extension.");
        if (!HasSafeDimensions(bytes, extension))
            return CustomIconUploadResult.Invalid($"Custom Icon dimensions must not exceed {MaximumDimension}×{MaximumDimension} pixels.");

        return CustomIconUploadResult.Valid(bytes, Path.GetFileName(fileName), canonicalType);
    }

    private static CustomIconUploadResult SanitizeSvg(byte[] bytes, string fileName)
    {
        try
        {
            using var input = new MemoryStream(bytes);
            using var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumSvgBytes
            });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (document.Root is null || !string.Equals(document.Root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
                return CustomIconUploadResult.Invalid("The uploaded SVG does not contain a valid SVG root element.");

            foreach (var element in document.Root.DescendantsAndSelf().ToList())
            {
                if (!AllowedSvgElements.Contains(element.Name.LocalName))
                {
                    element.Remove();
                    continue;
                }

                foreach (var attribute in element.Attributes().ToList())
                {
                    if (attribute.IsNamespaceDeclaration) continue;
                    var name = attribute.Name.LocalName;
                    var value = attribute.Value.Trim();
                    var unsafeReference = name.Equals("href", StringComparison.OrdinalIgnoreCase) &&
                                          !value.StartsWith('#');
                    if (!AllowedSvgAttributes.Contains(name) ||
                        name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                        unsafeReference ||
                        value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        value.Contains("url(", StringComparison.OrdinalIgnoreCase))
                    {
                        attribute.Remove();
                    }
                }
            }

            if (document.Root.Attribute("viewBox") is null &&
                (document.Root.Attribute("width") is null || document.Root.Attribute("height") is null))
            {
                return CustomIconUploadResult.Invalid("SVG Custom Icons require a viewBox or explicit width and height.");
            }
            if (!HasSafeSvgDimensions(document.Root))
                return CustomIconUploadResult.Invalid($"Custom Icon dimensions must not exceed {MaximumDimension}×{MaximumDimension} pixels.");

            using var output = new MemoryStream();
            using (var writer = XmlWriter.Create(output, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = true,
                Indent = false
            }))
            {
                document.Save(writer);
            }
            return CustomIconUploadResult.Valid(output.ToArray(), Path.ChangeExtension(Path.GetFileName(fileName), ".svg"), "image/svg+xml");
        }
        catch (XmlException)
        {
            return CustomIconUploadResult.Invalid("The uploaded SVG is malformed or unsafe.");
        }
    }

    private static bool HasRasterSignature(ReadOnlySpan<byte> bytes, string extension) => extension switch
    {
        ".png" => bytes.Length >= 24 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        ".webp" => bytes.Length >= 30 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        ".jpg" or ".jpeg" => bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9,
        _ => false
    };

    private static bool HasSafeDimensions(ReadOnlySpan<byte> bytes, string extension)
    {
        if (extension == ".png")
        {
            var width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
            return width is > 0 and <= MaximumDimension && height is > 0 and <= MaximumDimension;
        }
        if (extension is ".jpg" or ".jpeg")
            return TryReadJpegDimensions(bytes, out var width, out var height) && width <= MaximumDimension && height <= MaximumDimension;
        if (extension == ".webp")
            return TryReadWebpDimensions(bytes, out var width, out var height) &&
                   width is > 0 and <= MaximumDimension && height is > 0 and <= MaximumDimension;
        return false;
    }

    private static bool HasSafeSvgDimensions(XElement root)
    {
        var viewBox = root.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals("viewBox", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(viewBox))
        {
            var values = viewBox.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length != 4 ||
                !double.TryParse(values[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width) ||
                !double.TryParse(values[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height))
                return false;
            return width is > 0 and <= MaximumDimension && height is > 0 and <= MaximumDimension;
        }

        return TryParseSvgLength(root, "width", out var explicitWidth) &&
               TryParseSvgLength(root, "height", out var explicitHeight) &&
               explicitWidth is > 0 and <= MaximumDimension && explicitHeight is > 0 and <= MaximumDimension;
    }

    private static bool TryParseSvgLength(XElement root, string name, out double value)
    {
        value = 0;
        var raw = root.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (raw.EndsWith("px", StringComparison.OrdinalIgnoreCase)) raw = raw[..^2];
        return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadWebpDimensions(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 25 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return false;
        var chunk = bytes.Slice(12, 4);
        if (chunk.SequenceEqual("VP8X"u8) && bytes.Length >= 30)
        {
            width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
            height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
            return true;
        }
        if (chunk.SequenceEqual("VP8 "u8) && bytes.Length >= 30 && bytes.Slice(23, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }))
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(26, 2)) & 0x3fff;
            height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(28, 2)) & 0x3fff;
            return true;
        }
        if (chunk.SequenceEqual("VP8L"u8) && bytes.Length >= 25 && bytes[20] == 0x2f)
        {
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(21, 4));
            width = (int)(packed & 0x3fff) + 1;
            height = (int)((packed >> 14) & 0x3fff) + 1;
            return true;
        }
        return false;
    }

    private static bool TryReadJpegDimensions(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        var offset = 2;
        while (offset + 9 < bytes.Length)
        {
            if (bytes[offset] != 0xff) { offset++; continue; }
            var marker = bytes[offset + 1];
            offset += 2;
            if (marker is 0xd8 or 0xd9) continue;
            if (offset + 2 > bytes.Length) return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (length < 2 || offset + length > bytes.Length) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2));
                return width > 0 && height > 0;
            }
            offset += length;
        }
        return false;
    }
}

public sealed record CustomIconUploadResult(byte[]? Bytes, string FileName, string ContentType, string? Error)
{
    public bool Success => Bytes is { Length: > 0 } && string.IsNullOrWhiteSpace(Error);
    public static CustomIconUploadResult Valid(byte[] bytes, string fileName, string contentType) => new(bytes, fileName, contentType, null);
    public static CustomIconUploadResult Invalid(string error) => new(null, string.Empty, string.Empty, error);
}
