namespace ITCafe.Api.Helpers;

public static class FileSignatureValidator
{
    private static readonly Dictionary<string, List<byte[]>> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new() { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new() { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".png"] = new() { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".gif"] = new() { new byte[] { 0x47, 0x49, 0x46, 0x38 } },
        [".webp"] = new() { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // RIFF....WEBP checked below
        [".pdf"] = new() { new byte[] { 0x25, 0x50, 0x44, 0x46 } },
        [".docx"] = new() { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".xlsx"] = new() { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".zip"] = new() { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".mp4"] = new() { new byte[] { 0x00, 0x00, 0x00 } }, // loose check; ftyp appears within first bytes
    };

    public static async Task<bool> IsValidAsync(Stream stream, string extension, CancellationToken ct = default)
    {
        if (stream == null || !stream.CanRead)
            return false;

        if (!Signatures.TryGetValue(extension, out var signatures))
            return true; // no signature check for unknown extensions (e.g., .txt, .csv, .json)

        var maxLength = signatures.Max(s => s.Length);
        var buffer = new byte[maxLength];
        var read = await stream.ReadAsync(buffer.AsMemory(0, maxLength), ct);
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            if (read < 12) return false;
            // RIFF....WEBP
            var isRiff = buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46;
            var isWebp = buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50;
            return isRiff && isWebp;
        }

        if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            if (read < 12) return false;
            // Look for 'ftyp' at offset 4-7
            for (int i = 4; i <= read - 4; i++)
            {
                if (buffer[i] == 0x66 && buffer[i + 1] == 0x74 && buffer[i + 2] == 0x79 && buffer[i + 3] == 0x70)
                    return true;
            }
            return false;
        }

        foreach (var sig in signatures)
        {
            if (read < sig.Length) continue;
            if (buffer.AsSpan(0, sig.Length).SequenceEqual(sig))
                return true;
        }

        return false;
    }
}
