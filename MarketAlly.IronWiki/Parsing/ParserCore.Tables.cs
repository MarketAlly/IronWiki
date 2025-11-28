// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Parsing;

internal sealed partial class ParserCore
{
    /// <summary>
    /// Parses a wiki table {| ... |}.
    /// </summary>
    private Table? ParseTable()
    {
        if (!IsAtLineStart)
        {
            return null;
        }

        BeginContext();

        if (Consume(@"\{\|") is null)
        {
            return Reject<Table>();
        }

        var table = new Table();

        // Parse table attributes
        ParseTableAttributes(table.Attributes, out var attrWhitespace);
        table.AttributeTrailingWhitespace = attrWhitespace;

        // Consume newline after attributes
        Consume(@"\n");

        // Parse table content (caption, rows)
        ParseTableContent(table);

        // Expect closing |}
        if (Consume(@"\|\}") is null)
        {
            return Reject<Table>();
        }

        // Consume any trailing whitespace after |}
        Consume(@"[ \t]*");

        return Accept(table);
    }

    private void ParseTableContent(Table table)
    {
        TableRow? currentRow = null;
        var implicitRow = false;

        while (!NeedsTerminate() && LookAhead(@"\|\}") is null)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            // Check for caption |+
            if (IsAtLineStart && LookAhead(@"\|\+") is not null && table.Caption is null)
            {
                var caption = ParseTableCaption();
                if (caption is not null)
                {
                    table.Caption = caption;
                    continue;
                }
            }

            // Check for row marker |-
            if (IsAtLineStart && LookAhead(@"\|-") is not null)
            {
                var row = ParseTableRow();
                if (row is not null)
                {
                    table.Rows.Add(row);
                    currentRow = row;
                    implicitRow = false;
                    continue;
                }
            }

            // Check for cells | or ! (but not {| which starts nested table)
            if (IsAtLineStart && LookAhead(@"[\|!](?!\}|\+|-|\{)") is not null)
            {
                // Create implicit row if needed
                if (currentRow is null || !implicitRow)
                {
                    currentRow = new TableRow { HasExplicitRowMarker = false };
                    table.Rows.Add(currentRow);
                    implicitRow = true;
                }

                var cells = ParseTableCells();
                foreach (var cell in cells)
                {
                    currentRow.Cells.Add(cell);
                }
                continue;
            }

            // Check for nested table {|
            if (IsAtLineStart && LookAhead(@"\{\|") is not null)
            {
                var nestedTable = ParseTable();
                if (nestedTable is not null)
                {
                    // Add nested table to current cell if we have one
                    if (currentRow is not null && currentRow.Cells.Count > 0)
                    {
                        var lastCell = currentRow.Cells[currentRow.Cells.Count - 1];
                        // Create a nested table inline placeholder
                        if (lastCell.Content is not null)
                        {
                            lastCell.Content.Inlines.Add(new PlainText("\n"));
                        }
                        lastCell.NestedContent ??= new WikiNodeCollection<WikiNode>(lastCell);
                        lastCell.NestedContent.Add(nestedTable);
                    }
                    continue;
                }
            }

            // Skip unrecognized content
            if (Consume(@"[^\n]*\n?") is null)
            {
                break;
            }
        }
    }

    private TableCaption? ParseTableCaption()
    {
        BeginContext();

        if (Consume(@"\|\+") is null)
        {
            return Reject<TableCaption>();
        }

        var caption = new TableCaption();

        // Check for attributes (attr|content format)
        BeginContext(@"\||\n", true);

        var hasAttrPipe = false;
        var attrContent = new Run();

        if (ParseRun(RunParsingMode.Run, attrContent, false))
        {
            if (Consume(@"\|(?!\|)") is not null)
            {
                // This was attributes, parse actual content
                hasAttrPipe = true;
                // TODO: Parse attributes from attrContent
                Accept();
            }
            else
            {
                // No pipe - this is the content
                Rollback();
            }
        }
        else
        {
            Accept();
        }

        caption.HasAttributePipe = hasAttrPipe;

        // Parse caption content
        BeginContext(@"\n", false);
        var content = new Run();
        ParseRun(RunParsingMode.Run, content, true);
        caption.Content = content;
        Accept();

        // Consume trailing newline
        Consume(@"\n");

        return Accept(caption);
    }

    private TableRow? ParseTableRow()
    {
        BeginContext();

        if (Consume(@"\|-") is null)
        {
            return Reject<TableRow>();
        }

        var row = new TableRow { HasExplicitRowMarker = true };

        // Parse row attributes
        ParseTableAttributes(row.Attributes, out var attrWhitespace);
        row.AttributeTrailingWhitespace = attrWhitespace;

        // Consume newline
        Consume(@"\n");

        // Parse cells
        while (!NeedsTerminate() && LookAhead(@"\|\}|\|-|\|\+") is null)
        {
            if (IsAtLineStart && LookAhead(@"[\|!](?!\}|\+|-)") is not null)
            {
                var cells = ParseTableCells();
                foreach (var cell in cells)
                {
                    row.Cells.Add(cell);
                }
            }
            else
            {
                break;
            }
        }

        return Accept(row);
    }

    private List<TableCell> ParseTableCells()
    {
        var cells = new List<TableCell>();

        // Determine cell type
        var isHeader = LookAhead(@"!") is not null;
        var marker = isHeader ? @"!" : @"\|";
        var doubleMarker = isHeader ? @"!!" : @"\|\|";

        BeginContext();

        // Consume initial marker
        if (Consume(marker) is null)
        {
            Rollback();
            return cells;
        }

        var isFirst = true;

        while (true)
        {
            var cell = ParseSingleCell(isHeader, isFirst);
            if (cell is not null)
            {
                cell.IsInlineSibling = !isFirst;
                cells.Add(cell);
            }

            isFirst = false;

            // Check for inline sibling cells || or !!
            if (Consume(doubleMarker) is null)
            {
                break;
            }
        }

        // Consume trailing newline
        Consume(@"\n");

        Accept();
        return cells;
    }

    private TableCell? ParseSingleCell(bool isHeader, bool isFirstOnLine)
    {
        BeginContext();

        var cell = new TableCell { IsHeader = isHeader };

        // Check for attributes (attr|content format)
        BeginContext(@"\|(?!\|)|\n|!!", true);

        var attrContent = new Run();
        var hasContent = ParseRun(RunParsingMode.Run, attrContent, false);

        if (hasContent && Consume(@"\|(?!\|)") is not null)
        {
            // This was attributes
            cell.HasAttributePipe = true;
            // TODO: Parse attributes from attrContent
            Accept();
        }
        else
        {
            // No attribute pipe - rollback and parse as content
            Rollback();
        }

        // Parse cell content
        var doubleMarker = isHeader ? @"!!" : @"\|\|";
        BeginContext(doubleMarker + @"|\n", false);

        var content = new Run();
        ParseRun(RunParsingMode.Run, content, true);
        cell.Content = content;

        Accept();
        return Accept(cell);
    }

    private void ParseTableAttributes(WikiNodeCollection<TagAttributeNode> attributes, out string? trailingWhitespace)
    {
        trailingWhitespace = null;
        var ws = Consume(@"[ \t]+");

        while (LookAhead(@"\n|\|\}") is null)
        {
            if (ws is null)
            {
                break;
            }

            BeginContext();
            var attrName = ParseSimpleAttributeName();
            if (attrName is null)
            {
                Rollback();
                break;
            }

            var attr = new TagAttributeNode
            {
                Name = new Run(new PlainText(attrName)),
                LeadingWhitespace = ws
            };

            ws = Consume(@"[ \t]*");

            if (Consume("=") is not null)
            {
                attr.WhitespaceBeforeEquals = ws;
                attr.WhitespaceAfterEquals = Consume(@"[ \t]*");

                // Parse attribute value
                var value = ParseSimpleAttributeValue();
                if (value is not null)
                {
                    attr.Value = new WikitextDocument();
                    var para = new Paragraph();
                    para.Inlines.Add(new PlainText(value.Value.Value));
                    attr.Value.Lines.Add(para);
                    attr.Quote = value.Value.Quote;
                }

                ws = Consume(@"[ \t]+");
            }
            else
            {
                ws = null;
            }

            Accept(attr);
            attributes.Add(attr);
        }

        trailingWhitespace = ws;
    }

    private string? ParseSimpleAttributeName()
    {
        return Consume(@"[\w\-]+");
    }

    private (string Value, ValueQuoteStyle Quote)? ParseSimpleAttributeValue()
    {
        // Try double quotes
        if (Consume("\"") is not null)
        {
            var value = Consume(@"[^""]*");
            if (Consume("\"") is not null)
            {
                return (value ?? string.Empty, ValueQuoteStyle.DoubleQuotes);
            }
        }

        // Try single quotes
        if (Consume("'") is not null)
        {
            var value = Consume(@"[^']*");
            if (Consume("'") is not null)
            {
                return (value ?? string.Empty, ValueQuoteStyle.SingleQuotes);
            }
        }

        // Unquoted value
        var unquoted = Consume(@"[^\s\|\n]+");
        if (unquoted is not null)
        {
            return (unquoted, ValueQuoteStyle.None);
        }

        return null;
    }
}
