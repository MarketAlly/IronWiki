// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Parsing;

internal sealed partial class ParserCore
{
    /// <summary>
    /// Parses an image link [[File:Image.png|options]].
    /// </summary>
    private ImageLink? ParseImageLink()
    {
        if (LookAhead(@"\[\[") is null)
        {
            return null;
        }

        // Check for image namespace prefix
        var nsPattern = @"\[\[[\s_]*(?i:" + _options.ImageNamespacePattern + @")[\s_]*:";
        if (LookAhead(nsPattern) is null)
        {
            return null;
        }

        BeginContext(@"\||\]\]", true);

        if (Consume(@"\[\[") is null)
        {
            return Reject<ImageLink>();
        }

        // Parse target
        var target = new Run();
        BeginContext(@"\[\[|\n", false);

        if (!ParseRun(RunParsingMode.ExpandableText, target, true))
        {
            Rollback();
            return Reject<ImageLink>();
        }

        Accept();

        var node = new ImageLink { Target = target };

        // Parse arguments
        while (Consume(@"\|") is not null)
        {
            var arg = ParseImageLinkArgument();
            node.Arguments.Add(arg);
        }

        if (Consume(@"\]\]") is null)
        {
            if (_options.AllowClosingMarkInference)
            {
                node.InferredClosingMark = true;
            }
            else
            {
                return Reject<ImageLink>();
            }
        }

        return Accept(node);
    }

    private ImageLinkArgument ParseImageLinkArgument()
    {
        BeginContext(@"=", false);

        var name = ParseWikitext();
        Debug.Assert(name is not null);

        if (Consume(@"=") is not null)
        {
            // Named argument: name=value
            var ctx = _contextStack.Peek();
            _contextStack.Pop();
            _contextStack.Push(new ParsingContext(null, ctx.OverridesTerminator, ctx.StartPosition, ctx.StartLine, ctx.StartColumn));

            var value = ParseWikitext();
            Debug.Assert(value is not null);

            return Accept(new ImageLinkArgument { Name = name, Value = value });
        }

        return Accept(new ImageLinkArgument { Value = name });
    }

    /// <summary>
    /// Parses a wiki link [[Target|Text]].
    /// </summary>
    /// <remarks>
    /// Wiki links can span multiple lines. The text part (after the pipe) can contain line breaks.
    /// For example: [[Test|abc\ndef]] is a valid wikilink.
    /// </remarks>
    private WikiLink? ParseWikiLink()
    {
        // Note: \n is NOT included as a terminator because wiki links can span multiple lines
        BeginContext(@"\||\[\[|\]\]", true);

        if (Consume(@"\[\[") is null)
        {
            return Reject<WikiLink>();
        }

        var target = new Run();
        if (!ParseRun(RunParsingMode.ExpandableText, target, true))
        {
            if (_options.AllowEmptyWikiLinkTarget)
            {
                target = null;
            }
            else
            {
                return Reject<WikiLink>();
            }
        }

        var node = new WikiLink { Target = target };

        if (Consume(@"\|") is not null)
        {
            // Update terminator to allow pipes in text
            // Note: \n is NOT included because wiki link text can contain line breaks
            var ctx = _contextStack.Peek();
            _contextStack.Pop();
            _contextStack.Push(new ParsingContext(GetTerminator(@"\[\[|\]\]"), ctx.OverridesTerminator,
                ctx.StartPosition, ctx.StartLine, ctx.StartColumn));

            var text = new Run();
            if (ParseRun(RunParsingMode.ExpandableText, text, true))
            {
                node.Text = text;
            }
            else
            {
                // Empty text after pipe: [[Target|]]
                node.Text = new Run();
            }
        }

        if (Consume(@"\]\]") is null)
        {
            if (_options.AllowClosingMarkInference)
            {
                node.InferredClosingMark = true;
            }
            else
            {
                return Reject<WikiLink>();
            }
        }

        return Accept(node);
    }

    /// <summary>
    /// Parses an external link [URL text] or bare URL.
    /// </summary>
    private ExternalLink? ParseExternalLink()
    {
        BeginContext(@"[\s\]\|]", true);

        var hasBrackets = Consume(@"\[") is not null;
        Run? target;

        if (hasBrackets)
        {
            target = new Run();
            if (!ParseRun(RunParsingMode.ExpandableUrl, target, true))
            {
                if (_options.AllowEmptyExternalLinkTarget)
                {
                    target = null;
                }
                else
                {
                    return Reject<ExternalLink>();
                }
            }
        }
        else
        {
            // Bare URL - must match URL pattern
            var url = ParseUrlText();
            if (url is null)
            {
                return Reject<ExternalLink>();
            }
            target = new Run(url);
            target.SetSourceSpan(url);
        }

        var node = new ExternalLink { Target = target, HasBrackets = hasBrackets };

        if (hasBrackets)
        {
            // Parse display text after space/tab
            if (Consume(@"[ \t]") is not null)
            {
                var ctx = _contextStack.Peek();
                _contextStack.Pop();
                _contextStack.Push(new ParsingContext(GetTerminator(@"[\]\n]"), ctx.OverridesTerminator,
                    ctx.StartPosition, ctx.StartLine, ctx.StartColumn));

                var text = new Run();
                if (ParseRun(RunParsingMode.Run, text, true))
                {
                    node.Text = text;
                }
                else
                {
                    // Empty text: [URL ]
                    node.Text = new Run();
                }
            }

            if (Consume(@"\]") is null)
            {
                return Reject<ExternalLink>();
            }
        }

        return Accept(node);
    }
}
