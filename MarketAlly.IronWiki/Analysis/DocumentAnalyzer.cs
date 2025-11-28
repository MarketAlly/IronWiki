// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable CA1307 // Specify StringComparison for clarity - using ordinal comparison
#pragma warning disable CA1308 // Normalize strings to uppercase - lowercase is correct for anchors
#pragma warning disable CA1310 // Specify StringComparison for correctness
#pragma warning disable CA1822 // Mark members as static - keeping instance methods for extensibility

using System.Text;
using System.Text.RegularExpressions;
using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Analysis;

/// <summary>
/// Analyzes a parsed wikitext document to extract metadata, categories, references, and structure.
/// </summary>
/// <remarks>
/// <para>The DocumentAnalyzer performs a single pass over a parsed document to extract:</para>
/// <list type="bullet">
/// <item>Categories and their sort keys</item>
/// <item>Sections with headings, anchors, and hierarchy</item>
/// <item>References and footnotes</item>
/// <item>Internal and external links</item>
/// <item>Images and media files</item>
/// <item>Templates used</item>
/// <item>Interwiki and language links</item>
/// <item>Redirect target (if any)</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var parser = new WikitextParser();
/// var doc = parser.Parse(wikitext);
/// var analyzer = new DocumentAnalyzer();
/// var metadata = analyzer.Analyze(doc);
///
/// foreach (var category in metadata.Categories)
/// {
///     Console.WriteLine($"Category: {category.Name} (sort: {category.SortKey})");
/// }
/// </code>
/// </example>
public partial class DocumentAnalyzer
{
    private readonly DocumentAnalyzerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentAnalyzer"/> class.
    /// </summary>
    /// <param name="options">Optional analyzer options.</param>
    public DocumentAnalyzer(DocumentAnalyzerOptions? options = null)
    {
        _options = options ?? new DocumentAnalyzerOptions();
    }

    /// <summary>
    /// Analyzes a document and extracts metadata.
    /// </summary>
    /// <param name="document">The document to analyze.</param>
    /// <returns>The extracted document metadata.</returns>
    public DocumentMetadata Analyze(WikitextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = new DocumentMetadata();
        var context = new AnalysisContext(metadata, _options);

        // Check for redirect at the start
        CheckForRedirect(document, context);

        // Analyze all nodes
        AnalyzeNode(document, context);

        // Build table of contents from sections
        BuildTableOfContents(metadata);

        // Finalize reference numbering
        FinalizeReferences(metadata);

        return metadata;
    }

    private void CheckForRedirect(WikitextDocument document, AnalysisContext context)
    {
        if (document.Lines.Count == 0) return;

        var firstLine = document.Lines[0];
        IEnumerable<InlineNode>? inlines = null;

        // #REDIRECT is parsed as a ListItem because # is the list prefix
        // Check for ListItem with REDIRECT text
        if (firstLine is ListItem listItem)
        {
            var firstContent = GetPlainTextContent(listItem).TrimStart();
            if (firstContent.StartsWith("REDIRECT", StringComparison.OrdinalIgnoreCase))
            {
                inlines = listItem.Inlines;
            }
        }
        // Also check for Paragraph in case of different parsing
        else if (firstLine is Paragraph para && para.Inlines.Count > 0)
        {
            var firstContent = GetPlainTextContent(para).TrimStart();
            if (firstContent.StartsWith("#REDIRECT", StringComparison.OrdinalIgnoreCase))
            {
                inlines = para.Inlines;
            }
        }

        // Find the wiki link that follows REDIRECT
        if (inlines != null)
        {
            foreach (var inline in inlines)
            {
                if (inline is WikiLink link)
                {
                    context.Metadata.Redirect = new RedirectInfo
                    {
                        Target = link.Target?.ToString().Trim() ?? string.Empty,
                        SourceNode = link
                    };
                    context.Metadata.IsRedirect = true;
                    break;
                }
            }
        }
    }

