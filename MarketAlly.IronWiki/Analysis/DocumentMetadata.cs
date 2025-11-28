// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#pragma warning disable CA1002 // Do not expose generic lists - using List<T> for simpler API
#pragma warning disable CA1056 // URI properties should not be strings - URL as string is appropriate here

using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Analysis;

/// <summary>
/// Contains metadata extracted from a parsed wikitext document.
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// Gets or sets whether this document is a redirect.
    /// </summary>
    public bool IsRedirect { get; set; }

    /// <summary>
    /// Gets or sets redirect information if this is a redirect page.
    /// </summary>
    public RedirectInfo? Redirect { get; set; }

    /// <summary>
    /// Gets the list of categories the document belongs to.
    /// </summary>
    public List<CategoryInfo> Categories { get; } = [];

    /// <summary>
    /// Gets the list of sections in the document.
    /// </summary>
    public List<SectionInfo> Sections { get; } = [];

    /// <summary>
    /// Gets the table of contents built from sections.
    /// </summary>
    public TableOfContents? TableOfContents { get; set; }

    /// <summary>
    /// Gets the list of references/footnotes.
    /// </summary>
    public List<ReferenceInfo> References { get; } = [];

    /// <summary>
    /// Gets or sets whether the document contains a references section.
    /// </summary>
    public bool HasReferencesSection { get; set; }

    /// <summary>
    /// Gets the list of internal wiki links.
    /// </summary>
    public List<InternalLinkInfo> InternalLinks { get; } = [];

    /// <summary>
    /// Gets the list of external links.
    /// </summary>
    public List<ExternalLinkInfo> ExternalLinks { get; } = [];

    /// <summary>
    /// Gets the list of images and media files.
    /// </summary>
    public List<ImageInfo> Images { get; } = [];

    /// <summary>
    /// Gets the list of templates used.
    /// </summary>
    public List<TemplateInfo> Templates { get; } = [];

    /// <summary>
    /// Gets the list of language links (links to same article in other languages).
    /// </summary>
    public List<LanguageLinkInfo> LanguageLinks { get; } = [];

    /// <summary>
    /// Gets the list of interwiki links.
    /// </summary>
    public List<InterwikiLinkInfo> InterwikiLinks { get; } = [];

    /// <summary>
    /// Gets all unique category names.
    /// </summary>
    public IEnumerable<string> CategoryNames => Categories.Select(c => c.Name).Distinct();

    /// <summary>
    /// Gets all unique template names.
    /// </summary>
    public IEnumerable<string> TemplateNames => Templates.Select(t => t.Name).Distinct();

    /// <summary>
    /// Gets all unique image file names.
    /// </summary>
    public IEnumerable<string> ImageFileNames => Images.Select(i => i.FileName).Distinct();

    /// <summary>
    /// Gets all unique internal link targets.
    /// </summary>
    public IEnumerable<string> LinkedArticles =>
        InternalLinks.Select(l => l.Title).Where(t => !string.IsNullOrEmpty(t)).Distinct();
}

/// <summary>
/// Information about a redirect.
/// </summary>
public class RedirectInfo
{
    /// <summary>
    /// Gets or sets the redirect target page.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Gets or sets the source wiki link node.
    /// </summary>
    public WikiLink? SourceNode { get; init; }
}

/// <summary>
/// Information about a category.
/// </summary>
public class CategoryInfo
{
    /// <summary>
    /// Gets or sets the category name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the sort key for this page in the category.
    /// </summary>
    public string? SortKey { get; init; }

    /// <summary>
    /// Gets or sets the source wiki link node.
    /// </summary>
    public WikiLink? SourceNode { get; init; }
}

/// <summary>
/// Information about a document section.
/// </summary>
public class SectionInfo
{
    /// <summary>
    /// Gets or sets the section title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the heading level (2-6).
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    /// Gets or sets the anchor ID for this section.
    /// </summary>
    public required string Anchor { get; init; }

    /// <summary>
    /// Gets or sets the section index (0-based).
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets or sets the source heading node.
    /// </summary>
    public Heading? SourceNode { get; init; }
}

/// <summary>
/// Table of contents for a document.
/// </summary>
public class TableOfContents
{
    /// <summary>
    /// Gets the top-level TOC entries.
    /// </summary>
    public List<TocEntry> Entries { get; } = [];

