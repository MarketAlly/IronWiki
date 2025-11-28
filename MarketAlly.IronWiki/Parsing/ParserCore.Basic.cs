// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Parsing;

internal sealed partial class ParserCore
{
    /// <summary>
    /// Sentinel node indicating successful parsing but no node to add.
    /// </summary>
    private static readonly BlockNode EmptyLineNode = new Paragraph();

    /// <summary>
    /// Parses a complete wikitext document.
    /// </summary>
    private WikitextDocument ParseWikitext()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        BeginContext();

        var doc = new WikitextDocument();
        BlockNode? lastLine = null;

        if (NeedsTerminate())
        {
            return Accept(doc);
        }

        while (true)
        {
            var line = ParseLine(lastLine);
            if (line is not null && line != EmptyLineNode)
            {
                lastLine = line;
                doc.Lines.Add(line);
            }

            var extraPara = ParseLineEnd(lastLine);
            if (extraPara is null)
            {
                if (NeedsTerminate())
                {
                    // Hit a terminator - normal exit
                    break;
                }

                // Parser is stuck - self-heal by consuming one character as plain text
                // This prevents infinite loops and allows parsing to continue
                AddDiagnostic(DiagnosticSeverity.Warning, "Parser recovery: consumed unparseable character as plain text");
                var recoveredChar = ConsumeRecoveryChar();
                if (recoveredChar is not null)
                {
                    // Add to last paragraph or create new one
                    if (lastLine is Paragraph para)
                    {
                        AppendToParagraph(para, recoveredChar, _line, _column - 1, _line, _column);
                    }
                    else
                    {
                        var newPara = new Paragraph { Compact = true };
                        newPara.Inlines.Add(new PlainText(recoveredChar));
                        doc.Lines.Add(newPara);
                        lastLine = newPara;
                    }
                    continue;
                }

                // Can't recover - should never happen if there's text remaining
                break;
            }

            if (extraPara != EmptyLineNode)
            {
                doc.Lines.Add(extraPara);
                lastLine = extraPara;
            }

            if (NeedsTerminate())
            {
                break;
            }
        }

