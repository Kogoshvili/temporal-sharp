using System.Text;

namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Helpers for encoding and decoding PEM certificate material. Certificate
/// content in configuration or secret stores may be raw PEM text or a base64
/// encoding of the PEM (or DER) bytes.
/// </summary>
public static class TlsContent
{
    /// <summary>
    /// Decodes inline certificate content into PEM bytes. Content that starts
    /// with <c>-----BEGIN</c> is treated as raw PEM text; anything else is
    /// treated as base64. Returns <c>null</c> for null or blank content.
    /// </summary>
    public static byte[]? Decode(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var trimmed = content.Trim();
        return trimmed.StartsWith("-----BEGIN", StringComparison.Ordinal)
            ? Encoding.UTF8.GetBytes(trimmed)
            : Convert.FromBase64String(trimmed);
    }

    /// <summary>
    /// Wraps DER bytes in a PEM envelope with the given label (e.g.
    /// <c>CERTIFICATE</c> or <c>PRIVATE KEY</c>).
    /// </summary>
    public static byte[] EncodePem(byte[] der, string label)
    {
        var base64 = Convert.ToBase64String(der);
        var builder = new StringBuilder();
        builder.AppendLine($"-----BEGIN {label}-----");
        for (var offset = 0; offset < base64.Length; offset += 64)
        {
            builder.AppendLine(base64.Substring(offset, Math.Min(64, base64.Length - offset)));
        }

        builder.AppendLine($"-----END {label}-----");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }
}
