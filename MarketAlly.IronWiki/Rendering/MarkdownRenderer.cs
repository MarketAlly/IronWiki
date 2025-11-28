// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using MarketAlly.IronWiki.Nodes;

#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable CA1307 // Specify StringComparison for clarity
#pragma warning disable CA1308 // Normalize strings to uppercase
#pragma warning disable CA1310 // Specify StringComparison for correctness
#pragma warning disable CA1834 // Use StringBuilder.Append(char)

namespace MarketAlly.IronWiki.Rendering;

/// <summary>
/// Renders wikitext AST to Markdown.
/// </summary>
/// <remarks>
/// <para>This renderer converts parsed wikitext to GitHub-flavored Markdown.
/// Templates and images are resolved using optional <see cref="ITemplateResolver"/>
/// and <see cref="IImageResolver"/> implementations.</para>
/// </remarks>
/// <example>
/// <code>
/// var parser = new WikitextParser();
/// var ast = parser.Parse("== Hello ==\nThis is '''bold'''.");
///
/// var renderer = new MarkdownRenderer();
/// var markdown = renderer.Render(ast);
/// // Output: ## Hello\n\nThis is **bold**.
/// </code>
/// </example>
public class MarkdownRenderer
{
    private readonly ITemplateResolver? _templateResolver;
    private readonly IImageResolver? _imageResolver;
    private readonly MarkdownRenderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownRenderer"/> class.
    /// </summary>
    /// <param name="templateResolver">Optional template resolver for expanding templates.</param>
    /// <param name="imageResolver">Optional image resolver for resolving image URLs.</param>
    /// <param name="options">Optional rendering options.</param>
    public MarkdownRenderer(
        ITemplateResolver? templateResolver = null,
        IImageResolver? imageResolver = null,
        MarkdownRenderOptions? options = null)
    {
        _templateResolver = templateResolver;
        _imageResolver = imageResolver;
        _options = options ?? new MarkdownRenderOptions();
    }