    /// <summary>
    /// Gets all entries as a flat list.
    /// </summary>
    public IEnumerable<TocEntry> GetFlatList()
    {
        foreach (var entry in Entries)
        {
            yield return entry;
            foreach (var child in GetFlatListRecursive(entry))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<TocEntry> GetFlatListRecursive(TocEntry entry)
    {
        foreach (var child in entry.Children)
        {
            yield return child;
            foreach (var grandchild in GetFlatListRecursive(child))
            {
                yield return grandchild;
            }
        }
    }
}

/// <summary>
/// A table of contents entry.
/// </summary>
public class TocEntry
{
    /// <summary>
    /// Gets or sets the section title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the anchor ID.
    /// </summary>
    public required string Anchor { get; init; }

    /// <summary>
    /// Gets or sets the heading level.
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    /// Gets or sets the section index.
    /// </summary>
    public int SectionIndex { get; init; }

    /// <summary>
    /// Gets the child entries.
    /// </summary>
    public List<TocEntry> Children { get; } = [];
}

/// <summary>
/// Information about a reference/footnote.
/// </summary>
public class ReferenceInfo
{
    /// <summary>
    /// Gets or sets the reference content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets or sets the reference name (for named references).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the reference group.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Gets or sets the reference number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets whether this is a back-reference to a named reference.
    /// </summary>
    public bool IsBackReference { get; set; }

    /// <summary>
    /// Gets or sets the original reference this refers to (for back-references).
    /// </summary>
    public ReferenceInfo? ReferencedBy { get; set; }

    /// <summary>
    /// Gets the list of back-references to this reference.
    /// </summary>
    public List<ReferenceInfo> BackReferences { get; } = [];

    /// <summary>
    /// Gets or sets the source parser tag node.
    /// </summary>
    public ParserTag? SourceNode { get; init; }

    /// <summary>
    /// Gets or sets the section this reference appears in.
    /// </summary>
    public SectionInfo? Section { get; init; }
}

/// <summary>
/// Information about an internal wiki link.
/// </summary>
public class InternalLinkInfo
{
    /// <summary>
    /// Gets or sets the full link target.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Gets or sets the namespace (empty for main namespace).
    /// </summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the anchor/section within the target page.
    /// </summary>
    public string? Anchor { get; init; }

    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string? DisplayText { get; init; }

    /// <summary>
    /// Gets or sets the source wiki link node.
    /// </summary>
    public WikiLink? SourceNode { get; init; }

    /// <summary>
    /// Gets or sets the section this link appears in.
    /// </summary>
    public SectionInfo? Section { get; init; }
}

/// <summary>
/// Information about an external link.
/// </summary>
public class ExternalLinkInfo
{
    /// <summary>
    /// Gets or sets the URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string? DisplayText { get; init; }

    /// <summary>
    /// Gets or sets whether the link has brackets.
    /// </summary>
    public bool HasBrackets { get; init; }

    /// <summary>
    /// Gets or sets the source external link node.
    /// </summary>
    public ExternalLink? SourceNode { get; init; }

    /// <summary>
    /// Gets or sets the section this link appears in.
    /// </summary>
    public SectionInfo? Section { get; init; }
}

/// <summary>
/// Information about an image or media file.
/// </summary>
public class ImageInfo
{
    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets or sets the full target string.
    /// </summary>
    public string? FullTarget { get; init; }

    /// <summary>
    /// Gets or sets the image width.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets the image height.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets the alignment.
    /// </summary>
    public string? Alignment { get; set; }

    /// <summary>
    /// Gets or sets the frame type.
    /// </summary>
    public string? Frame { get; set; }

    /// <summary>
    /// Gets or sets the caption.
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Gets or sets the alt text.
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Gets or sets the link target (overrides default).
    /// </summary>
    public string? LinkTarget { get; set; }

    /// <summary>
    /// Gets or sets the CSS class.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Gets or sets whether the image has a border.
    /// </summary>
    public bool HasBorder { get; set; }

    /// <summary>
    /// Gets or sets whether upright scaling is enabled.
    /// </summary>
    public bool Upright { get; set; }

    /// <summary>
    /// Gets or sets whether this is a gallery image.
    /// </summary>
    public bool IsGalleryImage { get; set; }

    /// <summary>
    /// Gets or sets the source image link node.
    /// </summary>
    public ImageLink? SourceNode { get; init; }

    /// <summary>
    /// Gets or sets the section this image appears in.
    /// </summary>
    public SectionInfo? Section { get; init; }
}

/// <summary>
/// Information about a template usage.
/// </summary>
public class TemplateInfo
{
    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets whether this is a magic word/parser function.
    /// </summary>
    public bool IsMagicWord { get; init; }

    /// <summary>
    /// Gets the template arguments.
    /// </summary>
    public Dictionary<string, string> Arguments { get; } = [];

    /// <summary>
    /// Gets or sets the source template node.
    /// </summary>
    public Template? SourceNode { get; init; }

    /// <summary>
    /// Gets or sets the section this template appears in.
    /// </summary>
    public SectionInfo? Section { get; init; }
}

/// <summary>
/// Information about a language link.
/// </summary>
public class LanguageLinkInfo
{
    /// <summary>
    /// Gets or sets the language code.
    /// </summary>
    public required string LanguageCode { get; init; }

    /// <summary>
    /// Gets or sets the page title in that language.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the source wiki link node.
    /// </summary>
    public WikiLink? SourceNode { get; init; }
}

/// <summary>
/// Information about an interwiki link.
/// </summary>
public class InterwikiLinkInfo
{
    /// <summary>
    /// Gets or sets the interwiki prefix.
    /// </summary>
    public required string Prefix { get; init; }

    /// <summary>
    /// Gets or sets the page title on the target wiki.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the source wiki link node.
    /// </summary>
    public WikiLink? SourceNode { get; init; }
}
