// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Parsing;

internal sealed partial class ParserCore
{
    private static readonly Regex CommentSuffixPattern = new("-->", RegexOptions.Compiled);

    /// <summary>
    /// Parses an HTML comment &lt;!-- ... --&gt;.
    /// </summary>
    private Comment? ParseComment()
    {
        BeginContext();

        if (Consume("<!--") is null)
        {
            return Reject<Comment>();
        }

        var contentStart = _position;
        var match = CommentSuffixPattern.Match(_text, _position);

        string content;
        if (match.Success)
        {
            content = _text[contentStart..match.Index];
            AdvanceTo(match.Index + match.Length);
        }
        else
        {
            content = _text[contentStart..];
            AdvanceTo(_text.Length);
        }

        return Accept(new Comment(content));
    }

    /// <summary>
    /// Parses brace constructs (templates or argument references).
    /// </summary>
    /// <remarks>
    /// MediaWiki's brace parsing follows these rules:
    /// - 2 braces = template {{...}}
    /// - 3 braces = argument reference {{{...}}}
    /// - 4+ braces = look ahead to find matching closing braces
    ///
    /// The key insight for pathological cases like {{{{{arg}}:
    /// - 5 opening braces with only 2 closing braces
    /// - Need to find the innermost valid construct
    /// - Output excess braces as plain text
    /// </remarks>
    private InlineNode? ParseBraces()
    {
        var braces = LookAhead(@"\{+");
        if (braces is null || braces.Length < 2)
        {
            return null;
        }

        // For exactly 2 braces, parse as template
        if (braces.Length == 2)
        {
            return ParseTemplate();
        }

        // For exactly 3 braces, try argument reference first, then template
        if (braces.Length == 3)
        {
            return (InlineNode?)ParseArgumentReference() ?? ParseTemplate();
        }

        // For 4+ braces, we need to find the innermost matching construct.
        // MediaWiki processes from the inside out, matching closing braces.
        //
        // For {{{{{arg}}:
        //   - Look for first }} or }}} after the opening braces
        //   - We find }} (2 closing braces)
        //   - So the innermost construct is a template: {{arg}}
        //   - The remaining {{{ before it become plain text
        //
        // Strategy: Look ahead to find closing braces and determine what construct they form

        // Find the content and closing braces
        var searchStart = _position + braces.Length;
        var closingMatch = FindClosingBraces(searchStart);

        if (closingMatch.Position < 0)
        {
            // No closing braces found - output one { and try again
            BeginContext();
            Consume(@"\{");
            return Accept(new PlainText("{"));
        }

        var closingCount = closingMatch.Count;

        // Determine how many opening braces to use based on closing braces
        // We want to match the innermost valid construct
        int openingToUse;
        if (closingCount >= 3)
        {
            // Try argument reference first (use 3 opening to match 3 closing)
            openingToUse = 3;
        }
        else
        {
            // Only 2 closing braces - must be a template
            openingToUse = 2;
        }

        // Output excess opening braces as plain text
        var excessBraces = braces.Length - openingToUse;
        if (excessBraces > 0)
        {
            BeginContext();
            // Consume exactly excessBraces opening braces using regex quantifier
            var pattern = @"\{" + "{" + excessBraces + "}";
            Consume(pattern);
            return Accept(new PlainText(new string('{', excessBraces)));
        }

        // Now parse the construct with the correct number of braces
        if (openingToUse == 3)
        {
            return (InlineNode?)ParseArgumentReference() ?? ParseTemplate();
        }
        else
        {
            return ParseTemplate();
        }
    }

