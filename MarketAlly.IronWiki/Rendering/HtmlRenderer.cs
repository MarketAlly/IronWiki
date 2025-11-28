// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Web;
using MarketAlly.IronWiki.Nodes;

#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable CA1307 // Specify StringComparison for clarity
#pragma warning disable CA1308 // Normalize strings to uppercase
#pragma warning disable CA1310 // Specify StringComparison for correctness
#pragma warning disable CA1834 // Use StringBuilder.Append(char)

namespace MarketAlly.IronWiki.Rendering;

/// <summary>
/// Renders wikitext AST to HTML.
/// </summary>
/// <remarks>
/// <para>This renderer converts parsed wikitext to HTML. Templates and images are resolved
/// using optional <see cref="ITemplateResolver"/> and <see cref="IImageResolver"/> implementations.</para>
/// <para>If no resolvers are provided, templates render as placeholders and images render with
/// alt text only.</para>
/// </remarks>
/// <example>
/// <code>
/// var parser = new WikitextParser();
/// var ast = parser.Parse("== Hello ==\nThis is '''bold'''.");
///
/// var renderer = new HtmlRenderer();
/// var html = renderer.Render(ast);
/// // Output: &lt;h2&gt;Hello&lt;/h2&gt;\n&lt;p&gt;This is &lt;b&gt;bold&lt;/b&gt;.&lt;/p&gt;
/// </code>
/// </example>
public class HtmlRenderer
{
    private readonly ITemplateResolver? _templateResolver;
    private readonly IImageResolver? _imageResolver;
    private readonly HtmlRenderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlRenderer"/> class.
    /// </summary>
    /// <param name="templateResolver">Optional template resolver for expanding templates.</param>
    /// <param name="imageResolver">Optional image resolver for resolving image URLs.</param>
    /// <param name="options">Optional rendering options.</param>
    public HtmlRenderer(
        ITemplateResolver? templateResolver = null,
        IImageResolver? imageResolver = null,
        HtmlRenderOptions? options = null)
    {
        _templateResolver = templateResolver;
        _imageResolver = imageResolver;
        _options = options ?? new HtmlRenderOptions();
    }

    /// <summary>
    /// Renders a wikitext document to HTML.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="context">Optional render context.</param>
    /// <returns>The rendered HTML string.</returns>
    public string Render(WikitextDocument document, RenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        context ??= new RenderContext();

        var sb = new StringBuilder();
        RenderDocument(document, sb, context);
        return sb.ToString();
    }

    /// <summary>
    /// Renders a wiki node to HTML.
    /// </summary>
    /// <param name="node">The node to render.</param>
    /// <param name="context">Optional render context.</param>
    /// <returns>The rendered HTML string.</returns>
    public string Render(WikiNode node, RenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        context ??= new RenderContext();

        var sb = new StringBuilder();
        RenderNode(node, sb, context);
        return sb.ToString();
    }

    private void RenderDocument(WikitextDocument document, StringBuilder sb, RenderContext context)
    {
        var listStack = new Stack<(string tag, int depth)>();

        foreach (var line in document.Lines)
        {
            // Close any open lists if this isn't a list item
            if (line is not ListItem)
            {
                CloseAllLists(listStack, sb);
            }

            RenderNode(line, sb, context);

            if (line is not ListItem)
            {
                sb.AppendLine();
            }
        }

        // Close any remaining open lists
        CloseAllLists(listStack, sb);
    }

    private void RenderNode(WikiNode node, StringBuilder sb, RenderContext context)
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
                sb.Append(Escape(text.Content));
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
                RenderArgumentReference(argRef, sb, context);
                break;

            case FormatSwitch format:
                RenderFormatSwitch(format, sb, context);
                break;