    /// <summary>
    /// Renders a wikitext document to Markdown.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="context">Optional render context.</param>
    /// <returns>The rendered Markdown string.</returns>
    public string Render(WikitextDocument document, RenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        context ??= new RenderContext();

        var sb = new StringBuilder();
        RenderDocument(document, sb, context);
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Renders a wiki node to Markdown.
    /// </summary>
    /// <param name="node">The node to render.</param>
    /// <param name="context">Optional render context.</param>
    /// <returns>The rendered Markdown string.</returns>
    public string Render(WikiNode node, RenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        context ??= new RenderContext();

        var sb = new StringBuilder();
        RenderNode(node, sb, context, inParagraph: false);
        return sb.ToString().TrimEnd();
    }

    private void RenderDocument(WikitextDocument document, StringBuilder sb, RenderContext context)
    {
        BlockNode? lastBlock = null;

        foreach (var line in document.Lines)
        {
            // Add blank line between different block types
            if (lastBlock is not null)
            {
                var needsBlankLine = ShouldAddBlankLine(lastBlock, line);
                if (needsBlankLine)
                {
                    sb.AppendLine();
                }
            }

            RenderNode(line, sb, context, inParagraph: false);
            lastBlock = line;
        }
    }

    private static bool ShouldAddBlankLine(BlockNode previous, BlockNode current)
    {
        // Always blank line after headings
        if (previous is Heading) return true;

        // Blank line between paragraphs
        if (previous is Paragraph && current is Paragraph) return true;

        // Blank line before headings
        if (current is Heading) return true;

        // Blank line before/after tables
        if (previous is Table || current is Table) return true;

        // No blank line between consecutive list items
        if (previous is ListItem && current is ListItem) return false;

        return false;
    }

    private void RenderNode(WikiNode node, StringBuilder sb, RenderContext context, bool inParagraph)
    {
        switch (node)
        {
            case WikitextDocument doc:
                RenderDocument(doc, sb, context);
                break;

            case Heading heading:
                RenderHeading(heading, sb, context);
                break;

            case Paragraph para:
                RenderParagraph(para, sb, context);
                break;

            case ListItem listItem:
                RenderListItem(listItem, sb, context);
                break;

            case Table table:
                RenderTable(table, sb, context);
                break;

            case PlainText text:
                sb.Append(EscapeMarkdown(text.Content, inParagraph));
                break;

            case WikiLink link:
                RenderWikiLink(link, sb, context);
                break;

            case ExternalLink extLink:
                RenderExternalLink(extLink, sb, context);
                break;

            case ImageLink imageLink:
                RenderImageLink(imageLink, sb, context);
                break;

            case Template template:
                RenderTemplate(template, sb, context);
                break;

            case ArgumentReference argRef:
                RenderArgumentReference(argRef, sb);
                break;

            case FormatSwitch format:
                RenderFormatSwitch(format, sb);
                break;

            case Comment comment:
                if (_options.IncludeComments)
                {
                    sb.Append("<!-- ").Append(comment.Content).Append(" -->");
                }
                break;

            case HtmlTag htmlTag:
                RenderHtmlTag(htmlTag, sb, context);
                break;

            case ParserTag parserTag:
                RenderParserTag(parserTag, sb, context);
                break;

            case Run run:
                RenderInlines(run.Inlines, sb, context);
                break;

            default:
                // Unknown node type - render children
                foreach (var child in node.EnumerateChildren())
                {
                    RenderNode(child, sb, context, inParagraph);
                }
                break;
        }
    }

    private void RenderHeading(Heading heading, StringBuilder sb, RenderContext context)
    {
        var level = Math.Clamp(heading.Level, 1, 6);

        // Markdown heading prefix
        sb.Append(new string('#', level)).Append(' ');
        RenderInlines(heading.Inlines, sb, context);
        sb.AppendLine();
    }

    private void RenderParagraph(Paragraph para, StringBuilder sb, RenderContext context)
    {
        RenderInlines(para.Inlines, sb, context);
        sb.AppendLine();
    }

    private void RenderListItem(ListItem item, StringBuilder sb, RenderContext context)
    {
        var prefix = item.Prefix ?? "*";
        var depth = prefix.Length;
        var indent = new string(' ', (depth - 1) * 2);

        // Determine list marker
        var lastChar = prefix[^1];
        var marker = lastChar switch
        {
            '#' => "1.",
            ';' => "", // Definition term - render as bold
            ':' => "", // Definition description - render indented
            _ => "-"
        };

        if (lastChar == ';')
        {
            // Definition term
            sb.Append(indent).Append("**");
            RenderInlines(item.Inlines, sb, context);
            sb.AppendLine("**");
        }
        else if (lastChar == ':')
        {
            // Definition description or indent - use blockquote
            sb.Append(indent).Append("> ");
            RenderInlines(item.Inlines, sb, context);
            sb.AppendLine();
        }
        else
        {
            sb.Append(indent).Append(marker).Append(' ');
            RenderInlines(item.Inlines, sb, context);
            sb.AppendLine();
        }
    }

    private void RenderTable(Table table, StringBuilder sb, RenderContext context)
    {
        // Find the number of columns by looking at all rows
        var maxColumns = 0;
        foreach (var row in table.Rows)
        {
            maxColumns = Math.Max(maxColumns, row.Cells.Count);
        }

        if (maxColumns == 0) return;

        // Caption
        if (table.Caption is not null && table.Caption.Content is not null)
        {
            sb.Append("**");
            RenderInlines(table.Caption.Content.Inlines, sb, context);
            sb.AppendLine("**");
            sb.AppendLine();
        }

        var isFirstRow = true;

        foreach (var row in table.Rows)
        {
            sb.Append('|');

            for (var i = 0; i < maxColumns; i++)
            {
                if (i < row.Cells.Count)
                {
                    var cell = row.Cells[i];
                    sb.Append(' ');

                    if (cell.Content is not null)
                    {
                        RenderInlinesForTable(cell.Content.Inlines, sb, context);
                    }

                    sb.Append(" |");
                }
                else
                {
                    sb.Append(" |");
                }
            }

            sb.AppendLine();

            // Add header separator after first row
            if (isFirstRow)
            {
                sb.Append('|');
                for (var i = 0; i < maxColumns; i++)
                {
                    sb.Append(" --- |");
                }
                sb.AppendLine();
                isFirstRow = false;
            }
        }
    }

    private static void RenderWikiLink(WikiLink link, StringBuilder sb, RenderContext context)
    {
        var target = link.Target?.ToString().Trim() ?? "";
        var displayText = link.Text?.ToString().Trim();

        // Use display text if provided, otherwise use target
        if (string.IsNullOrEmpty(displayText))
        {
            displayText = target;
            // Handle pipe trick
            if (displayText.Contains('('))
            {
                displayText = displayText[..displayText.IndexOf('(')].Trim();
            }
        }

        // Build URL
        var url = context.WikiLinkBaseUrl + Uri.EscapeDataString(target.Replace(' ', '_'));

        sb.Append('[').Append(EscapeLinkText(displayText)).Append("](").Append(url).Append(')');
    }

    private static void RenderExternalLink(ExternalLink link, StringBuilder sb, RenderContext context)
    {
        var url = link.Target?.ToString().Trim() ?? "";
        var displayText = link.Text?.ToString().Trim();

        if (string.IsNullOrEmpty(displayText))
        {
            // Bare URL
            sb.Append('<').Append(url).Append('>');
        }
        else
        {
            sb.Append('[').Append(EscapeLinkText(displayText)).Append("](").Append(url).Append(')');
        }
    }

    private void RenderImageLink(ImageLink imageLink, StringBuilder sb, RenderContext context)
    {
        var imageInfo = _imageResolver?.Resolve(imageLink, context);
        var fileName = ExtractFileName(imageLink);

        // Parse options to get alt text and caption
        string? altText = null;
        string? caption = null;

        foreach (var arg in imageLink.Arguments)
        {
            var name = arg.Name?.ToString().Trim().ToLowerInvariant();
            var value = arg.Value?.ToString().Trim();

            if (name == "alt")
            {
                altText = value;
            }
            else if (arg.Name is null && value is not null &&
                     !IsImageKeyword(value.ToLowerInvariant()))
            {
                caption = value;
            }
        }

        altText ??= caption ?? fileName;

        if (imageInfo is not null)
        {
            var url = imageInfo.ThumbnailUrl ?? imageInfo.Url;
            sb.Append("![").Append(EscapeLinkText(altText)).Append("](").Append(url).Append(')');
        }
        else if (_options.ImagePlaceholderMode == MarkdownImagePlaceholderMode.LinkToFile)
        {
            // Render as a link placeholder
            var url = context.ImageDescriptionBaseUrl + Uri.EscapeDataString(fileName);
            sb.Append("[🖼 ").Append(EscapeLinkText(altText)).Append("](").Append(url).Append(')');
        }
        else
        {
            // Alt text only
            sb.Append(altText);
        }
    }

    private static bool IsImageKeyword(string value)
    {
        return value is "thumb" or "thumbnail" or "frame" or "frameless" or "border"
            or "left" or "right" or "center" or "centre" or "none"
            || value.EndsWith("px");
    }

    private static string ExtractFileName(ImageLink imageLink)
    {
        var target = imageLink.Target?.ToString().Trim() ?? "";
        var colonIndex = target.IndexOf(':');
        return colonIndex >= 0 ? target[(colonIndex + 1)..].Trim() : target;
    }

    private void RenderTemplate(Template template, StringBuilder sb, RenderContext context)
    {
        if (context.IsRecursionLimitExceeded)
        {
            sb.Append("[Template recursion limit exceeded]");
            return;
        }

        var resolved = _templateResolver?.Resolve(template, context);

        if (resolved is not null)
        {
            sb.Append(resolved);
        }
        else
        {
            // Render as placeholder based on options
            var name = template.Name?.ToString().Trim() ?? "?";

            switch (_options.TemplatePlaceholderMode)
            {
                case MarkdownTemplatePlaceholderMode.Hidden:
                    // Don't render anything
                    break;

                case MarkdownTemplatePlaceholderMode.NameOnly:
                    sb.Append("`{{").Append(name).Append("}}`");
                    break;

                case MarkdownTemplatePlaceholderMode.Full:
                    sb.Append("`{{").Append(name);
                    foreach (var arg in template.Arguments)
                    {
                        sb.Append('|');
                        if (arg.Name is not null)
                        {
                            sb.Append(arg.Name.ToString()).Append('=');
                        }
                        if (arg.Value is not null)
                        {
                            sb.Append(arg.Value.ToString());
                        }
                    }
                    sb.Append("}}`");
                    break;
            }
        }
    }

    private static void RenderArgumentReference(ArgumentReference argRef, StringBuilder sb)
    {
        var name = argRef.Name?.ToString().Trim() ?? "?";
        sb.Append("`{{{").Append(name);

        if (argRef.DefaultValue is not null)
        {
            sb.Append('|').Append(argRef.DefaultValue.ToString());
        }

        sb.Append("}}}`");
    }

    private static void RenderFormatSwitch(FormatSwitch format, StringBuilder sb)
    {
        // These are handled in RenderInlines for proper state tracking
        // This is a fallback for direct rendering
        if (format.SwitchBold && format.SwitchItalics)
        {
            sb.Append("***");
        }
        else if (format.SwitchBold)
        {
            sb.Append("**");
        }
        else if (format.SwitchItalics)
        {
            sb.Append('*');
        }
    }

    private void RenderHtmlTag(HtmlTag tag, StringBuilder sb, RenderContext context)
    {
        var name = tag.Name.ToLowerInvariant();

        // Convert some HTML tags to Markdown equivalents
        switch (name)
        {
            case "br":
                sb.Append("  \n"); // Two spaces + newline for line break
                break;

            case "hr":
                sb.AppendLine().AppendLine("---");
                break;

            case "b":
            case "strong":
                sb.Append("**");
                if (tag.Content is not null)
                {
                    RenderNode(tag.Content, sb, context, inParagraph: true);
                }
                sb.Append("**");
                break;

            case "i":
            case "em":
                sb.Append('*');
                if (tag.Content is not null)
                {
                    RenderNode(tag.Content, sb, context, inParagraph: true);
                }
                sb.Append('*');
                break;

            case "code":
                sb.Append('`');
                if (tag.Content is not null)
                {
                    RenderNode(tag.Content, sb, context, inParagraph: true);
                }
                sb.Append('`');
                break;

            case "pre":
                sb.AppendLine("```");
                if (tag.Content is not null)
                {
                    RenderNode(tag.Content, sb, context, inParagraph: false);
                }
                sb.AppendLine().AppendLine("```");
                break;

            case "blockquote":
                sb.Append("> ");
                if (tag.Content is not null)
                {
                    RenderNode(tag.Content, sb, context, inParagraph: true);
                }
                sb.AppendLine();
                break;

            case "s":
            case "del":
                sb.Append("~~");
                if (tag.Content is not null)
                {
                    RenderNode(tag.Content, sb, context, inParagraph: true);
                }
                sb.Append("~~");
                break;

            case "a":
                // Try to extract href
                var href = tag.Attributes.FirstOrDefault(a =>
                    a.Name?.ToString().Equals("href", StringComparison.OrdinalIgnoreCase) == true)?.Value?.ToString();
                if (href is not null && tag.Content is not null)
                {
                    sb.Append('[');
                    RenderNode(tag.Content, sb, context, inParagraph: true);
                    sb.Append("](").Append(href).Append(')');
                }
                else if (tag.Content is not null)
                {
                    RenderNode(tag.Content, sb, context, inParagraph: true);
                }
                break;

            default:
                // For other tags, just render content
                if (tag.Content is not null)
                {
                    RenderNode(tag.Content, sb, context, inParagraph: true);
                }
                break;
        }
    }

    private void RenderParserTag(ParserTag tag, StringBuilder sb, RenderContext context)
    {
        var name = tag.Name.ToLowerInvariant();

        switch (name)
        {
            case "nowiki":
                sb.Append(tag.Content ?? "");
                break;

            case "ref":
                // Render as footnote marker
                sb.Append("[^ref]");
                break;

            case "references":
                sb.AppendLine().AppendLine("[^ref]: References");
                break;

            case "code":
            case "source":
            case "syntaxhighlight":
                // Extract language if specified
                var lang = tag.Attributes
                    .FirstOrDefault(a => a.Name?.ToString().Equals("lang", StringComparison.OrdinalIgnoreCase) == true)
                    ?.Value?.ToString() ?? "";

                sb.AppendLine($"```{lang}");
                sb.AppendLine(tag.Content ?? "");
                sb.AppendLine("```");
                break;

            case "math":
                // Render as inline code or LaTeX delimiters
                if (_options.UseLaTeXMath)
                {
                    sb.Append('$').Append(tag.Content ?? "").Append('$');
                }
                else
                {
                    sb.Append('`').Append(tag.Content ?? "").Append('`');
                }
                break;

            case "gallery":
                // Render gallery items as a list of images
                sb.AppendLine().AppendLine("*Gallery:*");
                if (!string.IsNullOrEmpty(tag.Content))
                {
                    var lines = tag.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            sb.Append("- ").AppendLine(trimmed);
                        }
                    }
                }
                break;

            default:
                if (!string.IsNullOrEmpty(tag.Content))
                {
                    sb.Append(tag.Content);
                }
                break;
        }
    }

    private void RenderInlines(WikiNodeCollection<InlineNode> inlines, StringBuilder sb, RenderContext context)
    {
        // Track bold/italic state for proper Markdown markers
        var isBold = false;
        var isItalic = false;

        foreach (var inline in inlines)
        {
            if (inline is FormatSwitch format)
            {
                if (format.SwitchBold && format.SwitchItalics)
                {
                    // Toggle both
                    if (isBold && isItalic)
                    {
                        sb.Append("***");
                        isBold = false;
                        isItalic = false;
                    }
                    else if (!isBold && !isItalic)
                    {
                        sb.Append("***");
                        isBold = true;
                        isItalic = true;
                    }
                    else
                    {
                        // Mixed state - close what's open, open what's closed
                        if (isBold) { sb.Append("**"); isBold = false; }
                        else { sb.Append("**"); isBold = true; }
                        if (isItalic) { sb.Append('*'); isItalic = false; }
                        else { sb.Append('*'); isItalic = true; }
                    }
                }
                else if (format.SwitchBold)
                {
                    sb.Append("**");
                    isBold = !isBold;
                }
                else if (format.SwitchItalics)
                {
                    sb.Append('*');
                    isItalic = !isItalic;
                }
            }
            else
            {
                RenderNode(inline, sb, context, inParagraph: true);
            }
        }

        // Close any unclosed formatting
        if (isBold) sb.Append("**");
        if (isItalic) sb.Append('*');
    }

    private void RenderInlinesForTable(WikiNodeCollection<InlineNode> inlines, StringBuilder sb, RenderContext context)
    {
        // Same as RenderInlines but escapes pipe characters
        var isBold = false;
        var isItalic = false;

        foreach (var inline in inlines)
        {
            if (inline is FormatSwitch format)
            {
                if (format.SwitchBold && format.SwitchItalics)
                {
                    sb.Append("***");
                    isBold = !isBold;
                    isItalic = !isItalic;
                }
                else if (format.SwitchBold)
                {
                    sb.Append("**");
                    isBold = !isBold;
                }
                else if (format.SwitchItalics)
                {
                    sb.Append('*');
                    isItalic = !isItalic;
                }
            }
            else if (inline is PlainText text)
            {
                // Escape pipes in table cells
                sb.Append(text.Content.Replace("|", "\\|"));
            }
            else
            {
                RenderNode(inline, sb, context, inParagraph: true);
            }
        }

        if (isBold) sb.Append("**");
        if (isItalic) sb.Append('*');
    }

    private static string EscapeMarkdown(string text, bool inParagraph)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new StringBuilder(text.Length + 10);

        foreach (var c in text)
        {
            // Escape Markdown special characters
            if (inParagraph && c is '*' or '_' or '`' or '[' or ']' or '\\')
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string EscapeLinkText(string text)
    {
        // Escape brackets in link text
        return text.Replace("[", "\\[").Replace("]", "\\]");
    }
}

