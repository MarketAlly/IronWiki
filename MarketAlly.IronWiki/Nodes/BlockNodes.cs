// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json.Serialization;

namespace MarketAlly.IronWiki.Nodes;

/// <summary>
/// Base class for line-level (block) nodes in the AST.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Paragraph), "paragraph")]
[JsonDerivedType(typeof(Heading), "heading")]
[JsonDerivedType(typeof(ListItem), "listItem")]
[JsonDerivedType(typeof(HorizontalRule), "horizontalRule")]
[JsonDerivedType(typeof(Table), "table")]
public abstract class BlockNode : WikiNode
{
}

/// <summary>
/// Represents the root node of a wikitext document, containing all lines/blocks.
/// </summary>
public sealed class WikitextDocument : WikiNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WikitextDocument"/> class.
    /// </summary>
    public WikitextDocument()
    {
        Lines = new WikiNodeCollection<BlockNode>(this);
    }

    /// <summary>
    /// Gets the collection of lines (block-level nodes) in this document.
    /// </summary>
    [JsonPropertyName("lines")]
    public WikiNodeCollection<BlockNode> Lines { get; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => Lines;

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new WikitextDocument();
        clone.Lines.AddFrom(Lines);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        var isFirst = true;
        foreach (var line in Lines)
        {
            if (!isFirst)
            {
                sb.Append('\n');
            }
            isFirst = false;
            sb.Append(line);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Represents a paragraph containing inline content.
/// </summary>
public sealed class Paragraph : BlockNode, IInlineContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Paragraph"/> class.
    /// </summary>
    public Paragraph()
    {
        Inlines = new WikiNodeCollection<InlineNode>(this);
    }

    /// <summary>
    /// Gets the collection of inline nodes in this paragraph.
    /// </summary>
    [JsonPropertyName("inlines")]
    public WikiNodeCollection<InlineNode> Inlines { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this paragraph is compact (not followed by a blank line).
    /// </summary>
    [JsonPropertyName("compact")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Compact { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => Inlines;

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new Paragraph { Compact = Compact };
        clone.Inlines.AddFrom(Inlines);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var inline in Inlines)
        {
            sb.Append(inline);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Appends text content with source span information.
    /// </summary>
    internal void AppendWithSourceSpan(string content, int startLine, int startCol, int endLine, int endCol)
    {
        if (Inlines.LastNode is PlainText lastText)
        {
            lastText.Content += content;
            lastText.ExtendSourceSpan(endLine, endCol);
        }
        else
        {
            var text = new PlainText(content);
            text.SetSourceSpan(startLine, startCol, endLine, endCol);
            Inlines.Add(text);
        }
        ExtendSourceSpan(endLine, endCol);
    }
}

/// <summary>
/// Represents a heading (== Heading ==).
/// </summary>
public sealed class Heading : BlockNode, IInlineContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Heading"/> class.
    /// </summary>
    public Heading()
    {
        Inlines = new WikiNodeCollection<InlineNode>(this);
    }

    /// <summary>
    /// Gets or sets the heading level (1-6).
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; set; }

    /// <summary>
    /// Gets the collection of inline nodes in this heading.
    /// </summary>
    [JsonPropertyName("inlines")]
    public WikiNodeCollection<InlineNode> Inlines { get; }

    /// <summary>
    /// Gets or sets the suffix content after the closing equals signs.
    /// </summary>
    [JsonPropertyName("suffix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Suffix { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        foreach (var inline in Inlines)
        {
            yield return inline;
        }
        if (Suffix is not null)
        {
            yield return Suffix;
        }
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new Heading { Level = Level, Suffix = Suffix };
        clone.Inlines.AddFrom(Inlines);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var equals = new string('=', Level);
        var sb = new StringBuilder();
        sb.Append(equals);
        foreach (var inline in Inlines)
        {
            sb.Append(inline);
        }
        sb.Append(equals);
        if (Suffix is not null)
        {
            sb.Append(Suffix);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Represents a list item (* item, # item, : item, ; item).
/// </summary>
public sealed class ListItem : BlockNode, IInlineContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListItem"/> class.
    /// </summary>
    public ListItem()
    {
        Inlines = new WikiNodeCollection<InlineNode>(this);
    }

    /// <summary>
    /// Gets or sets the list prefix (*, #, :, ;, or combinations).
    /// </summary>
    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets the collection of inline nodes in this list item.
    /// </summary>
    [JsonPropertyName("inlines")]
    public WikiNodeCollection<InlineNode> Inlines { get; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => Inlines;

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new ListItem { Prefix = Prefix };
        clone.Inlines.AddFrom(Inlines);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Prefix);
        foreach (var inline in Inlines)
        {
            sb.Append(inline);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Represents a horizontal rule (----).
/// </summary>
public sealed class HorizontalRule : BlockNode
{
    /// <summary>
    /// Gets or sets the number of dashes in the rule.
    /// </summary>
    [JsonPropertyName("dashes")]
    public int DashCount { get; set; } = 4;

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => [];

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new HorizontalRule { DashCount = DashCount };

    /// <inheritdoc />
    public override string ToString() => new('-', DashCount);
}
