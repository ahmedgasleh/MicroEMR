using System.Security.Cryptography;
using System.Text;

namespace MicroEMR.Infrastructure.Provisioning;

public enum MigrationHashVersion
{
    LegacyV1 = 1,
    CanonicalV2 = 2
}

public static class MigrationSourceHashing
{
    public static string ComputeHash(string source, MigrationHashVersion version)
    {
        ArgumentNullException.ThrowIfNull(source);
        var decoded = RemoveLeadingBom(source);
        var hashInput = version switch
        {
            MigrationHashVersion.LegacyV1 => decoded,
            MigrationHashVersion.CanonicalV2 => NormalizeLineEndings(decoded),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unsupported migration hash version.")
        };

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)));
    }

    public static string ComputeCanonicalV2(string source) =>
        ComputeHash(source, MigrationHashVersion.CanonicalV2);

    internal static string NormalizeLineEndings(string source) =>
        source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

    private static string RemoveLeadingBom(string source) =>
        source.Length > 0 && source[0] == '\uFEFF' ? source[1..] : source;
}