            case Comment comment:
                if (_options.IncludeComments)
                {
                    sb.Append("<!--").Append(comment.Content).Append("-->");
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
                    RenderNode(child, sb, context);
                }
                break;
        }
    }

    private void RenderHeading(Heading heading, StringBuilder sb, RenderContext context)
    {
        var level = Math.Clamp(heading.Level, 1, 6);
        sb.Append($"<h{level}>");
        RenderInlines(heading.Inlines, sb, context);
        sb.Append($"</h{level}>");
    }

    private void RenderParagraph(Paragraph para, StringBuilder sb, RenderContext context)
    {
        sb.Append("<p>");
        RenderInlines(para.Inlines, sb, context);
        sb.Append("</p>");
    }

    private void RenderListItem(ListItem item, StringBuilder sb, RenderContext context)
    {
        // Determine list type from prefix
        var prefix = item.Prefix ?? "*";
        var depth = prefix.Length;

        // Build the nested structure
        for (var i = 0; i < depth; i++)
        {
            var c = i < prefix.Length ? prefix[i] : prefix[^1];
            var tag = c switch
            {
                '#' => "ol",
                ';' => "dl",
                ':' when i > 0 && prefix[i - 1] == ';' => "dl", // Definition description
                _ => "ul"
            };
            sb.Append($"<{tag}>");
        }

        // Render the item content
        var itemTag = prefix[^1] == ';' ? "dt" : prefix[^1] == ':' ? "dd" : "li";
        sb.Append($"<{itemTag}>");
        RenderInlines(item.Inlines, sb, context);
        sb.Append($"</{itemTag}>");

        // Close the lists
        for (var i = depth - 1; i >= 0; i--)
        {
            var c = i < prefix.Length ? prefix[i] : prefix[^1];
            var tag = c switch
            {
                '#' => "ol",
                ';' or ':' => "dl",
                _ => "ul"
            };
            sb.Append($"</{tag}>");
        }

        sb.AppendLine();
    }

    private void RenderTable(Table table, StringBuilder sb, RenderContext context)
    {
        sb.Append("<table");
        RenderAttributes(table.Attributes, sb);
        sb.AppendLine(">");

        if (table.Caption is not null)
        {
            sb.Append("<caption>");
            if (table.Caption.Content is not null)
            {
                RenderInlines(table.Caption.Content.Inlines, sb, context);
            }
            sb.AppendLine("</caption>");
        }

        foreach (var row in table.Rows)
        {
            sb.Append("<tr");
            RenderAttributes(row.Attributes, sb);
            sb.AppendLine(">");

            foreach (var cell in row.Cells)
            {
                var tag = cell.IsHeader ? "th" : "td";
                sb.Append($"<{tag}");
                RenderAttributes(cell.Attributes, sb);
                sb.Append(">");

                if (cell.Content is not null)
                {
                    RenderInlines(cell.Content.Inlines, sb, context);
                }

                if (cell.NestedContent is not null)
                {
                    foreach (var nested in cell.NestedContent)
                    {
                        RenderNode(nested, sb, context);
                    }
                }

                sb.AppendLine($"</{tag}>");
            }

            sb.AppendLine("</tr>");
        }

        sb.Append("</table>");
    }

    private static void RenderAttributes(WikiNodeCollection<TagAttributeNode> attributes, StringBuilder sb)
    {
        foreach (var attr in attributes)
        {
            var name = attr.Name?.ToString().Trim();
            if (string.IsNullOrEmpty(name)) continue;

            // Sanitize attribute name
            if (!IsValidAttributeName(name)) continue;

            sb.Append(' ').Append(name);

            if (attr.Value is not null)
            {
                var value = attr.Value.ToString().Trim();
                sb.Append("=\"").Append(EscapeAttribute(value)).Append('"');
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
            // Handle pipe trick: [[Foo (bar)|]] -> Foo
            if (displayText.Contains('('))
            {
                displayText = displayText[..displayText.IndexOf('(')].Trim();
            }
        }

        // Build URL
        var url = context.WikiLinkBaseUrl + Uri.EscapeDataString(target.Replace(' ', '_'));

        sb.Append("<a href=\"").Append(EscapeAttribute(url)).Append("\">");
        sb.Append(Escape(displayText));
        sb.Append("</a>");
    }

    private void RenderExternalLink(ExternalLink link, StringBuilder sb, RenderContext context)
    {
        var url = link.Target?.ToString().Trim() ?? "";
        var displayText = link.Text?.ToString().Trim();

        // Validate URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "ftp" && uri.Scheme != "mailto"))
        {
            sb.Append(Escape(displayText ?? url));
            return;
        }

        sb.Append("<a href=\"").Append(EscapeAttribute(url)).Append("\"");

        if (_options.ExternalLinksInNewTab)
        {
            sb.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
        }

        sb.Append(" class=\"external\">");
        sb.Append(Escape(displayText ?? url));
        sb.Append("</a>");
    }

    private void RenderImageLink(ImageLink imageLink, StringBuilder sb, RenderContext context)
    {
        var imageInfo = _imageResolver?.Resolve(imageLink, context);

        // Parse image options
        var options = ParseImageOptions(imageLink);

        if (imageInfo is not null)
        {
            RenderResolvedImage(imageLink, imageInfo, options, sb, context);
        }
        else
        {
            RenderImagePlaceholder(imageLink, options, sb, context);
        }
    }

    private static ImageOptions ParseImageOptions(ImageLink imageLink)
    {
        var options = new ImageOptions();

        foreach (var arg in imageLink.Arguments)
        {
            var value = arg.Value?.ToString().Trim().ToLowerInvariant();
            if (value is null) continue;

            switch (value)
            {
                case "thumb":
                case "thumbnail":
                    options.IsThumbnail = true;
                    break;
                case "frame":
                    options.IsFramed = true;
                    break;
                case "frameless":
                    options.IsFrameless = true;
                    break;
                case "border":
                    options.HasBorder = true;
                    break;
                case "left":
                    options.Alignment = "left";
                    break;
                case "right":
                    options.Alignment = "right";
                    break;
                case "center":
                case "centre":
                    options.Alignment = "center";
                    break;
                case "none":
                    options.Alignment = "none";
                    break;
                default:
                    // Check for size
                    if (value.EndsWith("px"))
                    {
                        var sizeStr = value[..^2];
                        if (sizeStr.Contains('x'))
                        {
                            var parts = sizeStr.Split('x');
                            if (parts.Length == 2)
                            {
                                if (int.TryParse(parts[0], out var w)) options.Width = w;
                                if (int.TryParse(parts[1], out var h)) options.Height = h;
                            }
                        }
                        else if (int.TryParse(sizeStr, out var w))
                        {
                            options.Width = w;
                        }
                    }
                    else if (arg.Name is null)
                    {
                        // Unnamed argument that's not a keyword - it's the caption
                        options.Caption = arg.Value?.ToString();
                    }
                    else if (arg.Name.ToString().Trim().Equals("alt", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Alt = arg.Value?.ToString();
                    }
                    break;
            }
        }

        return options;
    }

    private static void RenderResolvedImage(ImageLink imageLink, ImageInfo info, ImageOptions options, StringBuilder sb, RenderContext context)
    {
        var isThumbnail = options.IsThumbnail || options.IsFramed;
        var url = options.IsThumbnail && info.ThumbnailUrl is not null ? info.ThumbnailUrl : info.Url;
        var width = options.Width ?? info.ThumbnailWidth ?? info.Width;
        var height = options.Height ?? info.ThumbnailHeight ?? info.Height;

        if (isThumbnail)
        {
            // Render as figure with caption
            var alignClass = options.Alignment is not null ? $" class=\"float-{options.Alignment}\"" : "";
            sb.Append($"<figure{alignClass}>");
        }

        sb.Append("<img src=\"").Append(EscapeAttribute(url)).Append("\"");

        var alt = options.Alt ?? options.Caption ?? ExtractFileName(imageLink);
        sb.Append(" alt=\"").Append(EscapeAttribute(alt)).Append("\"");

        if (width.HasValue)
        {
            sb.Append($" width=\"{width.Value}\"");
        }
        if (height.HasValue)
        {
            sb.Append($" height=\"{height.Value}\"");
        }
        if (options.HasBorder)
        {
            sb.Append(" class=\"border\"");
        }

        sb.Append(" />");

        if (isThumbnail && !string.IsNullOrEmpty(options.Caption))
        {
            sb.Append("<figcaption>").Append(Escape(options.Caption)).Append("</figcaption>");
        }

        if (isThumbnail)
        {
            sb.Append("</figure>");
        }
    }

    private void RenderImagePlaceholder(ImageLink imageLink, ImageOptions options, StringBuilder sb, RenderContext context)
    {
        var fileName = ExtractFileName(imageLink);
        var alt = options.Alt ?? options.Caption ?? fileName;

        if (_options.ImagePlaceholderMode == ImagePlaceholderMode.AltTextOnly)
        {
            sb.Append(Escape(alt));
        }
        else
        {
            // Render as a placeholder span
            sb.Append("<span class=\"image-placeholder\" data-file=\"");
            sb.Append(EscapeAttribute(fileName));
            sb.Append("\">[Image: ").Append(Escape(alt)).Append("]</span>");
        }
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
            sb.Append("<span class=\"template-error\">[Template recursion limit exceeded]</span>");
            return;
        }

        var resolved = _templateResolver?.Resolve(template, context);

        if (resolved is not null)
        {
            sb.Append(resolved);
        }
        else
        {
            // Render as placeholder
            var name = template.Name?.ToString().Trim() ?? "?";

            if (_options.TemplatePlaceholderMode == TemplatePlaceholderMode.Hidden)
            {
                // Don't render anything
            }
            else if (_options.TemplatePlaceholderMode == TemplatePlaceholderMode.NameOnly)
            {
                sb.Append("<span class=\"template\" data-template=\"");
                sb.Append(EscapeAttribute(name));
                sb.Append("\">{{").Append(Escape(name)).Append("}}</span>");
            }
            else
            {
                // Full - include arguments
                sb.Append("<span class=\"template\" data-template=\"");
                sb.Append(EscapeAttribute(name));
                sb.Append("\">{{").Append(Escape(name));

                foreach (var arg in template.Arguments)
                {
                    sb.Append("|");
                    if (arg.Name is not null)
                    {
                        sb.Append(Escape(arg.Name.ToString()));
                        sb.Append("=");
                    }
                    if (arg.Value is not null)
                    {
                        sb.Append(Escape(arg.Value.ToString()));
                    }
                }

                sb.Append("}}</span>");
            }
        }
    }

    private static void RenderArgumentReference(ArgumentReference argRef, StringBuilder sb, RenderContext context)
    {
        // Argument references are typically only meaningful inside templates
        // Render as placeholder
        var name = argRef.Name?.ToString().Trim() ?? "?";
        sb.Append("<span class=\"arg-ref\">{{{").Append(Escape(name));

        if (argRef.DefaultValue is not null)
        {
            sb.Append("|").Append(Escape(argRef.DefaultValue.ToString()));
        }

        sb.Append("}}}</span>");
    }

    private static void RenderFormatSwitch(FormatSwitch format, StringBuilder sb, RenderContext context)
    {
        // FormatSwitch nodes toggle bold/italic state
        // In a proper renderer, we'd track state and emit opening/closing tags
        // For simplicity, we emit the raw markers as placeholder
        if (format.SwitchBold && format.SwitchItalics)
        {
            sb.Append("'''''");
        }
        else if (format.SwitchBold)
        {
            sb.Append("'''");
        }
        else if (format.SwitchItalics)
        {
            sb.Append("''");
        }
    }

    private void RenderHtmlTag(HtmlTag tag, StringBuilder sb, RenderContext context)
    {
        var name = tag.Name.ToLowerInvariant();

        // Sanitize - only allow safe tags
        if (!_options.AllowedHtmlTags.Contains(name))
        {
            // Render content only, skip the tag
            if (tag.Content is not null)
            {
                RenderNode(tag.Content, sb, context);
            }
            return;
        }

        sb.Append('<').Append(name);
        RenderTagAttributes(tag.Attributes, sb);

        if (tag.TagStyle == TagStyle.SelfClosing || tag.TagStyle == TagStyle.CompactSelfClosing)
        {
            sb.Append(" />");
        }
        else
        {
            sb.Append('>');
            if (tag.Content is not null)
            {
                RenderNode(tag.Content, sb, context);
            }
            sb.Append("</").Append(name).Append('>');
        }
    }

    private void RenderTagAttributes(WikiNodeCollection<TagAttributeNode> attributes, StringBuilder sb)
    {
        foreach (var attr in attributes)
        {
            var name = attr.Name?.ToString().Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(name)) continue;

            // Sanitize - only allow safe attributes
            if (!IsValidAttributeName(name)) continue;
            if (_options.DisallowedAttributes.Contains(name)) continue;

            // Block event handlers
            if (name.StartsWith("on")) continue;

            sb.Append(' ').Append(name);

            if (attr.Value is not null)
            {
                var value = attr.Value.ToString();

                // Sanitize URLs in href/src
                if ((name == "href" || name == "src") && !IsSafeUrl(value))
                {
                    continue;
                }

                sb.Append("=\"").Append(EscapeAttribute(value)).Append('"');
            }
        }
    }

    private static void RenderParserTag(ParserTag tag, StringBuilder sb, RenderContext context)
    {
        var name = tag.Name.ToLowerInvariant();

        switch (name)
        {
            case "nowiki":
                // Render content as escaped text
                sb.Append(Escape(tag.Content ?? ""));
                break;

            case "ref":
                // Render as superscript reference
                sb.Append("<sup class=\"reference\">[ref]</sup>");
                break;

            case "references":
                sb.Append("<div class=\"references\"></div>");
                break;

            case "code":
            case "source":
            case "syntaxhighlight":
                sb.Append("<pre><code>");
                sb.Append(Escape(tag.Content ?? ""));
                sb.Append("</code></pre>");
                break;

            case "math":
                sb.Append("<span class=\"math\">");
                sb.Append(Escape(tag.Content ?? ""));
                sb.Append("</span>");
                break;

            case "gallery":
                sb.Append("<div class=\"gallery\">");
                sb.Append(Escape(tag.Content ?? ""));
                sb.Append("</div>");
                break;

            default:
                // Unknown parser tag - render content as-is
                if (!string.IsNullOrEmpty(tag.Content))
                {
                    sb.Append(Escape(tag.Content));
                }
                break;
        }
    }

    private void RenderInlines(WikiNodeCollection<InlineNode> inlines, StringBuilder sb, RenderContext context)
    {
        // Track bold/italic state for proper tag nesting
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
                        sb.Append("</b></i>");
                        isBold = false;
                        isItalic = false;
                    }
                    else if (isBold)
                    {
                        sb.Append("</b><i>");
                        isBold = false;
                        isItalic = true;
                    }
                    else if (isItalic)
                    {
                        sb.Append("</i><b>");
                        isItalic = false;
                        isBold = true;
                    }
                    else
                    {
                        sb.Append("<i><b>");
                        isBold = true;
                        isItalic = true;
                    }
                }
                else if (format.SwitchBold)
                {
                    if (isBold)
                    {
                        sb.Append("</b>");
                        isBold = false;
                    }
                    else
                    {
                        sb.Append("<b>");
                        isBold = true;
                    }
                }
                else if (format.SwitchItalics)
                {
                    if (isItalic)
                    {
                        sb.Append("</i>");
                        isItalic = false;
                    }
                    else
                    {
                        sb.Append("<i>");
                        isItalic = true;
                    }
                }
            }
            else
            {
                RenderNode(inline, sb, context);
            }
        }

        // Close any unclosed tags
        if (isBold) sb.Append("</b>");
        if (isItalic) sb.Append("</i>");
    }

    private static void CloseAllLists(Stack<(string tag, int depth)> listStack, StringBuilder sb)
    {
        while (listStack.Count > 0)
        {
            var (tag, _) = listStack.Pop();
            sb.Append($"</{tag}>");
        }
    }

    private static string Escape(string text)
    {
        return HttpUtility.HtmlEncode(text);
    }

    private static string EscapeAttribute(string value)
    {
        return HttpUtility.HtmlAttributeEncode(value);
    }

    private static bool IsValidAttributeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        // Basic validation - alphanumeric, hyphens, underscores
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') return false;
        }
        return true;
    }

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Block javascript: and data: URLs
        var trimmed = url.Trim().ToLowerInvariant();
        if (trimmed.StartsWith("javascript:", StringComparison.Ordinal)) return false;
        if (trimmed.StartsWith("data:", StringComparison.Ordinal)) return false;
        if (trimmed.StartsWith("vbscript:", StringComparison.Ordinal)) return false;

        return true;
    }

    private sealed class ImageOptions
    {
        public bool IsThumbnail { get; set; }
        public bool IsFramed { get; set; }
        public bool IsFrameless { get; set; }
        public bool HasBorder { get; set; }
        public string? Alignment { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Caption { get; set; }
        public string? Alt { get; set; }
    }
}

