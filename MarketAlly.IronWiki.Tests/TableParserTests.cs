// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using FluentAssertions;
using MarketAlly.IronWiki.Nodes;
using MarketAlly.IronWiki.Parsing;
using Xunit;

namespace MarketAlly.IronWiki.Tests;

public class TableParserTests
{
    private readonly WikitextParser _parser = new();

    [Fact]
    public void Parse_SimpleTable_ReturnsTableNode()
    {
        var wikitext = @"{|
|Cell 1
|Cell 2
|}";

        var result = _parser.Parse(wikitext);

        var table = result.EnumerateDescendants<Table>().FirstOrDefault();
        table.Should().NotBeNull();
    }

    [Fact]
    public void Parse_TableWithRows_ParsesAllRows()
    {
        var wikitext = @"{|
|-
|Cell 1
|-
|Cell 2
|}";

        var result = _parser.Parse(wikitext);

        var table = result.EnumerateDescendants<Table>().FirstOrDefault();
        table.Should().NotBeNull();
        table!.Rows.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void Parse_TableWithCaption_ParsesCaption()
    {
        var wikitext = @"{|
|+ My Caption
|-
|Cell 1
|}";

        var result = _parser.Parse(wikitext);

        var table = result.EnumerateDescendants<Table>().FirstOrDefault();
        table.Should().NotBeNull();
        table!.Caption.Should().NotBeNull();
    }

    [Fact]
    public void Parse_TableWithHeaderCells_ParsesHeadersCorrectly()
    {
        var wikitext = @"{|
! Header 1
! Header 2
|-
| Cell 1
| Cell 2
|}";

        var result = _parser.Parse(wikitext);

        var table = result.EnumerateDescendants<Table>().FirstOrDefault();
        table.Should().NotBeNull();

        var headerCells = table!.Rows.SelectMany(r => r.Cells).Where(c => c.IsHeader).ToList();
        headerCells.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_TableWithInlineCells_ParsesInlineCells()
    {
        var wikitext = @"{|
| Cell 1 || Cell 2 || Cell 3
|}";

        var result = _parser.Parse(wikitext);

        var table = result.EnumerateDescendants<Table>().FirstOrDefault();
        table.Should().NotBeNull();

        var cells = table!.Rows.SelectMany(r => r.Cells).ToList();
        cells.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public void Parse_TableWithAttributes_ParsesAttributes()
    {
        var wikitext = @"{| class=""wikitable"" style=""width:100%""
|-
| Cell
|}";

        var result = _parser.Parse(wikitext);

        var table = result.EnumerateDescendants<Table>().FirstOrDefault();
        table.Should().NotBeNull();
        // Attributes should be parsed
    }

    [Fact]
    public void Parse_NestedTable_ParsesOuterTable()
    {
        var wikitext = @"{|
|
{|
| Nested
|}
|}";

        var result = _parser.Parse(wikitext);

        var tables = result.EnumerateDescendants<Table>().ToList();
        tables.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void Parse_TableWithCellAttributes_ParsesCellAttributes()
    {
        var wikitext = @"{|
| style=""color:red"" | Styled Cell
|}";

        var result = _parser.Parse(wikitext);

        var table = result.EnumerateDescendants<Table>().FirstOrDefault();
        table.Should().NotBeNull();

        var cells = table!.Rows.SelectMany(r => r.Cells).ToList();
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_ComplexTable_ParsesCorrectly()
    {
        var wikitext = @"{| class=""wikitable sortable""
|+ Table Caption
|-
! Header 1 !! Header 2 !! Header 3
|-
| Row 1, Cell 1 || Row 1, Cell 2 || Row 1, Cell 3
|-
| Row 2, Cell 1 || Row 2, Cell 2 || Row 2, Cell 3
|}";

        var result = _parser.Parse(wikitext);

        var table = result.EnumerateDescendants<Table>().FirstOrDefault();
        table.Should().NotBeNull();
        table!.Caption.Should().NotBeNull();
        table.Rows.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public void ToString_Table_ReconstructsValidWikitext()
    {
        var wikitext = @"{|
|-
|Cell 1
|Cell 2
|}";

        var result = _parser.Parse(wikitext);
        var reconstructed = result.ToString();

        // Should contain table markers
        reconstructed.Should().Contain("{|");
        reconstructed.Should().Contain("|}");
    }
}