    /// <summary>
    /// Finds the next closing braces (}} or }}}) after the given position.
    /// </summary>
    private (int Position, int Count) FindClosingBraces(int startPosition)
    {
        // Look for }}} or }}
        for (var i = startPosition; i < _text.Length - 1; i++)
        {
            if (_text[i] == '}' && _text[i + 1] == '}')
            {
                // Count consecutive closing braces
                var count = 2;
                if (i + 2 < _text.Length && _text[i + 2] == '}')
                {
                    count = 3;
                    // Check for more
                    var j = i + 3;
                    while (j < _text.Length && _text[j] == '}')
                    {
                        count++;
                        j++;
                    }
                }
                return (i, count);
            }
        }
        return (-1, 0);
    }

    /// <summary>
    /// Parses a template argument reference {{{name|default}}}.
    /// </summary>
    private ArgumentReference? ParseArgumentReference()
    {
        BeginContext(@"\}\}\}|\|", true);

        if (Consume(@"\{\{\{") is null)
        {
            return Reject<ArgumentReference>();
        }

        var name = ParseWikitext();
        Debug.Assert(name is not null);

        WikitextDocument? defaultValue = null;
        if (Consume(@"\|") is not null)
        {
            defaultValue = ParseWikitext();
        }

        // Consume any extra pipe-separated values (they're ignored)
        while (Consume(@"\|") is not null)
        {
            ParseWikitext();
        }

        if (Consume(@"\}\}\}") is null)
        {
            return Reject<ArgumentReference>();
        }

        return Accept(new ArgumentReference { Name = name, DefaultValue = defaultValue });
    }

    /// <summary>
    /// Parses a template {{name|arg1|arg2}}.
    /// </summary>
    private Template? ParseTemplate()
    {
        BeginContext(@"\}\}|\|", true);

        if (Consume(@"\{\{") is null)
        {
            return Reject<Template>();
        }

        var node = new Template(new Run());

        // Determine if this is a magic word (variable or parser function)
        if (LookAhead(@"\s*#") is not null)
        {
            node.IsMagicWord = true;
        }
        else
        {
            var nameMatch = LookAhead(@"\s*[^:\|\{\}]+(?=[:\}])");
            if (nameMatch is not null)
            {
                var trimmedName = nameMatch.Trim();
                node.IsMagicWord = _options.IsMagicWord(trimmedName);
            }
        }

        if (node.IsMagicWord)
        {
            BeginContext(":", false);
            if (!ParseRun(RunParsingMode.ExpandableText, node.Name!, true))
            {
                Debug.Fail("Should have been able to read magic word name");
                Rollback();
                return Reject<Template>();
            }
            Accept();

            // Parse first argument after colon
            if (Consume(":") is not null)
            {
                node.Arguments.Add(ParseTemplateArgument());
            }
        }
        else
        {
            if (!ParseRun(RunParsingMode.ExpandableText, node.Name!, true))
            {
                if (_options.AllowEmptyTemplateName)
                {
                    node.Name = null;
                }
                else
                {
                    return Reject<Template>();
                }
            }
        }

        // Parse remaining arguments
        while (Consume(@"\|") is not null)
        {
            node.Arguments.Add(ParseTemplateArgument());
        }

        if (Consume(@"\}\}") is null)
        {
            if (_options.AllowClosingMarkInference)
            {
                node.InferredClosingMark = true;
            }
            else
            {
                return Reject<Template>();
            }
        }

        return Accept(node);
    }

    /// <summary>
    /// Parses a template argument.
    /// </summary>
    private TemplateArgument ParseTemplateArgument()
    {
        BeginContext("=", false);

        var name = ParseWikitext();
        Debug.Assert(name is not null);

        if (Consume(@"=") is not null)
        {
            // Named argument
            var ctx = _contextStack.Peek();
            _contextStack.Pop();
            _contextStack.Push(new ParsingContext(null, ctx.OverridesTerminator,
                ctx.StartPosition, ctx.StartLine, ctx.StartColumn));

            var value = ParseWikitext();
            Debug.Assert(value is not null);

            return Accept(new TemplateArgument { Name = name, Value = value });
        }

        return Accept(new TemplateArgument { Value = name });
    }
}