/// <summary>
/// Options for HTML rendering.
/// </summary>
public class HtmlRenderOptions
{
    /// <summary>
    /// Gets or sets whether to include HTML comments in output.
    /// </summary>
    public bool IncludeComments { get; set; }

    /// <summary>
    /// Gets or sets whether external links should open in a new tab.
    /// </summary>
    public bool ExternalLinksInNewTab { get; set; } = true;

    /// <summary>
    /// Gets or sets how unresolved templates should be rendered.
    /// </summary>
    public TemplatePlaceholderMode TemplatePlaceholderMode { get; set; } = TemplatePlaceholderMode.NameOnly;

    /// <summary>
    /// Gets or sets how unresolved images should be rendered.
    /// </summary>
    public ImagePlaceholderMode ImagePlaceholderMode { get; set; } = ImagePlaceholderMode.Placeholder;

    /// <summary>
    /// Gets the set of allowed HTML tags.
    /// </summary>
    public HashSet<string> AllowedHtmlTags { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "b", "i", "u", "s", "em", "strong", "small", "big",
        "sub", "sup", "span", "div", "p", "br", "hr", "wbr",
        "table", "tr", "td", "th", "thead", "tbody", "tfoot", "caption",
        "ul", "ol", "li", "dl", "dt", "dd",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "blockquote", "pre", "code", "kbd", "var", "samp",
        "a", "img", "abbr", "cite", "q", "dfn", "ins", "del", "mark"
    };

    /// <summary>
    /// Gets the set of disallowed attributes (applied to all tags).
    /// </summary>
    public HashSet<string> DisallowedAttributes { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "style" // Optionally allow this if you trust the source
    };
}

/// <summary>
/// Specifies how unresolved templates should be rendered.
/// </summary>
public enum TemplatePlaceholderMode
{
    /// <summary>
    /// Don't render anything for unresolved templates.
    /// </summary>
    Hidden,

    /// <summary>
    /// Render as {{TemplateName}}.
    /// </summary>
    NameOnly,

    /// <summary>
    /// Render the full template syntax including arguments.
    /// </summary>
    Full
}

/// <summary>
/// Specifies how unresolved images should be rendered.
/// </summary>
public enum ImagePlaceholderMode
{
    /// <summary>
    /// Render only the alt text.
    /// </summary>
    AltTextOnly,

    /// <summary>
    /// Render as a placeholder span with class and data attributes.
    /// </summary>
    Placeholder
}
