// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Rendering;

/// <summary>
/// Renders wiki AST nodes as plain text, stripping markup and formatting.
/// </summary>
/// <remarks>
/// <para>This class is not thread-safe. Create a new instance for concurrent use.</para>
/// <para>For simple conversion, use the <see cref="WikiNodeExtensions.ToPlainText(WikiNode)"/> extension method.</para>
/// </remarks>
public class PlainTextRenderer
{
    private static PlainTextRenderer? _cachedInstance;

    /// <summary>
    /// Gets the output builder for custom rendering implementations.
    /// </summary>
    protected StringBuilder Output { get; } = new();

    /// <summary>
    /// Tags whose content should not be rendered as plain text.
    /// </summary>
    private static readonly HashSet<string> InvisibleTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "math", "ref", "templatedata", "templatestyles", "nowiki", "noinclude", "includeonly"
    };

    /// <summary>
    /// Renders a node as plain text.
    /// </summary>
    /// <param name="node">The node to render.</param>
    /// <returns>The plain text representation.</returns>
    public string Render(WikiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Output.Clear();
        RenderNode(node);
        return Output.ToString();
    }

    /// <summary>
    /// Renders a node to the output buffer.
    /// </summary>
    /// <param name="node">The node to render.</param>
    protected virtual void RenderNode(WikiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        switch (node)
        {
            case WikitextDocument doc:
                RenderDocument(doc);
                break;
            case Paragraph para:
                RenderParagraph(para);
                break;
            case Heading heading:
                RenderHeading(heading);
                break;
            case ListItem listItem:
                RenderListItem(listItem);
                break;
            case HorizontalRule:
                Output.AppendLine();
                break;
            case Table table:
                RenderTable(table);
                break;
            case TableRow row:
                RenderTableRow(row);
                break;
            case TableCell cell:
                RenderTableCell(cell);
                break;
            case TableCaption caption:
                RenderTableCaption(caption);
                break;
            case PlainText text:
                RenderPlainText(text);
                break;
            case WikiLink link:
                RenderWikiLink(link);
                break;
            case ExternalLink extLink:
                RenderExternalLink(extLink);
                break;
            case ImageLink imageLink:
                RenderImageLink(imageLink);
                break;
            case Template:
            case ArgumentReference:
            case Comment:
                // Templates, argument refs, and comments render as nothing
                break;
            case FormatSwitch:
                // Format switches don't produce output
                break;
            case ParserTag parserTag:
                RenderParserTag(parserTag);
                break;
            case HtmlTag htmlTag:
                RenderHtmlTag(htmlTag);
                break;
            case Run run:
                RenderRun(run);
                break;
            default:
                // For unknown nodes, render children
                foreach (var child in node.EnumerateChildren())
                {
                    RenderNode(child);
                }
                break;
        }
    }

    private void RenderDocument(WikitextDocument doc)
    {
        var isFirst = true;
        foreach (var line in doc.Lines)
        {
            if (!isFirst)
            {
                Output.AppendLine();
            }
            isFirst = false;
            RenderNode(line);
        }
    }

    private void RenderParagraph(Paragraph para)
    {
        foreach (var inline in para.Inlines)
        {
            RenderNode(inline);
        }
    }

    private void RenderHeading(Heading heading)
    {
        foreach (var inline in heading.Inlines)
        {
            RenderNode(inline);
        }
    }

    private void RenderListItem(ListItem listItem)
    {
        foreach (var inline in listItem.Inlines)
        {
            RenderNode(inline);
        }
    }

    private void RenderTable(Table table)
    {
        if (table.Caption is not null)
        {
            RenderNode(table.Caption);
        }

        var isFirstRow = true;
        foreach (var row in table.Rows)
        {
            if (!isFirstRow)
            {
                Output.AppendLine();
            }
            isFirstRow = false;
            RenderNode(row);
        }
    }

    private void RenderTableRow(TableRow row)
    {
        var isFirstCell = true;
        foreach (var cell in row.Cells)
        {
            if (!isFirstCell)
            {
                Output.Append('\t');
            }
            isFirstCell = false;
            RenderNode(cell);
        }
    }

    private void RenderTableCell(TableCell cell)
    {
        if (cell.Content is not null)
        {
            RenderNode(cell.Content);
        }
    }

    private void RenderTableCaption(TableCaption caption)
    {
        if (caption.Content is not null)
        {
            RenderNode(caption.Content);
        }
    }

    private void RenderPlainText(PlainText text)
    {
        // Decode HTML entities
        Output.Append(WebUtility.HtmlDecode(text.Content));
    }

    private void RenderWikiLink(WikiLink link)
    {
        if (link.Text is null)
        {
            // No display text - show target
            if (link.Target is not null)
            {
                RenderNode(link.Target);
            }
            return;
        }

        if (link.Text.Inlines.Count > 0)
        {
            RenderNode(link.Text);
            return;
        }

        // Pipe trick: [[Foo (bar)|]] -> Foo
        if (link.Target is not null)
        {
            var startPos = Output.Length;
            RenderNode(link.Target);

            // Remove disambiguation suffix
            if (Output.Length - startPos >= 3 && Output[^1] == ')')
            {
                for (var i = startPos + 1; i < Output.Length - 1; i++)
                {
                    if (Output[i] == '(')
                    {
                        // Remove " (disambiguation)" suffix
                        var removeFrom = i;
                        if (removeFrom > startPos && char.IsWhiteSpace(Output[removeFrom - 1]))
                        {
                            removeFrom--;
                        }
                        Output.Remove(removeFrom, Output.Length - removeFrom);
                        return;
                    }
                }
            }
        }
    }

    private void RenderExternalLink(ExternalLink link)
    {
        if (!link.HasBrackets)
        {
            if (link.Target is not null)
            {
                RenderNode(link.Target);
            }
            return;
        }

        if (link.Text is not null)
        {
            var startPos = Output.Length;
            RenderNode(link.Text);

            // Check if we rendered any non-whitespace
            for (var i = startPos; i < Output.Length; i++)
            {
                if (!char.IsWhiteSpace(Output[i]))
                {
                    return;
                }
            }
        }

        // No meaningful text - show placeholder
        Output.Append("[#]");
    }

    private void RenderImageLink(ImageLink imageLink)
    {
        // Render alt text if present
        var alt = imageLink.Arguments.FirstOrDefault(a =>
            a.Name is not null &&
            a.Name.Lines.Count > 0 &&
            a.Name.Lines[0] is Paragraph p &&
            p.Inlines.Count > 0 &&
            p.Inlines[0] is PlainText pt &&
            pt.Content.Equals("alt", StringComparison.OrdinalIgnoreCase));

        if (alt is not null)
        {
            RenderNode(alt.Value);
        }

        // Render caption (last unnamed argument)
        var caption = imageLink.Arguments.LastOrDefault(a => a.Name is null);
        if (caption is not null)
        {
            if (alt is not null)
            {
                Output.Append(' ');
            }
            RenderNode(caption.Value);
        }
    }

    private void RenderParserTag(ParserTag tag)
    {
        if (tag.Name is not null && InvisibleTags.Contains(tag.Name))
        {
            return;
        }

        Output.Append(tag.Content);
    }

    private void RenderHtmlTag(HtmlTag tag)
    {
        var name = tag.Name?.ToUpperInvariant();

        if (name is "BR" or "HR")
        {
            Output.Append('\n');
            if (tag.Content is not null)
            {
                RenderNode(tag.Content);
                Output.Append('\n');
            }
            return;
        }

        if (tag.Content is not null)
        {
            RenderNode(tag.Content);
        }
    }

    private void RenderRun(Run run)
    {
        foreach (var inline in run.Inlines)
        {
            RenderNode(inline);
        }
    }

    /// <summary>
    /// Gets a shared instance for single-threaded use.
    /// </summary>
    internal static PlainTextRenderer GetShared()
    {
        return Interlocked.Exchange(ref _cachedInstance, null) ?? new PlainTextRenderer();
    }

    /// <summary>
    /// Returns a shared instance to the cache.
    /// </summary>
    internal static void ReturnShared(PlainTextRenderer renderer)
    {
        Interlocked.CompareExchange(ref _cachedInstance, renderer, null);
    }
}

/// <summary>
/// Extension methods for rendering wiki nodes.
/// </summary>
public static class WikiNodeExtensions
{
    /// <summary>
    /// Converts a wiki node to plain text.
    /// </summary>
    /// <param name="node">The node to convert.</param>
    /// <returns>The plain text representation.</returns>
    public static string ToPlainText(this WikiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var renderer = PlainTextRenderer.GetShared();
        try
        {
            return renderer.Render(node);
        }
        finally
        {
            PlainTextRenderer.ReturnShared(renderer);
        }
    }

    /// <summary>
    /// Converts a wiki node to plain text using a custom renderer.
    /// </summary>
    /// <param name="node">The node to convert.</param>
    /// <param name="renderer">The renderer to use.</param>
    /// <returns>The plain text representation.</returns>
    public static string ToPlainText(this WikiNode node, PlainTextRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(renderer);

        return renderer.Render(node);
    }
}