    private void AnalyzeNode(WikiNode node, AnalysisContext context)
    {
        switch (node)
        {
            case WikitextDocument doc:
                foreach (var line in doc.Lines)
                {
                    AnalyzeNode(line, context);
                }
                break;

            case Heading heading:
                AnalyzeHeading(heading, context);
                break;

            case Paragraph para:
                foreach (var inline in para.Inlines)
                {
                    AnalyzeNode(inline, context);
                }
                break;

            case ListItem listItem:
                foreach (var inline in listItem.Inlines)
                {
                    AnalyzeNode(inline, context);
                }
                break;

            case Table table:
                AnalyzeTable(table, context);
                break;

            case WikiLink link:
                AnalyzeWikiLink(link, context);
                break;

            case ExternalLink extLink:
                AnalyzeExternalLink(extLink, context);
                break;

            case ImageLink imageLink:
                AnalyzeImageLink(imageLink, context);
                break;

            case Template template:
                AnalyzeTemplate(template, context);
                break;

            case ParserTag parserTag:
                AnalyzeParserTag(parserTag, context);
                break;

            case HtmlTag htmlTag:
                if (htmlTag.Content is not null)
                {
                    AnalyzeNode(htmlTag.Content, context);
                }
                break;

            case Run run:
                foreach (var inline in run.Inlines)
                {
                    AnalyzeNode(inline, context);
                }
                break;
        }
    }

    private void AnalyzeHeading(Heading heading, AnalysisContext context)
    {
        var title = GetPlainTextContent(heading).Trim();
        var anchor = GenerateAnchor(title, context);

        var section = new SectionInfo
        {
            Title = title,
            Level = heading.Level,
            Anchor = anchor,
            SourceNode = heading,
            Index = context.Metadata.Sections.Count
        };

        context.Metadata.Sections.Add(section);
        context.CurrentSection = section;

        // Analyze heading content for any links/templates
        foreach (var inline in heading.Inlines)
        {
            AnalyzeNode(inline, context);
        }
    }

    private void AnalyzeWikiLink(WikiLink link, AnalysisContext context)
    {
        var target = link.Target?.ToString().Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(target)) return;

        // Parse namespace and title
        var (ns, title, anchor) = ParseLinkTarget(target);

        // Check for category
        if (IsCategoryNamespace(ns))
        {
            var sortKey = link.Text?.ToString().Trim();
            context.Metadata.Categories.Add(new CategoryInfo
            {
                Name = title,
                SortKey = sortKey,
                SourceNode = link
            });
            return;
        }

        // Check for interwiki/language link
        if (IsLanguageCode(ns) && _options.RecognizeLanguageLinks)
        {
            context.Metadata.LanguageLinks.Add(new LanguageLinkInfo
            {
                LanguageCode = ns.ToLowerInvariant(),
                Title = title,
                SourceNode = link
            });
            return;
        }

        if (IsInterwikiPrefix(ns) && _options.RecognizeInterwikiLinks)
        {
            context.Metadata.InterwikiLinks.Add(new InterwikiLinkInfo
            {
                Prefix = ns.ToLowerInvariant(),
                Title = title,
                SourceNode = link
            });
            return;
        }