/// <summary>
/// Options for Markdown rendering.
/// </summary>
public class MarkdownRenderOptions
{
    /// <summary>
    /// Gets or sets whether to include HTML comments in output.
    /// </summary>
    public bool IncludeComments { get; set; }

    /// <summary>
    /// Gets or sets how unresolved templates should be rendered.
    /// </summary>
    public MarkdownTemplatePlaceholderMode TemplatePlaceholderMode { get; set; } = MarkdownTemplatePlaceholderMode.NameOnly;

    /// <summary>
    /// Gets or sets how unresolved images should be rendered.
    /// </summary>
    public MarkdownImagePlaceholderMode ImagePlaceholderMode { get; set; } = MarkdownImagePlaceholderMode.LinkToFile;

    /// <summary>
    /// Gets or sets whether to use LaTeX delimiters for math content.
    /// </summary>
    public bool UseLaTeXMath { get; set; } = true;
}

/// <summary>
/// Specifies how unresolved templates should be rendered in Markdown.
/// </summary>
public enum MarkdownTemplatePlaceholderMode
{
    /// <summary>
    /// Don't render anything for unresolved templates.
    /// </summary>
    Hidden,

    /// <summary>
    /// Render as `{{TemplateName}}` (in code span).
    /// </summary>
    NameOnly,

    /// <summary>
    /// Render the full template syntax in a code span.
    /// </summary>
    Full
}

/// <summary>
/// Specifies how unresolved images should be rendered in Markdown.
/// </summary>
public enum MarkdownImagePlaceholderMode
{
    /// <summary>
    /// Render only the alt text.
    /// </summary>
    AltTextOnly,

    /// <summary>
    /// Render as a link to the file description page.
    /// </summary>
    LinkToFile
}
