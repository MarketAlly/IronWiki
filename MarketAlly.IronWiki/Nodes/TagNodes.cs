// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json.Serialization;

namespace MarketAlly.IronWiki.Nodes;

/// <summary>
/// Specifies how a tag is rendered in wikitext.
/// </summary>
public enum TagStyle
{
    /// <summary>
    /// Normal tag with opening and closing tags: &lt;tag&gt;&lt;/tag&gt;
    /// </summary>
    Normal,

    /// <summary>
    /// Self-closing tag: &lt;tag /&gt;
    /// </summary>
    SelfClosing,

    /// <summary>
    /// Compact self-closing tag: &lt;tag&gt; (for tags like br, hr)
    /// </summary>
    CompactSelfClosing,

    /// <summary>
    /// Unclosed tag (unbalanced): &lt;tag&gt;...[EOF]
    /// </summary>
    NotClosed
}

/// <summary>
/// Specifies how an attribute value is quoted.
/// </summary>
public enum ValueQuoteStyle
{
    /// <summary>
    /// No quotes around the value.
    /// </summary>
    None,

    /// <summary>
    /// Single quotes around the value.
    /// </summary>
    SingleQuotes,

    /// <summary>
    /// Double quotes around the value.
    /// </summary>
    DoubleQuotes
}

/// <summary>
/// Base class for tag nodes (HTML tags and parser tags).
/// </summary>
public abstract class TagNode : InlineNode
{
    private string _closingTagTrailingWhitespace = string.Empty;
    private TagStyle _tagStyle;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagNode"/> class.
    /// </summary>
    protected TagNode()
    {
        Attributes = new WikiNodeCollection<TagAttributeNode>(this);
    }

