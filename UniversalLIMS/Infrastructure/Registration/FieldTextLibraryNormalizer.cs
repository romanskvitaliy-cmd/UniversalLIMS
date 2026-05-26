using System.Security.Cryptography;
using System.Text;

namespace UniversalLIMS.Infrastructure.Registration;

internal static class FieldTextLibraryNormalizer
{
    public const int MaxBodyLength = 4000;

    public const int MaxShortLabelLength = 200;

    public const int MaxEntriesPerKey = 200;

    public static string NormalizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            body.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        return tag.Trim().ToUpperInvariant();
    }

    public static string ComputeBodyHash(string normalizedBody)
    {
        var bytes = Encoding.UTF8.GetBytes(normalizedBody);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string BuildDefaultShortLabel(string normalizedBody)
    {
        if (normalizedBody.Length <= 60)
        {
            return normalizedBody;
        }

        return $"{normalizedBody[..57]}…";
    }
}
