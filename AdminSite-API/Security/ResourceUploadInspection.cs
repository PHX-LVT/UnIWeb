namespace FullProject.Security;

public static class ResourceUploadInspection
{
    public const long MaximumImagePixels = 100_000_000;

    public static bool HasSafeImageDimensions(byte[] sample, string fileName, out string? error)
    {
        error = null;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!TryReadImageDimensions(sample, extension, out var width, out var height))
        {
            error = "Image dimensions could not be verified from the uploaded file.";
            return false;
        }

        if (width <= 0 || height <= 0 || (long)width * height > MaximumImagePixels)
        {
            error = "Image dimensions are invalid or exceed the 100 megapixel safety limit.";
            return false;
        }

        return true;
    }

    private static bool TryReadImageDimensions(byte[] bytes, string extension, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (extension == ".png" && bytes.Length >= 24)
        {
            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            return true;
        }

        if (extension == ".gif" && bytes.Length >= 10)
        {
            width = bytes[6] | bytes[7] << 8;
            height = bytes[8] | bytes[9] << 8;
            return true;
        }

        if (extension == ".webp" && bytes.Length >= 30)
        {
            if (bytes.AsSpan(12, 4).SequenceEqual("VP8X"u8))
            {
                width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
                height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
                return true;
            }

            if (bytes.AsSpan(12, 4).SequenceEqual("VP8 "u8) &&
                bytes[23] == 0x9d && bytes[24] == 0x01 && bytes[25] == 0x2a)
            {
                width = (bytes[26] | bytes[27] << 8) & 0x3fff;
                height = (bytes[28] | bytes[29] << 8) & 0x3fff;
                return true;
            }

            if (bytes.AsSpan(12, 4).SequenceEqual("VP8L"u8) && bytes[20] == 0x2f)
            {
                width = 1 + bytes[21] + ((bytes[22] & 0x3f) << 8);
                height = 1 + (bytes[22] >> 6) + (bytes[23] << 2) + ((bytes[24] & 0x0f) << 10);
                return true;
            }
        }

        if (extension is ".jpg" or ".jpeg")
            return TryReadJpegDimensions(bytes, out width, out height);

        return false;
    }

    private static bool TryReadJpegDimensions(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        var offset = 2;
        while (offset + 8 < bytes.Length)
        {
            if (bytes[offset] != 0xFF) { offset++; continue; }
            var marker = bytes[offset + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                height = bytes[offset + 5] << 8 | bytes[offset + 6];
                width = bytes[offset + 7] << 8 | bytes[offset + 8];
                return true;
            }

            if (offset + 3 >= bytes.Length) break;
            var length = bytes[offset + 2] << 8 | bytes[offset + 3];
            if (length < 2) break;
            offset += length + 2;
        }
        return false;
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
}