        // Regular internal link
        var displayText = link.Text?.ToString().Trim() ?? title;
        context.Metadata.InternalLinks.Add(new InternalLinkInfo
        {
            Target = target,
            Namespace = ns,
            Title = title,
            Anchor = anchor,
            DisplayText = displayText,
            SourceNode = link,
            Section = context.CurrentSection
        });
    }

    private void AnalyzeExternalLink(ExternalLink link, AnalysisContext context)
    {
        var url = link.Target?.ToString().Trim() ?? string.Empty;
        var text = link.Text?.ToString().Trim();

        context.Metadata.ExternalLinks.Add(new ExternalLinkInfo
        {
            Url = url,
            DisplayText = text,
            HasBrackets = link.HasBrackets,
            SourceNode = link,
            Section = context.CurrentSection
        });
    }

    private void AnalyzeImageLink(ImageLink link, AnalysisContext context)
    {
        var target = link.Target?.ToString().Trim() ?? string.Empty;

        // Extract file name (remove namespace prefix)
        var colonIndex = target.IndexOf(':', StringComparison.Ordinal);
        var fileName = colonIndex >= 0 ? target[(colonIndex + 1)..].Trim() : target;

        var imageInfo = new ImageInfo
        {
            FileName = fileName,
            FullTarget = target,
            SourceNode = link,
            Section = context.CurrentSection
        };

        // Parse arguments
        foreach (var arg in link.Arguments)
        {
            var name = arg.Name?.ToString().Trim()?.ToLowerInvariant();
            var value = arg.Value?.ToString().Trim() ?? string.Empty;

            if (name is null)
            {
                // Positional argument - could be size, alignment, or caption
                if (TryParseSize(value, out var width, out var height))
                {
                    imageInfo.Width = width;
                    imageInfo.Height = height;
                }
                else if (IsAlignmentValue(value))
                {
                    imageInfo.Alignment = value.ToLowerInvariant();
                }
                else if (IsFrameValue(value))
                {
                    imageInfo.Frame = value.ToLowerInvariant();
                }
                else
                {
                    // Assume caption
                    imageInfo.Caption = value;
                }
            }
            else
            {
                switch (name)
                {
                    case "alt":
                        imageInfo.AltText = value;
                        break;
                    case "link":
                        imageInfo.LinkTarget = value;
                        break;
                    case "class":
                        imageInfo.CssClass = value;
                        break;
                    case "border":
                        imageInfo.HasBorder = true;
                        break;
                    case "upright":
                        imageInfo.Upright = true;
                        break;
                }
            }
        }

        context.Metadata.Images.Add(imageInfo);
    }

    private void AnalyzeTemplate(Template template, AnalysisContext context)
    {
        var name = template.Name?.ToString().Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return;

        var templateInfo = new TemplateInfo
        {
            Name = name,
            IsMagicWord = template.IsMagicWord,
            SourceNode = template,
            Section = context.CurrentSection
        };

        // Extract arguments
        var positionalIndex = 1;
        foreach (var arg in template.Arguments)
        {
            var argName = arg.Name?.ToString().Trim();
            var argValue = arg.Value?.ToString().Trim() ?? string.Empty;

            if (argName is null)
            {
                templateInfo.Arguments[positionalIndex.ToString()] = argValue;
                positionalIndex++;
            }
            else
            {
                templateInfo.Arguments[argName] = argValue;
            }
        }

        context.Metadata.Templates.Add(templateInfo);

        // Recursively analyze template arguments
        foreach (var arg in template.Arguments)
        {
            AnalyzeNode(arg.Value, context);
            if (arg.Name is not null)
            {
                AnalyzeNode(arg.Name, context);
            }
        }
    }

    private void AnalyzeParserTag(ParserTag tag, AnalysisContext context)
    {
        var tagName = tag.Name?.ToLowerInvariant() ?? string.Empty;

        switch (tagName)
        {
            case "ref":
                AnalyzeReference(tag, context);
                break;
            case "references":
                context.Metadata.HasReferencesSection = true;
                break;
            case "nowiki":
            case "pre":
            case "code":
            case "source":
            case "syntaxhighlight":
                // These are code/preformatted blocks - no further analysis needed
                break;
            case "gallery":
                AnalyzeGallery(tag, context);
                break;
        }
    }

    private void AnalyzeReference(ParserTag tag, AnalysisContext context)
    {
        var refInfo = new ReferenceInfo
        {
            Content = tag.Content ?? string.Empty,
            SourceNode = tag,
            Section = context.CurrentSection
        };

        // Parse attributes for name and group
        foreach (var attr in tag.Attributes)
        {
            var attrName = attr.Name?.ToString().Trim()?.ToLowerInvariant();
            var attrValue = attr.Value?.ToString().Trim() ?? string.Empty;

            switch (attrName)
            {
                case "name":
                    refInfo.Name = attrValue;
                    break;
                case "group":
                    refInfo.Group = attrValue;
                    break;
            }
        }

        // Check if this is a reference to an existing named reference
        if (string.IsNullOrEmpty(refInfo.Content) && !string.IsNullOrEmpty(refInfo.Name))
        {
            refInfo.IsBackReference = true;
        }

        context.Metadata.References.Add(refInfo);
    }

    private void AnalyzeGallery(ParserTag tag, AnalysisContext context)
    {
        if (string.IsNullOrEmpty(tag.Content)) return;

        // Parse gallery content - each line is an image
        var lines = tag.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("<!--")) continue;

            // Format: File:Name.jpg|Caption
            var pipeIndex = trimmedLine.IndexOf('|');
            var fileName = pipeIndex >= 0 ? trimmedLine[..pipeIndex].Trim() : trimmedLine;
            var caption = pipeIndex >= 0 ? trimmedLine[(pipeIndex + 1)..].Trim() : null;

            // Remove File: prefix if present
            var colonIndex = fileName.IndexOf(':');
            if (colonIndex >= 0)
            {
                fileName = fileName[(colonIndex + 1)..].Trim();
            }

            context.Metadata.Images.Add(new ImageInfo
            {
                FileName = fileName,
                FullTarget = trimmedLine,
                Caption = caption,
                IsGalleryImage = true,
                Section = context.CurrentSection
            });
        }
    }

    private void AnalyzeTable(Table table, AnalysisContext context)
    {
        // Analyze table caption and cells for nested content
        if (table.Caption?.Content is not null)
        {
            AnalyzeNode(table.Caption.Content, context);
        }

        foreach (var row in table.Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.Content is not null)
                {
                    AnalyzeNode(cell.Content, context);
                }
            }
        }
    }

    private string GenerateAnchor(string title, AnalysisContext context)
    {
        // Generate URL-safe anchor
        var anchor = AnchorRegex().Replace(title, "_");
        anchor = anchor.Trim('_');

        // Handle duplicates
        var baseAnchor = anchor;
        var count = 1;
        while (context.UsedAnchors.Contains(anchor))
        {
            anchor = $"{baseAnchor}_{count}";
            count++;
        }
        context.UsedAnchors.Add(anchor);

        return anchor;
    }

    private static string GetPlainTextContent(WikiNode node)
    {
        var sb = new StringBuilder();
        GetPlainTextContentCore(node, sb);
        return sb.ToString();
    }

    private static void GetPlainTextContentCore(WikiNode node, StringBuilder sb)
    {
        switch (node)
        {
            case PlainText text:
                sb.Append(text.Content);
                break;
            case WikiLink link:
                if (link.Text is not null)
                {
                    GetPlainTextContentCore(link.Text, sb);
                }
                else if (link.Target is not null)
                {
                    GetPlainTextContentCore(link.Target, sb);
                }
                break;
            default:
                foreach (var child in node.EnumerateChildren())
                {
                    GetPlainTextContentCore(child, sb);
                }
                break;
        }
    }

    private static (string Namespace, string Title, string? Anchor) ParseLinkTarget(string target)
    {
        string? anchor = null;
        var anchorIndex = target.IndexOf('#');
        if (anchorIndex >= 0)
        {
            anchor = target[(anchorIndex + 1)..];
            target = target[..anchorIndex];
        }

        var colonIndex = target.IndexOf(':');
        if (colonIndex >= 0)
        {
            var ns = target[..colonIndex].Trim();
            var title = target[(colonIndex + 1)..].Trim();
            return (ns, title, anchor);
        }

        return (string.Empty, target, anchor);
    }

    private bool IsCategoryNamespace(string ns)
    {
        return _options.CategoryNamespaces.Contains(ns, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsLanguageCode(string ns)
    {
        // Check if it's a 2-3 letter language code
        if (ns.Length < 2 || ns.Length > 3) return false;
        return _options.LanguageCodes.Contains(ns, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsInterwikiPrefix(string ns)
    {
        return _options.InterwikiPrefixes.Contains(ns, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryParseSize(string value, out int? width, out int? height)
    {
        width = null;
        height = null;

        if (!value.EndsWith("px", StringComparison.OrdinalIgnoreCase)) return false;

        var sizeStr = value[..^2];
        if (sizeStr.Contains('x', StringComparison.OrdinalIgnoreCase))
        {
            var parts = sizeStr.Split('x', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out var w)) width = w;
                if (int.TryParse(parts[1], out var h)) height = h;
                return width.HasValue || height.HasValue;
            }
            if (parts.Length == 1)
            {
                // Format: x200px means height only
                if (sizeStr.StartsWith('x') && int.TryParse(parts[0], out var h))
                {
                    height = h;
                    return true;
                }
            }
        }
        else if (int.TryParse(sizeStr, out var w))
        {
            width = w;
            return true;
        }

        return false;
    }

    private static bool IsAlignmentValue(string value)
    {
        return value.Equals("left", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("right", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("center", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("none", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFrameValue(string value)
    {
        return value.Equals("thumb", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("thumbnail", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("frame", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("framed", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("frameless", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("border", StringComparison.OrdinalIgnoreCase);
    }

    private static void BuildTableOfContents(DocumentMetadata metadata)
    {
        if (metadata.Sections.Count == 0) return;

        var toc = new TableOfContents();
        var stack = new Stack<TocEntry>();

        foreach (var section in metadata.Sections)
        {
            var entry = new TocEntry
            {
                Title = section.Title,
                Anchor = section.Anchor,
                Level = section.Level,
                SectionIndex = section.Index
            };

            // Find parent
            while (stack.Count > 0 && stack.Peek().Level >= section.Level)
            {
                stack.Pop();
            }

            if (stack.Count > 0)
            {
                stack.Peek().Children.Add(entry);
            }
            else
            {
                toc.Entries.Add(entry);
            }

            stack.Push(entry);
        }

        metadata.TableOfContents = toc;
    }

    private static void FinalizeReferences(DocumentMetadata metadata)
    {
        var namedRefs = new Dictionary<string, ReferenceInfo>(StringComparer.OrdinalIgnoreCase);
        var number = 1;

        foreach (var reference in metadata.References)
        {
            if (!string.IsNullOrEmpty(reference.Name))
            {
                if (reference.IsBackReference)
                {
                    // Find the original reference
                    if (namedRefs.TryGetValue(reference.Name, out var original))
                    {
                        reference.Number = original.Number;
                        reference.ReferencedBy = original;
                        original.BackReferences.Add(reference);
                    }
                }
                else
                {
                    reference.Number = number++;
                    namedRefs[reference.Name] = reference;
                }
            }
            else
            {
                reference.Number = number++;
            }
        }
    }

    [GeneratedRegex(@"[^\w\-]")]
    private static partial Regex AnchorRegex();

    private sealed class AnalysisContext
    {
        public DocumentMetadata Metadata { get; }
        public DocumentAnalyzerOptions Options { get; }
        public SectionInfo? CurrentSection { get; set; }
        public HashSet<string> UsedAnchors { get; } = new(StringComparer.OrdinalIgnoreCase);

        public AnalysisContext(DocumentMetadata metadata, DocumentAnalyzerOptions options)
        {
            Metadata = metadata;
            Options = options;
        }
    }
}

/// <summary>
/// Options for document analysis.
/// </summary>
public class DocumentAnalyzerOptions
{
    /// <summary>
    /// Gets or sets the category namespace names.
    /// </summary>
    public IReadOnlyList<string> CategoryNamespaces { get; set; } = ["Category", "Cat"];

    /// <summary>
    /// Gets or sets known language codes for language link detection.
    /// </summary>
    public IReadOnlyList<string> LanguageCodes { get; set; } =
    [
        "en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "ar", "hi", "pl", "nl",
        "sv", "uk", "vi", "fa", "he", "id", "tr", "cs", "ro", "hu", "fi", "da", "no", "el",
        "th", "bg", "ca", "sr", "hr", "sk", "lt", "sl", "et", "lv", "ms", "simple"
    ];

    /// <summary>
    /// Gets or sets known interwiki prefixes.
    /// </summary>
    public IReadOnlyList<string> InterwikiPrefixes { get; set; } =
    [
        "wikipedia", "wiktionary", "wikiquote", "wikibooks", "wikisource", "wikinews",
        "wikiversity", "wikivoyage", "wikidata", "wikimedia", "commons", "meta", "mw",
        "mediawikiwiki", "species", "incubator", "phabricator", "bugzilla"
    ];

    /// <summary>
    /// Gets or sets whether to recognize language links.
    /// </summary>
    public bool RecognizeLanguageLinks { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to recognize interwiki links.
    /// </summary>
    public bool RecognizeInterwikiLinks { get; set; } = true;
}
