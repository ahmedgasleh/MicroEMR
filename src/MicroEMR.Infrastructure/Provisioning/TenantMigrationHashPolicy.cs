namespace MicroEMR.Infrastructure.Provisioning;

internal enum TenantMigrationHashMatch
{
    Mismatch,
    CanonicalV2,
    ApprovedLegacyV1
}

internal static class TenantMigrationHashPolicy
{
    // Migrations through 0059 predate CanonicalV2. Only the explicitly registered
    // 0000-0017 CRLF ledger representations receive LegacyV1 compatibility.
    internal const string CanonicalV2OnlyAfterMigrationNumber = "0059";

    private const string LegacyReason = "LegacyV1 CRLF checkout representation confirmed by Step 41P";

    private static readonly IReadOnlyDictionary<string, CompatibilityEntry> CompatibilityRegistry =
        new Dictionary<string, CompatibilityEntry>(StringComparer.Ordinal)
        {
            ["0000-tenant-metadata"] = Entry("DBA7AD270231D00CB504F2F8030703F20C06DBE062149180B6F86FC12579CBE8", "59CE21585DC2174A990709A767127B2AEE3D8EA332E7F202CE3389ACF483A7EA"),
            ["0001-initial-schema"] = Entry("F6474F5DFCCEB9AAC6A6DA1E2BCF9D3381BF4776A7E4C3C144AC3759D76E8080", "E846F1888FF4170F00DAB085C50FD6B8E1A73401C4E8B4F42A4D9E568C952B9F"),
            ["0002-patients"] = Entry("F67568B3FFF5E2979B9A8D8EC4FDE33E840EC9DE78D7DE83F03B0051A8FAC279", "04EFBCC07C788415474AD876AEC54D9E6DE683380BB3082CE20E1827FE98DC34"),
            ["0003-documents"] = Entry("06B4A48F3C4FE0D09A826BBBB8335763B81865E139F45FEBB23311779EF20A44", "F4E5737703E8B61F3DAAF939F102015541A12DFC8C932C5EB194CD890D53C380"),
            ["0004-encounters"] = Entry("6624140D78A08BEB3EC96E972B102DA2F7DCC67E05FCC6DA192D526E961B5EF3", "A445B9B61C5F4133EB50BA28D7A2DD845D12474EBAF86A4E9E212FAAF4AD8D12"),
            ["0005-allergies"] = Entry("7515C30D4633EB486A4F0DFBA55E8390E434CBAE87B8145F7E655C0E75BE6FB2", "7B56A42AA944D3CED21D6B304C695B60DEA381C1F5D66E566D01776A6A31FEA9"),
            ["0006-medications"] = Entry("33DA2D98D69B8AAB05B16140802B65F975775C95B7D533566BE1646F70D41169", "D2FB52D1CCCC523969E253A6BF5D610766D3F375BBF9A3D8D0EFA0FBB307FDFC"),
            ["0007-problems"] = Entry("58F2926A2DA167AD972B079814F82310C8C3718D17268902D9BA75DC535AFE82", "4449235B4712B4906BCEF47009FF9BCCA07482B5A39C113CCEDB9648AE5217CC"),
            ["0008-vitals"] = Entry("FBE73C0136CFEF1371BF62826975164EA6897E26ED20A1FC11BE236C50D30F0B", "5574F3454FFBEAA2FEC36B372A71F6FF9C246F8FE29DF2A41258E533A6956827"),
            ["0009-chart-alerts"] = Entry("1D866E532804422348A3B2A9194A903A472F52C7F9F2123757B8C4629C2B68C5", "918D54B42220AE7EC62C507A5E9E0BD172B986C2C4B08C126101A161225BBBB8"),
            ["0010-results"] = Entry("563A238BB8FBCF439652649208AC6408BEFD2A2767EB9E5367727DC615DF2CAC", "078AB07E3237337F7D56B5033EBF20A0ADABF4352467CCB85382C8A77BB58902"),
            ["0011-tasks"] = Entry("79A9A2E1BF243C8D12D842B2DD3B8D9A6167F23633B26D8820AA543B461F91E1", "C37BF0203ED1F1B72AAFA1D1925442875617024B0850F3B7491454770C598036"),
            ["0012-encounter-soap-templates"] = Entry("1D394A253847102983C68BCADDE4BEBF9DAEB155554FAE417848EBAAF6397371", "C655C44AF93EB30BA42706EB2558DC12A7CB9A0195A9172526030DD593EABE01"),
            ["0013-scheduling"] = Entry("F73C099C6EE1873AB90459D7FC472D4C62932A41B233B4FF8CBF34B47AF2027D", "30C0018A1F9E03CE55C5F842A08D7F29848030C840658C9D741776557B2AEF7D"),
            ["0014-scheduling-mark-arrived"] = Entry("C588F703BAF9B5BCC15CDCB4464050F34B96ACBB08C88E77DC38DB38C28F7C09", "F41B623770F56B4B065A95E549441EFEF419C6CD2CCCE9B0CF58BE9FBFC96AB4"),
            ["0015-start-encounter-status"] = Entry("D943721A844B88CD3A449217E655470695A3088B9C6893C4A5BAF22568D5B3F1", "A9AA339C1A0D8A333B8CE0299DBF1A8CD8BC603ED2DA71EC3630662B98A1E810"),
            ["0016-complete-appointment-after-sign"] = Entry("BF006625A0C02CEDEA9A2E79EB4B163117E517532BEE4F34B699BB7E2EF3F616", "7329B6E8B10EE89F5374EA259DEC17FA030B0B37202621B538A17A540F8AA871"),
            ["0017-document-template-versioning"] = Entry("57E8A24DA9A0BF4B212EB0F50D40DF62023F5FC6EB2AD40D5B1066B578C1B9A1", "D793B232DDF1E21C114CA61975E330DD581EA7A1F5BEDDBF4F386D732BC2C514")
        };

    internal static int RegisteredLegacyMigrationCount => CompatibilityRegistry.Count;

    internal static TenantMigrationHashMatch Validate(
        TenantDatabaseMigration migration,
        string appliedHash)
    {
        ArgumentNullException.ThrowIfNull(migration);
        ArgumentException.ThrowIfNullOrWhiteSpace(appliedHash);

        if (string.Equals(migration.ScriptHash, appliedHash, StringComparison.OrdinalIgnoreCase))
            return TenantMigrationHashMatch.CanonicalV2;

        if (!CompatibilityRegistry.TryGetValue(migration.MigrationId, out var compatibility))
            return TenantMigrationHashMatch.Mismatch;

        // This source identity check must precede all LegacyV1 acceptance.
        if (!string.Equals(migration.ScriptHash, compatibility.PinnedCanonicalV2Hash,
                StringComparison.OrdinalIgnoreCase))
            return TenantMigrationHashMatch.Mismatch;

        return compatibility.AcceptedLegacyV1Hashes.Contains(appliedHash)
            ? TenantMigrationHashMatch.ApprovedLegacyV1
            : TenantMigrationHashMatch.Mismatch;
    }

    internal static string? GetCompatibilityReason(string migrationId) =>
        CompatibilityRegistry.TryGetValue(migrationId, out var entry) ? entry.Reason : null;

    private static CompatibilityEntry Entry(string canonicalV2, string legacyV1) =>
        new(canonicalV2, new HashSet<string>([legacyV1], StringComparer.OrdinalIgnoreCase), LegacyReason);

    private sealed record CompatibilityEntry(
        string PinnedCanonicalV2Hash,
        IReadOnlySet<string> AcceptedLegacyV1Hashes,
        string Reason);
}
