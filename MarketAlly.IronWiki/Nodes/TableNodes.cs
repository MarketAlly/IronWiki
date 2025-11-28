// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json.Serialization;

namespace MarketAlly.IronWiki.Nodes;

/// <summary>
/// Represents a wiki table ({| ... |}).
/// </summary>
public sealed class Table : BlockNode
{
    private TableCaption? _caption;

    /// <summary>
    /// Initializes a new instance of the <see cref="Table"/> class.
    /// </summary>
    public Table()
    {
        Attributes = new WikiNodeCollection<TagAttributeNode>(this);
        Rows = new WikiNodeCollection<TableRow>(this);
    }

    /// <summary>
    /// Gets the collection of table attributes.
    /// </summary>
    [JsonPropertyName("attributes")]
    public WikiNodeCollection<TagAttributeNode> Attributes { get; }

    /// <summary>
    /// Gets or sets the table caption.
    /// </summary>
    [JsonPropertyName("caption")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TableCaption? Caption
    {
        get => _caption;
        set => AttachChild(ref _caption, value);
    }

    /// <summary>
    /// Gets the collection of table rows.
    /// </summary>
    [JsonPropertyName("rows")]
    public WikiNodeCollection<TableRow> Rows { get; }

    /// <summary>
    /// Gets or sets the trailing whitespace after the last attribute.
    /// </summary>
    [JsonPropertyName("attrWhitespace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttributeTrailingWhitespace { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        foreach (var attr in Attributes)
        {
            yield return attr;
        }
        if (_caption is not null)
        {
            yield return _caption;
        }
        foreach (var row in Rows)
        {
            yield return row;
        }
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new Table
        {
            Caption = Caption,
            AttributeTrailingWhitespace = AttributeTrailingWhitespace
        };
        clone.Attributes.AddFrom(Attributes);
        clone.Rows.AddFrom(Rows);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("{|");
        foreach (var attr in Attributes)
        {
            sb.Append(attr);
        }
        sb.Append(AttributeTrailingWhitespace);
        sb.AppendLine();

        if (_caption is not null)
        {
            sb.AppendLine(_caption.ToString());
        }

        foreach (var row in Rows)
        {
            sb.AppendLine(row.ToString());
        }

        sb.Append("|}");
        return sb.ToString();
    }
}

/// <summary>
/// Represents a table caption (|+ caption).
/// </summary>
public sealed class TableCaption : WikiNode
{
    private Run? _content;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableCaption"/> class.
    /// </summary>
    public TableCaption()
    {
        Attributes = new WikiNodeCollection<TagAttributeNode>(this);
    }

    /// <summary>
    /// Gets the collection of caption attributes.
    /// </summary>
    [JsonPropertyName("attributes")]
    public WikiNodeCollection<TagAttributeNode> Attributes { get; }

    /// <summary>
    /// Gets or sets the caption content.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Content
    {
        get => _content;
        set => AttachChild(ref _content, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether there is an attribute pipe separator.
    /// </summary>
    [JsonPropertyName("hasAttrPipe")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasAttributePipe { get; set; }

    /// <summary>
    /// Gets or sets the trailing whitespace after the last attribute.
    /// </summary>
    [JsonPropertyName("attrWhitespace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttributeTrailingWhitespace { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        foreach (var attr in Attributes)
        {
            yield return attr;
        }
        if (_content is not null)
        {
            yield return _content;
        }
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new TableCaption
        {
            Content = Content,
            HasAttributePipe = HasAttributePipe,
            AttributeTrailingWhitespace = AttributeTrailingWhitespace
        };
        clone.Attributes.AddFrom(Attributes);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder("|+");
        foreach (var attr in Attributes)
        {
            sb.Append(attr);
        }
        sb.Append(AttributeTrailingWhitespace);
        if (HasAttributePipe)
        {
            sb.Append('|');
        }
        sb.Append(Content);
        return sb.ToString();
    }
}

/// <summary>
/// Represents a table row (|- ... ).
/// </summary>
public sealed class TableRow : WikiNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableRow"/> class.
    /// </summary>
    public TableRow()
    {
        Attributes = new WikiNodeCollection<TagAttributeNode>(this);
        Cells = new WikiNodeCollection<TableCell>(this);
    }

    /// <summary>
    /// Gets the collection of row attributes.
    /// </summary>
    [JsonPropertyName("attributes")]
    public WikiNodeCollection<TagAttributeNode> Attributes { get; }

    /// <summary>
    /// Gets the collection of cells in this row.
    /// </summary>
    [JsonPropertyName("cells")]
    public WikiNodeCollection<TableCell> Cells { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this row has an explicit row marker (|-).
    /// </summary>
    /// <remarks>
    /// The first row in a table may not have an explicit row marker.
    /// </remarks>
    [JsonPropertyName("hasRowMarker")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasExplicitRowMarker { get; set; } = true;

    /// <summary>
    /// Gets or sets the trailing whitespace after the last attribute.
    /// </summary>
    [JsonPropertyName("attrWhitespace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttributeTrailingWhitespace { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        foreach (var attr in Attributes)
        {
            yield return attr;
        }
        foreach (var cell in Cells)
        {
            yield return cell;
        }
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new TableRow
        {
            HasExplicitRowMarker = HasExplicitRowMarker,
            AttributeTrailingWhitespace = AttributeTrailingWhitespace
        };
        clone.Attributes.AddFrom(Attributes);
        clone.Cells.AddFrom(Cells);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (HasExplicitRowMarker)
        {
            sb.Append("|-");
            foreach (var attr in Attributes)
            {
                sb.Append(attr);
            }
            sb.Append(AttributeTrailingWhitespace);
            sb.Append('\n');
        }

        var isFirst = true;
        foreach (var cell in Cells)
        {
            if (!isFirst && !cell.IsInlineSibling)
            {
                sb.Append('\n');
            }
            isFirst = false;
            sb.Append(cell);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Represents a table cell (| cell or ! header cell).
/// </summary>
public sealed class TableCell : WikiNode
{
    private Run? _content;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableCell"/> class.
    /// </summary>
    public TableCell()
    {
        Attributes = new WikiNodeCollection<TagAttributeNode>(this);
    }

    /// <summary>
    /// Gets the collection of cell attributes.
    /// </summary>
    [JsonPropertyName("attributes")]
    public WikiNodeCollection<TagAttributeNode> Attributes { get; }

    /// <summary>
    /// Gets or sets the cell content.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Content
    {
        get => _content;
        set => AttachChild(ref _content, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this is a header cell (! instead of |).
    /// </summary>
    [JsonPropertyName("isHeader")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsHeader { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is an attribute pipe separator.
    /// </summary>
    [JsonPropertyName("hasAttrPipe")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasAttributePipe { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this cell is on the same line as the previous cell (|| or !!).
    /// </summary>
    [JsonPropertyName("inline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsInlineSibling { get; set; }

    /// <summary>
    /// Gets or sets the trailing whitespace after the last attribute.
    /// </summary>
    [JsonPropertyName("attrWhitespace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttributeTrailingWhitespace { get; set; }

    /// <summary>
    /// Gets the nested content within the cell (e.g., nested tables).
    /// </summary>
    [JsonPropertyName("nested")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WikiNodeCollection<WikiNode>? NestedContent { get; internal set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        foreach (var attr in Attributes)
        {
            yield return attr;
        }
        if (_content is not null)
        {
            yield return _content;
        }
        if (NestedContent is not null)
        {
            foreach (var nested in NestedContent)
            {
                yield return nested;
            }
        }
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new TableCell
        {
            Content = Content,
            IsHeader = IsHeader,
            HasAttributePipe = HasAttributePipe,
            IsInlineSibling = IsInlineSibling,
            AttributeTrailingWhitespace = AttributeTrailingWhitespace
        };
        clone.Attributes.AddFrom(Attributes);
        if (NestedContent is not null)
        {
            clone.NestedContent = new WikiNodeCollection<WikiNode>(clone);
            clone.NestedContent.AddFrom(NestedContent);
        }
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        var marker = IsHeader ? '!' : '|';

        if (IsInlineSibling)
        {
            sb.Append(marker);
            sb.Append(marker);
        }
        else if (HasAttributePipe)
        {
            sb.Append(marker);
            foreach (var attr in Attributes)
            {
                sb.Append(attr);
            }
            sb.Append(AttributeTrailingWhitespace);
            sb.Append('|');
        }
        else
        {
            sb.Append(marker);
        }

        sb.Append(Content);
        return sb.ToString();
    }
}
