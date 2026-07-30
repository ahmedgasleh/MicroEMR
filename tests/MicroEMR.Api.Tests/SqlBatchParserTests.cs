using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SqlBatchParserTests
{
    [Fact]
    public void NormalBatchesAreReturnedInOrder()
    {
        var batches = SqlBatchParser.Parse("SELECT 1;\nGO\nSELECT 2;");

        Assert.Equal(2, batches.Count);
        Assert.Contains("SELECT 1", batches[0]);
        Assert.Contains("SELECT 2", batches[1]);
    }

    [Fact]
    public void GoInsideStringOrCommentIsNotSeparator()
    {
        var script = "SELECT 'first\nGO\nlast';\n-- GO\n/*\nGO\n*/\nSELECT 2;";

        var batches = SqlBatchParser.Parse(script);

        Assert.Single(batches);
    }

    [Fact]
    public void RepeatCountIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SqlBatchParser.Parse("SELECT 1;\nGO 2"));
    }
}
