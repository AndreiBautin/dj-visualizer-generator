using System.Buffers.Binary;

namespace DjVisualizer.Infrastructure.Uploads;

/// <summary>
/// Reads an image's pixel dimensions from its header without decoding it. A 10 MB cap on the
/// file says nothing about its decoded size: a single-colour 30000x30000 PNG compresses to a few
/// megabytes and decodes to about 3.6 GB, which on a 512 MB instance kills the process and every
/// render with it.
/// </summary>
internal static class ImageDimensions
{
    public const int MaxSide = 8192;

    public static (int Width, int Height)? TryRead(Stream stream, string extension) =>
        extension is ".png" ? TryReadPng(stream) : TryReadJpeg(stream);

    // IHDR is required to be the first chunk: 8-byte signature, 4-byte length, "IHDR", then
    // big-endian width and height.
    private static (int, int)? TryReadPng(Stream stream)
    {
        Span<byte> header = stackalloc byte[24];
        if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length
            || header[12] != 'I' || header[13] != 'H' || header[14] != 'D' || header[15] != 'R')
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..]);
        return width > int.MaxValue || height > int.MaxValue ? null : ((int)width, (int)height);
    }

    // Walks the marker segments to the first start-of-frame (SOF0-SOF15 except DHT, JPG and DAC),
    // whose payload carries precision, height, then width.
    private static (int, int)? TryReadJpeg(Stream stream)
    {
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
        {
            return null;
        }

        Span<byte> segment = stackalloc byte[7];
        while (true)
        {
            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xFF);

            if (marker < 0)
            {
                return null;
            }

            // Standalone markers carry no length.
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                continue;
            }

            if (marker is 0xD9 or 0xDA)
            {
                return null; // end of image, or scan data, before any frame header
            }

            if (stream.ReadAtLeast(segment[..2], 2, throwOnEndOfStream: false) < 2)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(segment);
            if (length < 2)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (stream.ReadAtLeast(segment[2..7], 5, throwOnEndOfStream: false) < 5)
                {
                    return null;
                }

                return (BinaryPrimitives.ReadUInt16BigEndian(segment[5..]), BinaryPrimitives.ReadUInt16BigEndian(segment[3..]));
            }

            stream.Seek(length - 2, SeekOrigin.Current);
        }
    }
}
