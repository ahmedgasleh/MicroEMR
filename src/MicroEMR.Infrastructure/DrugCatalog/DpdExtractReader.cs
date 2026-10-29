using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using MicroEMR.Application.DrugCatalog;

namespace MicroEMR.Infrastructure.DrugCatalog;

// Official extract Read Me: positional UTF-8 CSV, quoted fields; no API DTO leaks into prescribing.
public sealed class DpdExtractReader : IDrugCatalogSourceReader
{
    public const string SourceSystem = "https://health-products.canada.ca/dpd-bdpp";
    private const int MaxArchive = 32 * 1024 * 1024;
    private const long MaxExpanded = 256L * 1024 * 1024;
    public async Task<DrugImportBatch> ReadAsync(Stream input, CancellationToken token = default)
    {
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920]; int read;
            while ((read = await input.ReadAsync(chunk, token)) > 0)
            {
                if (buffer.Length + read > MaxArchive) throw new ArgumentException("DPD archive exceeds 32 MB.");
                await buffer.WriteAsync(chunk.AsMemory(0, read), token);
            }
            var hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
            buffer.Position = 0;
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
            if (zip.Entries.Count > 100 || zip.Entries.Sum(x => x.Length) > MaxExpanded) throw new ArgumentException("DPD archive is too large.");
            long expandedRead = 0;
            var result = new List<DrugFacts>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var suffix in new[] { "", "_ap", "_ia", "_dr" })
            {
                if (!zip.Entries.Any(x => x.FullName == "drug" + suffix + ".txt")) continue;
                var products = Rows("drug", suffix, 15);
                var ingredients = Rows("ingred", suffix, 15).ToLookup(x => x[0], StringComparer.Ordinal);
                var forms = Rows("form", suffix, 4).ToLookup(x => x[0], StringComparer.Ordinal);
                var routes = Rows("route", suffix, 4).ToLookup(x => x[0], StringComparer.Ordinal);
                var statuses = Rows("status", suffix, 7).ToLookup(x => x[0], StringComparer.Ordinal);
                var productIds = products.Select(x => x[0]).ToHashSet(StringComparer.Ordinal);
                if (ingredients.Any(x => !productIds.Contains(x.Key)) || forms.Any(x => !productIds.Contains(x.Key)) ||
                    routes.Any(x => !productIds.Contains(x.Key)) || statuses.Any(x => !productIds.Contains(x.Key)))
                    throw new ArgumentException("DPD archive contains orphan component rows.");
                foreach (var p in products)
                {
                    token.ThrowIfCancellationRequested();
                    if (p[0].Any(c => c is < '0' or > '9') || !long.TryParse(p[0], out var code) || code <= 0 || p[0].Length > 8 || !seen.Add(p[0]))
                        throw new ArgumentException("Invalid or ambiguous DPD product code.");
                    var current = statuses[p[0]].Where(s => s[1] == "Y").ToArray();
                    if (current.Length != 1) throw new ArgumentException("A product must have one current DPD status.");
                    if (p[3].Length > 29 || p[4].Length > 200 || p[2].Length > 40 || current[0][2].Length > 40)
                        throw new ArgumentException("DPD product exceeds documented bounds.");
                    var ai = ingredients[p[0]].Select(x => new DrugIngredient(x[2], x[4], x[5], x[7], x[9], x[8])).ToArray();
                    if (int.TryParse(p[8], out var expectedIngredients) && expectedIngredients != ai.Length)
                        throw new ArgumentException("DPD ingredient count does not match product metadata.");
                    result.Add(new(SourceSystem, p[0], p[3], p[4], p[2], current[0][2], ai,
                        forms[p[0]].Select(x => x[2]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                        routes[p[0]].Select(x => x[2]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                        string.IsNullOrEmpty(p[9]) ? null : p[9], p[2] == "Human" && current[0][2] == "MARKETED"));
                    if (result.Count > 100000) throw new ArgumentException("Too many DPD products.");
                }
            }
            return new(result, hash);

            List<string[]> Rows(string prefix, string suffix, int columns)
            {
                var name = prefix + suffix + ".txt";
                var entries = zip.Entries.Where(x => x.FullName == name).ToArray();
                if (entries.Length != 1) throw new ArgumentException("Missing or duplicate DPD file: " + name);
                using var stream = entries[0].Open();
                using var capped = new CountedStream(stream, n => { expandedRead += n; if (expandedRead > MaxExpanded) throw new ArgumentException("Expanded DPD data exceeds 256 MB."); });
                using var reader = new StreamReader(capped, new UTF8Encoding(false, true), false);
                if (reader.Peek() == '\uFEFF') reader.Read();
                using var csv = new TextFieldParser(reader) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
                csv.SetDelimiters(",");
                var rows = new List<string[]>();
                while (!csv.EndOfData)
                {
                    token.ThrowIfCancellationRequested();
                    var row = csv.ReadFields()!;
                    if (row.Length != columns || row.Any(x => x.Length > 4000 || x.Any(char.IsControl) || !ValidUnicode(x)))
                        throw new ArgumentException("Malformed DPD record in " + name);
                    rows.Add(row);
                    if (rows.Count > 300000) throw new ArgumentException("Too many DPD component records.");
                }
                return rows;
            }
        }
        catch (Exception e) when (e is InvalidDataException or MalformedLineException or DecoderFallbackException)
        { throw new ArgumentException("Invalid DPD UTF-8 CSV archive.", e); }
    }
    private static bool ValidUnicode(string value)
    {
        for (var i = 0; i < value.Length; i++)
            if (char.IsSurrogate(value[i]) && (!char.IsHighSurrogate(value[i]) || ++i >= value.Length || !char.IsLowSurrogate(value[i]))) return false;
        return true;
    }
    private sealed class CountedStream(Stream input, Action<int> count) : Stream
    {
        public override int Read(byte[] buffer, int offset, int size) { var n = input.Read(buffer, offset, size); count(n); return n; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int size) => throw new NotSupportedException();
    }
}
