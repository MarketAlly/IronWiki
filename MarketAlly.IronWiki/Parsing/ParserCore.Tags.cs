// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Parsing;

internal sealed partial class ParserCore
{
    private static readonly Dictionary<string, Regex> ClosingTagCache = new();

    /// <summary>
    /// Parses an HTML or parser tag.
    /// </summary>
    private TagNode? ParseTag()
    {
        BeginContext();

        if (Consume("<") is null)
        {
            return Reject<TagNode>();
        }

        var tagName = Consume(@"[\w\-_:]+");
        if (tagName is null)
        {
            return Reject<TagNode>();
        }

        TagNode node = _options.IsParserTag(tagName)
            ? new ParserTag { Name = tagName }
            : new HtmlTag { Name = tagName };

        // Parse attributes
        var ws = Consume(@"\s+");
        string? rightBracket;

        while ((rightBracket = Consume("/?>")) is null)
        {
            if (ws is null)
            {
                // Need whitespace between attributes
                return Reject<TagNode>();
            }

            BeginContext();
            var attrName = ParseAttributeName();
            var attr = new TagAttributeNode { Name = attrName, LeadingWhitespace = ws };

            ws = Consume(@"\s+");

            if (Consume("=") is not null)
            {
                attr.WhitespaceBeforeEquals = ws;
                attr.WhitespaceAfterEquals = Consume(@"\s+");

                // Try different quote styles
                if (ParseAttributeValue(ValueQuoteStyle.SingleQuotes) is { } singleQuoted)
                {
                    attr.Value = singleQuoted;
                    attr.Quote = ValueQuoteStyle.SingleQuotes;
                }
                else if (ParseAttributeValue(ValueQuoteStyle.DoubleQuotes) is { } doubleQuoted)
                {
                    attr.Value = doubleQuoted;
                    attr.Quote = ValueQuoteStyle.DoubleQuotes;
                }
                else
                {
                    attr.Value = ParseAttributeValue(ValueQuoteStyle.None);
                    attr.Quote = ValueQuoteStyle.None;
                    Debug.Assert(attr.Value is not null);
                }

                ws = Consume(@"\s+");
            }

            Accept(attr);
            node.Attributes.Add(attr);
        }

        node.AttributeTrailingWhitespace = ws;

        if (rightBracket == "/>")
        {
            node.TagStyle = TagStyle.SelfClosing;
            return Accept(node);
        }

        if (_options.IsSelfClosingOnlyTag(tagName))
        {
            node.TagStyle = TagStyle.CompactSelfClosing;
            return Accept(node);
        }

        // Parse tag content
        if (ParseTagContent(node))
        {
            return Accept(node);
        }

        return Reject<TagNode>();
    }

    private Run? ParseAttributeName()
    {
        BeginContext(@"/?>|[\s=]", true);

        var node = new Run();
        if (ParseRun(RunParsingMode.Run, node, false))
        {
            return Accept(node);
        }

        return Reject<Run>();
    }

    private WikitextDocument? ParseAttributeValue(ValueQuoteStyle quoteStyle)
    {
        BeginContext(null, true);

        switch (quoteStyle)
        {
            case ValueQuoteStyle.None:
                {
                    var ctx = _contextStack.Peek();
                    _contextStack.Pop();
                    _contextStack.Push(new ParsingContext(GetTerminator(@"[>\s]|/>"),
                        ctx.OverridesTerminator, ctx.StartPosition, ctx.StartLine, ctx.StartColumn));

                    var value = ParseWikitext();
                    Accept();
                    return value;
                }

            case ValueQuoteStyle.SingleQuotes:
                if (Consume("'") is not null)
                {
                    var ctx = _contextStack.Peek();
                    _contextStack.Pop();
                    _contextStack.Push(new ParsingContext(GetTerminator(@"[>']|/>"),
                        ctx.OverridesTerminator, ctx.StartPosition, ctx.StartLine, ctx.StartColumn));

                    var value = ParseWikitext();
                    if (Consume(@"'(?=\s|>)") is not null)
                    {
                        Accept();
                        return value;
                    }
                }
                break;

            case ValueQuoteStyle.DoubleQuotes:
                if (Consume("\"") is not null)
                {
                    var ctx = _contextStack.Peek();
                    _contextStack.Pop();
                    _contextStack.Push(new ParsingContext(GetTerminator(@"[>""]|/>"),
                        ctx.OverridesTerminator, ctx.StartPosition, ctx.StartLine, ctx.StartColumn));

                    var value = ParseWikitext();
                    if (Consume(@"""(?=\s|>)") is not null)
                    {
                        Accept();
                        return value;
                    }
                }
                break;
        }

        return Reject<WikitextDocument>();
    }

    private bool ParseTagContent(TagNode node)
    {
        var normalizedName = node.Name.ToUpperInvariant();
        var closingTagPattern = "(?i)</(" + Regex.Escape(normalizedName) + @")(\s*)>";

        Regex closingTagRegex;
        lock (ClosingTagCache)
        {
            if (!ClosingTagCache.TryGetValue(normalizedName, out closingTagRegex!))
            {
                closingTagRegex = new Regex(closingTagPattern, RegexOptions.Compiled);
                ClosingTagCache[normalizedName] = closingTagRegex;
            }
        }

        if (node is ParserTag parserTag)
        {
            // Parser tags: content is raw text, not parsed
            var match = closingTagRegex.Match(_text, _position);
            if (match.Success)
            {
                parserTag.Content = _text[_position..match.Index];
                AdvanceTo(match.Index + match.Length);
                SetClosingTagInfo(node, match);
                return true;
            }

            // Unclosed parser tag - fail
            return false;
        }

        // HTML tag: parse content as wikitext
        var htmlTag = (HtmlTag)node;

        if (normalizedName == "li")
        {
            // Special handling for <li>: closed by </li>, <li>, or EOF
            BeginContext(@"</li\s*>|<li(\s*>|\s+)", false);
        }
        else
        {
            BeginContext(closingTagPattern, false);
        }

        htmlTag.Content = ParseWikitext();
        Accept();

        // Try to consume closing tag
        var closingTag = Consume(closingTagPattern);
        if (closingTag is null)
        {
            // Unbalanced HTML tag
            node.InferredClosingMark = true;
            node.ClosingTagName = null;
            node.TagStyle = TagStyle.NotClosed;
            return true;
        }

        var closingMatch = closingTagRegex.Match(closingTag);
        SetClosingTagInfo(node, closingMatch);
        return true;
    }

    private static void SetClosingTagInfo(TagNode node, Match match)
    {
        Debug.Assert(match.Success);
        Debug.Assert(match.Groups[1].Success);
        Debug.Assert(match.Groups[2].Success);

        var closingName = match.Groups[1].Value;
        node.ClosingTagName = closingName != node.Name ? closingName : null;
        node.ClosingTagTrailingWhitespace = match.Groups[2].Value;
    }
}