    /// <summary>
    /// Gets or sets the tag name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the closing tag name if different from the opening tag.
    /// </summary>
    [JsonPropertyName("closingName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClosingTagName { get; set; }

    /// <summary>
    /// Gets or sets how the tag is rendered.
    /// </summary>
    [JsonPropertyName("style")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public virtual TagStyle TagStyle
    {
        get => _tagStyle;
        set
        {
            if (value is not (TagStyle.Normal or TagStyle.SelfClosing or TagStyle.CompactSelfClosing or TagStyle.NotClosed))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _tagStyle = value;
        }
    }

    /// <summary>
    /// Gets or sets the trailing whitespace in the closing tag.
    /// </summary>
    [JsonPropertyName("closingWhitespace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClosingTagTrailingWhitespace
    {
        get => string.IsNullOrEmpty(_closingTagTrailingWhitespace) ? null : _closingTagTrailingWhitespace;
        set
        {
            if (value is not null && !string.IsNullOrWhiteSpace(value) && value.Any(c => !char.IsWhiteSpace(c)))
            {
                throw new ArgumentException("Value must contain only whitespace characters.", nameof(value));
            }
            _closingTagTrailingWhitespace = value ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the collection of tag attributes.
    /// </summary>
    [JsonPropertyName("attributes")]
    public WikiNodeCollection<TagAttributeNode> Attributes { get; }

    /// <summary>
    /// Gets or sets the trailing whitespace after the last attribute.
    /// </summary>
    [JsonPropertyName("attrWhitespace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttributeTrailingWhitespace { get; set; }

    /// <summary>
    /// Builds the content portion of the tag for string representation.
    /// </summary>
    protected abstract void BuildContentString(StringBuilder builder);

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => Attributes;

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder("<");
        sb.Append(Name);
        foreach (var attr in Attributes)
        {
            sb.Append(attr);
        }
        sb.Append(AttributeTrailingWhitespace);

        switch (TagStyle)
        {
            case TagStyle.Normal:
            case TagStyle.NotClosed:
                sb.Append('>');
                BuildContentString(sb);
                break;
            case TagStyle.SelfClosing:
                sb.Append("/>");
                return sb.ToString();
            case TagStyle.CompactSelfClosing:
                sb.Append('>');
                return sb.ToString();
        }

        if (TagStyle != TagStyle.NotClosed)
        {
            sb.Append("</");
            sb.Append(ClosingTagName ?? Name);
            sb.Append(_closingTagTrailingWhitespace);
            sb.Append('>');
        }

        return sb.ToString();
    }
}

/// <summary>
/// Represents a parser extension tag (e.g., &lt;ref&gt;, &lt;nowiki&gt;).
/// </summary>
/// <remarks>
/// Parser tags have their content preserved as raw text rather than being parsed.
/// </remarks>
public sealed class ParserTag : TagNode
{
    /// <summary>
    /// Gets or sets the raw content of the tag.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    /// <inheritdoc />
    public override TagStyle TagStyle
    {
        set
        {
            if (value is TagStyle.SelfClosing or TagStyle.CompactSelfClosing)
            {
                if (!string.IsNullOrEmpty(Content))
                {
                    throw new InvalidOperationException("Cannot self-close a tag with non-empty content.");
                }
            }
            base.TagStyle = value;
        }
    }

    /// <inheritdoc />
    protected override void BuildContentString(StringBuilder builder) => builder.Append(Content);

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new ParserTag
        {
            Name = Name,
            ClosingTagName = ClosingTagName,
            Content = Content,
            ClosingTagTrailingWhitespace = ClosingTagTrailingWhitespace,
            AttributeTrailingWhitespace = AttributeTrailingWhitespace,
            TagStyle = TagStyle
        };
        clone.Attributes.AddFrom(Attributes);
        return clone;
    }
}

/// <summary>
/// Represents an HTML tag with parsed content.
/// </summary>
public sealed class HtmlTag : TagNode
{
    private WikitextDocument? _content;

    /// <summary>
    /// Gets or sets the parsed content of the tag.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WikitextDocument? Content
    {
        get => _content;
        set => AttachChild(ref _content, value);
    }

    /// <inheritdoc />
    public override TagStyle TagStyle
    {
        set
        {
            if (value is TagStyle.SelfClosing or TagStyle.CompactSelfClosing)
            {
                if (Content is not null && Content.Lines.Count > 0)
                {
                    throw new InvalidOperationException("Cannot self-close a tag with non-empty content.");
                }
            }
            base.TagStyle = value;
        }
    }

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
    protected override void BuildContentString(StringBuilder builder) => builder.Append(Content);

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new HtmlTag
        {
            Name = Name,
            ClosingTagName = ClosingTagName,
            Content = Content,
            ClosingTagTrailingWhitespace = ClosingTagTrailingWhitespace,
            AttributeTrailingWhitespace = AttributeTrailingWhitespace,
            TagStyle = TagStyle
        };
        clone.Attributes.AddFrom(Attributes);
        return clone;
    }
}

/// <summary>
/// Represents a tag attribute (name="value").
/// </summary>
public sealed class TagAttributeNode : WikiNode
{
    private string _leadingWhitespace = " ";
    private string? _whitespaceBeforeEquals;
    private string? _whitespaceAfterEquals;
    private Run? _name;
    private WikitextDocument? _value;

    /// <summary>
    /// Gets or sets the attribute name.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Name
    {
        get => _name;
        set => AttachChild(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the attribute value.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WikitextDocument? Value
    {
        get => _value;
        set => AttachChild(ref _value, value);
    }

    /// <summary>
    /// Gets or sets the quote style for the value.
    /// </summary>
    [JsonPropertyName("quote")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ValueQuoteStyle Quote { get; set; }

    /// <summary>
    /// Gets or sets the leading whitespace before the attribute.
    /// </summary>
    [JsonPropertyName("leadingWs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string LeadingWhitespace
    {
        get => _leadingWhitespace;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Leading whitespace cannot be null or empty.", nameof(value));
            }
            if (value.Any(c => !char.IsWhiteSpace(c)))
            {
                throw new ArgumentException("Value must contain only whitespace characters.", nameof(value));
            }
            _leadingWhitespace = value;
        }
    }

    /// <summary>
    /// Gets or sets the whitespace before the equals sign.
    /// </summary>
    [JsonPropertyName("wsBeforeEq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WhitespaceBeforeEquals
    {
        get => _whitespaceBeforeEquals;
        set
        {
            if (value is not null && value.Any(c => !char.IsWhiteSpace(c)))
            {
                throw new ArgumentException("Value must contain only whitespace characters.", nameof(value));
            }
            _whitespaceBeforeEquals = value;
        }
    }

    /// <summary>
    /// Gets or sets the whitespace after the equals sign.
    /// </summary>
    [JsonPropertyName("wsAfterEq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WhitespaceAfterEquals
    {
        get => _whitespaceAfterEquals;
        set
        {
            if (value is not null && value.Any(c => !char.IsWhiteSpace(c)))
            {
                throw new ArgumentException("Value must contain only whitespace characters.", nameof(value));
            }
            _whitespaceAfterEquals = value;
        }
    }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        if (_name is not null) yield return _name;
        if (_value is not null) yield return _value;
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new TagAttributeNode
    {
        Name = Name,
        Value = Value,
        Quote = Quote,
        LeadingWhitespace = LeadingWhitespace,
        WhitespaceBeforeEquals = WhitespaceBeforeEquals,
        WhitespaceAfterEquals = WhitespaceAfterEquals
    };

    /// <inheritdoc />
    public override string ToString()
    {
        var quote = Quote switch
        {
            ValueQuoteStyle.SingleQuotes => "'",
            ValueQuoteStyle.DoubleQuotes => "\"",
            _ => null
        };

        var sb = new StringBuilder();
        sb.Append(LeadingWhitespace);
        sb.Append(Name);
        sb.Append(WhitespaceBeforeEquals);
        sb.Append('=');
        sb.Append(WhitespaceAfterEquals);
        sb.Append(quote);
        sb.Append(Value);
        sb.Append(quote);
        return sb.ToString();
    }
}
