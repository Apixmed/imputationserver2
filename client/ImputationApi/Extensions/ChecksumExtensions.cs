using System.Security.Cryptography;
using System.Text;

namespace ImputationApi.Extensions
{
    public static class ChecksumExtensions
    {
        public static string? NormalizeSha256Hex(string? checksum)
        {
            if (string.IsNullOrWhiteSpace(checksum))
            {
                return null;
            }

            string trimmed = checksum.Trim();
            if (trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["sha256:".Length..];
            }

            trimmed = trimmed.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToLowerInvariant();
        }

        public static string ComputeSha256Hex(string input)
        {
            ArgumentNullException.ThrowIfNull(input);

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(bytes);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static string ComputeFileSha256Hex(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found.", filePath);
            }

            using FileStream stream = File.OpenRead(filePath);
            byte[] hashBytes = SHA256.HashData(stream);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