        return Accept(doc);
    }

    /// <summary>
    /// Parses a single line.
    /// </summary>
    private BlockNode? ParseLine(BlockNode? lastLine)
    {
        BeginContext(@"\n", false);

        var node = ParseTable()
                ?? ParseListItem()
                ?? ParseHeading()
                ?? ParseCompactParagraph(lastLine);

        // Clean up trailing empty PlainText nodes
        if (lastLine is IInlineContainer container &&
            container.Inlines.LastNode is PlainText { Content.Length: 0 } emptyText)
        {
            emptyText.Remove();
        }

        if (node is not null)
        {
            Accept();
            return node;
        }

        Rollback();
        return null;
    }

    /// <summary>
    /// Parses line ending and manages paragraph state.
    /// </summary>
    private BlockNode? ParseLineEnd(BlockNode? lastNode)
    {
        var unclosedParagraph = lastNode as Paragraph;
        if (unclosedParagraph is not null && !unclosedParagraph.Compact)
        {
            unclosedParagraph = null;
        }

        var lastColumn = _column;
        if (Consume(@"\n") is null)
        {
            return null;
        }

        BeginContext();

        // Whitespace between newlines
        var trailingWs = Consume(@"[\f\r\t\v\x85\p{Z}]+");

        if (unclosedParagraph is not null)
        {
            var trailingWsEndCol = _column;

            // Try to consume second newline to close paragraph
            if (Consume(@"\n") is not null)
            {
                // Mark paragraph as closed (non-compact) so next line creates new paragraph
                unclosedParagraph.Compact = false;

                // Append the newline and whitespace
                AppendToParagraph(unclosedParagraph, "\n" + (trailingWs ?? string.Empty),
                    _line - 1, lastColumn, _line, trailingWsEndCol);

                // Check for special case: \n\nTERM
                if (NeedsTerminate(GetTerminator(@"\n")))
                {
                    var extraPara = new Paragraph();
                    if (_options.TrackSourceSpans)
                    {
                        extraPara.SetSourceSpan(_line, _column, _line, _column);
                    }
                    Accept();
                    return extraPara;
                }

                Accept();
                return EmptyLineNode;
            }

            // Only one \n - check if we hit a terminator
            if (NeedsTerminate())
            {
                AppendToParagraph(unclosedParagraph, "\n" + trailingWs,
                    _line - 1, lastColumn, _line, _column);
                Accept();
                return EmptyLineNode;
            }

            // Paragraph continues - add empty placeholder
            AppendToParagraph(unclosedParagraph, "",
                _line - 1, lastColumn, _line - 1, lastColumn);
            Rollback();
            return EmptyLineNode;
        }

        // Last node is not an unclosed paragraph (LIST_ITEM, HEADING, etc.)
        if (NeedsTerminate(GetTerminator(@"\n")))
        {
            var extraPara = new Paragraph();
            if (trailingWs is not null)
            {
                var pt = new PlainText(trailingWs);
                if (_options.TrackSourceSpans)
                {
                    var ctx = _contextStack.Peek();
                    pt.SetSourceSpan(ctx.StartLine, ctx.StartColumn, _line, _column);
                }
                extraPara.Inlines.Add(pt);
            }
            return Accept(extraPara);
        }

        Rollback();
        return EmptyLineNode;
    }

    private static void AppendToParagraph(Paragraph para, string content, int startLine, int startCol, int endLine, int endCol)
    {
        if (para.Inlines.LastNode is PlainText lastText)
        {
            lastText.Content += content;
            lastText.ExtendSourceSpan(endLine, endCol);
        }
        else if (content.Length > 0)
        {
            var text = new PlainText(content);
            text.SetSourceSpan(startLine, startCol, endLine, endCol);
            para.Inlines.Add(text);
        }
        para.ExtendSourceSpan(endLine, endCol);
    }

    /// <summary>
    /// Parses a list item.
    /// </summary>
    private ListItem? ParseListItem()
    {
        if (!IsAtLineStart)
        {
            return null;
        }

        BeginContext();

        var prefix = Consume(@"[*#:;]+|-{4,}| ");
        if (prefix is null)
        {
            return Reject<ListItem>();
        }

        var node = new ListItem { Prefix = prefix };
        ParseRun(RunParsingMode.Run, node, false);

        return Accept(node);
    }

    /// <summary>
    /// Parses a heading.
    /// </summary>
    private Heading? ParseHeading()
    {
        var prefix = LookAhead(@"={1,6}");
        if (prefix is null)
        {
            return null;
        }

        // Try each level from highest to lowest
        for (var level = prefix.Length; level > 0; level--)
        {
            var result = TryParseHeadingAtLevel(level);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private Heading? TryParseHeadingAtLevel(int level)
    {
        var equalsPattern = $"={{{level}}}";
        var terminatorPattern = equalsPattern + "(?!=)";

        BeginContext(terminatorPattern, false);

        if (Consume(equalsPattern) is null)
        {
            return Reject<Heading>();
        }

        var heading = new Heading { Level = level };
        var segments = new List<IInlineContainer>();

        while (true)
        {
            BeginContext();
            var segment = new Run();

            var hasContent = ParseRun(RunParsingMode.Run, segment, true);
            if (!hasContent && LookAhead(terminatorPattern) is null)
            {
                Rollback();
                break;
            }

            if (Consume(equalsPattern) is null)
            {
                // This segment is the suffix
                heading.Suffix = segment;
                Accept();
                break;
            }

            segments.Add(segment);
            Accept();
        }

        // Validate suffix contains only whitespace
        if (heading.Suffix is not null &&
            heading.Suffix.Inlines.OfType<PlainText>().Any(pt => !string.IsNullOrWhiteSpace(pt.Content)))
        {
            // Segment contexts were already accepted, so we just reject the heading context
            return Reject<Heading>();
        }

        if (segments.Count == 0)
        {
            return Reject<Heading>();
        }

        // Concatenate segments
        for (var i = 0; i < segments.Count; i++)
        {
            heading.Inlines.AddFrom(segments[i].Inlines);
            if (i < segments.Count - 1)
            {
                heading.Inlines.Add(new PlainText(new string('=', level)));
            }
        }

        // Note: Segment contexts were already accepted in the while loop at Accept() calls
        return Accept(heading);
    }

    /// <summary>
    /// Parses a paragraph (possibly merging with previous).
    /// </summary>
    private BlockNode ParseCompactParagraph(BlockNode? lastLine)
    {
        var mergeTo = lastLine as Paragraph;
        if (mergeTo is { Compact: false })
        {
            mergeTo = null;
        }

        BeginContext();

        if (mergeTo is not null)
        {
            // Continue previous paragraph
            if (mergeTo.Inlines.LastNode is PlainText lastText)
            {
                lastText.Content += "\n";
                var span = lastText.SourceSpan;
                lastText.ExtendSourceSpan(span.EndLine + 1, 0);
                mergeTo.ExtendSourceSpan(span.EndLine + 1, 0);
            }
        }

        var node = mergeTo ?? new Paragraph { Compact = true };
        ParseRun(RunParsingMode.Run, node, false);

        if (mergeTo is not null)
        {
            lastLine!.ExtendSourceSpan(_line, _column);
            Accept();
            return EmptyLineNode;
        }

        return Accept(node);
    }

    /// <summary>
    /// Parses a run of inline content.
    /// </summary>
    private bool ParseRun(RunParsingMode mode, IInlineContainer container, bool setSourceSpan)
    {
        BeginContext();
        var parsedAny = false;

        while (!NeedsTerminate())
        {
            _cancellationToken.ThrowIfCancellationRequested();

            InlineNode? inline = ParseExpandable();
            if (inline is not null)
            {
                goto AddNode;
            }

            inline = mode switch
            {
                RunParsingMode.Run => ParseInline(),
                RunParsingMode.ExpandableText => ParsePartialPlainText(),
                RunParsingMode.ExpandableUrl => ParseUrlText(),
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };

            if (inline is null)
            {
                break;
            }

        AddNode:
            parsedAny = true;

            // Merge consecutive PlainText nodes
            if (inline is PlainText newText && container.Inlines.LastNode is PlainText lastText)
            {
                lastText.Content += newText.Content;
                lastText.ExtendSourceSpan(_line, _column);
                continue;
            }

            container.Inlines.Add(inline);
        }

        if (parsedAny)
        {
            Accept((WikiNode)container, setSourceSpan);
            return true;
        }

        Rollback();
        return false;
    }

    private InlineNode? ParseInline()
    {
        return ParseTag()
            ?? ParseImageLink()
            ?? ParseWikiLink()
            ?? ParseExternalLink()
            ?? (InlineNode?)ParseFormatSwitch()
            ?? ParsePartialPlainText();
    }

    private InlineNode? ParseExpandable()
    {
        return ParseComment() ?? ParseBraces();
    }

    /// <summary>
    /// Pattern to detect potential element starts in plain text.
    /// </summary>
    private static readonly Regex PlainTextEndPattern = new(
        @"\[|\{\{\{?|<(\s*\w|!--)|'{2,5}(?!')|((https?:|ftp:|irc:|gopher:)//|news:|mailto:)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private PlainText? ParsePartialPlainText()
    {
        BeginContext();

        if (NeedsTerminate())
        {
            return Reject<PlainText>();
        }

        var terminatorPos = FindTerminator(1);
        var startPos = _position;

        // Look for potential element starts
        var match = PlainTextEndPattern.Match(_text, _position + 1, terminatorPos - _position - 1);

        int endPos;
        if (match.Success)
        {
            endPos = match.Index;
        }
        else if (terminatorPos > _position)
        {
            endPos = terminatorPos;
        }
        else
        {
            endPos = _text.Length;
        }

        AdvanceTo(endPos);
        var content = _text[startPos..endPos];

        return Accept(new PlainText(content));
    }

    /// <summary>
    /// Pattern for matching URLs.
    /// </summary>
    private const string UrlPattern =
        @"(?i)\b(((https?:|ftp:|irc:|gopher:)//)|news:|mailto:)([^\x00-\x20\s""\[\]\x7f\|\{\}<>]|<[^>]*>)+?(?=([!""().,:;'-]*\s|[\x00-\x20\s""\[\]\x7f|{}]|$))";

    private PlainText? ParseUrlText()
    {
        BeginContext();

        var url = Consume(UrlPattern);
        if (url is not null)
        {
            return Accept(new PlainText(url));
        }

        return Reject<PlainText>();
    }

    private FormatSwitch? ParseFormatSwitch()
    {
        BeginContext();

        var token = Consume(@"'{5}(?!')|'{3}(?!')|'{2}(?!')");
        if (token is null)
        {
            return Reject<FormatSwitch>();
        }

        var node = token.Length switch
        {
            2 => new FormatSwitch(false, true),
            3 => new FormatSwitch(true, false),
            5 => new FormatSwitch(true, true),
            _ => throw new InvalidOperationException("Invalid format switch length")
        };

        return Accept(node);
    }
}
