// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json.Serialization;

namespace MarketAlly.IronWiki.Nodes;

/// <summary>
/// Interface for nodes that can contain inline content.
/// </summary>
public interface IInlineContainer
{
    /// <summary>
    /// Gets the collection of inline nodes.
    /// </summary>
    WikiNodeCollection<InlineNode> Inlines { get; }
}

/// <summary>
/// Base class for inline nodes that appear within block-level elements.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PlainText), "plainText")]
[JsonDerivedType(typeof(WikiLink), "wikiLink")]
[JsonDerivedType(typeof(ExternalLink), "externalLink")]
[JsonDerivedType(typeof(ImageLink), "imageLink")]
[JsonDerivedType(typeof(Template), "template")]
[JsonDerivedType(typeof(ArgumentReference), "argumentReference")]
[JsonDerivedType(typeof(FormatSwitch), "formatSwitch")]
[JsonDerivedType(typeof(Comment), "comment")]
[JsonDerivedType(typeof(HtmlTag), "htmlTag")]
[JsonDerivedType(typeof(ParserTag), "parserTag")]
public abstract class InlineNode : WikiNode
{
}

/// <summary>
/// Represents a run of inline content (a container for inline nodes).
/// </summary>
public sealed class Run : WikiNode, IInlineContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Run"/> class.
    /// </summary>
    public Run()
    {
        Inlines = new WikiNodeCollection<InlineNode>(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Run"/> class with initial content.
    /// </summary>
    /// <param name="node">The initial inline node.</param>
    public Run(InlineNode node) : this()
    {
        Inlines.Add(node);
    }

    /// <summary>
    /// Gets the collection of inline nodes in this run.
    /// </summary>
    [JsonPropertyName("inlines")]
    public WikiNodeCollection<InlineNode> Inlines { get; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => Inlines;

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new Run();
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
}

/// <summary>
/// Represents plain text content.
/// </summary>
public sealed class PlainText : InlineNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlainText"/> class.
    /// </summary>
    public PlainText() : this(string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlainText"/> class with content.
    /// </summary>
    /// <param name="content">The text content.</param>
    public PlainText(string content)
    {
        Content = content;
    }

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => [];

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new PlainText(Content);

    /// <inheritdoc />
    public override string ToString() => Content;
}

/// <summary>
/// Represents a wiki link ([[Target|Text]]).
/// </summary>
public sealed class WikiLink : InlineNode
{
    private Run? _target;
    private Run? _text;

    /// <summary>
    /// Gets or sets the link target.
    /// </summary>
    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Target
    {
        get => _target;
        set => AttachChild(ref _target, value);
    }

    /// <summary>
    /// Gets or sets the display text, or <c>null</c> if no pipe is present.
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Text
    {
        get => _text;
        set => AttachChild(ref _text, value);
    }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        if (_target is not null) yield return _target;
        if (_text is not null) yield return _text;
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new WikiLink { Target = Target, Text = Text };

    /// <inheritdoc />
    public override string ToString()
    {
        return Text is null ? $"[[{Target}]]" : $"[[{Target}|{Text}]]";
    }
}

/// <summary>
/// Represents an external link ([URL Text] or bare URL).
/// </summary>
public sealed class ExternalLink : InlineNode
{
    private Run? _target;
    private Run? _text;

    /// <summary>
    /// Gets or sets the link target URL.
    /// </summary>
    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Target
    {
        get => _target;
        set => AttachChild(ref _target, value);
    }

    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Text
    {
        get => _text;
        set => AttachChild(ref _text, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the link is enclosed in square brackets.
    /// </summary>
    [JsonPropertyName("brackets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasBrackets { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        if (_target is not null) yield return _target;
        if (_text is not null) yield return _text;
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new ExternalLink
    {
        Target = Target,
        Text = Text,
        HasBrackets = HasBrackets
    };

    /// <inheritdoc />
    public override string ToString()
    {
        var result = Target?.ToString() ?? string.Empty;
        if (Text is not null)
        {
            result += " " + Text;
        }
        return HasBrackets ? $"[{result}]" : result;
    }
}

/// <summary>
/// Represents an image/file link ([[File:Image.png|options|caption]]).
/// </summary>
public sealed class ImageLink : InlineNode
{
    private Run _target = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageLink"/> class.
    /// </summary>
    public ImageLink()
    {
        Arguments = new WikiNodeCollection<ImageLinkArgument>(this);
    }

    /// <summary>
    /// Gets or sets the image file target.
    /// </summary>
    [JsonPropertyName("target")]
    public Run Target
    {
        get => _target;
        set => AttachRequiredChild(ref _target, value);
    }

    /// <summary>
    /// Gets the collection of image link arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public WikiNodeCollection<ImageLinkArgument> Arguments { get; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        yield return _target;
        foreach (var arg in Arguments)
        {
            yield return arg;
        }
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new ImageLink { Target = Target };
        clone.Arguments.AddFrom(Arguments);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder("[[");
        sb.Append(_target);
        foreach (var arg in Arguments)
        {
            sb.Append('|');
            if (arg.Name is not null)
            {
                sb.Append(arg.Name);
                sb.Append('=');
            }
            sb.Append(arg.Value);
        }
        sb.Append("]]");
        return sb.ToString();
    }
}

/// <summary>
/// Represents an argument in an image link.
/// </summary>
public sealed class ImageLinkArgument : WikiNode
{
    private WikitextDocument? _name;
    private WikitextDocument _value = null!;

    /// <summary>
    /// Gets or sets the argument name, or <c>null</c> for anonymous arguments.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WikitextDocument? Name
    {
        get => _name;
        set => AttachChild(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the argument value.
    /// </summary>
    [JsonPropertyName("value")]
    public WikitextDocument Value
    {
        get => _value;
        set => AttachRequiredChild(ref _value, value);
    }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        if (_name is not null) yield return _name;
        yield return _value;
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new ImageLinkArgument { Name = Name, Value = Value };

    /// <inheritdoc />
    public override string ToString()
    {
        return Name is null ? Value.ToString() : $"{Name}={Value}";
    }
}

/// <summary>
/// Represents a template transclusion ({{Template|arg1|arg2}}).
/// </summary>
public sealed class Template : InlineNode
{
    private Run? _name;

    /// <summary>
    /// Initializes a new instance of the <see cref="Template"/> class.
    /// </summary>
    public Template()
    {
        Arguments = new WikiNodeCollection<TemplateArgument>(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Template"/> class with a name.
    /// </summary>
    /// <param name="name">The template name.</param>
    public Template(Run? name) : this()
    {
        _name = name;
        if (name is not null)
        {
            name.Parent = this;
        }
    }

    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Run? Name
    {
        get => _name;
        set => AttachChild(ref _name, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this is a magic word (variable or parser function).
    /// </summary>
    [JsonPropertyName("isMagicWord")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsMagicWord { get; set; }

    /// <summary>
    /// Gets the collection of template arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public WikiNodeCollection<TemplateArgument> Arguments { get; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        if (_name is not null) yield return _name;
        foreach (var arg in Arguments)
        {
            yield return arg;
        }
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore()
    {
        var clone = new Template { Name = Name, IsMagicWord = IsMagicWord };
        clone.Arguments.AddFrom(Arguments);
        return clone;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Arguments.Count == 0)
        {
            return $"{{{{{Name}}}}}";
        }

        var sb = new StringBuilder("{{");
        sb.Append(Name);
        var isFirst = true;
        foreach (var arg in Arguments)
        {
            sb.Append(isFirst && IsMagicWord ? ':' : '|');
            isFirst = false;
            sb.Append(arg);
        }
        sb.Append("}}");
        return sb.ToString();
    }
}

/// <summary>
/// Represents a template argument.
/// </summary>
public sealed class TemplateArgument : WikiNode
{
    private WikitextDocument? _name;
    private WikitextDocument _value = null!;

    /// <summary>
    /// Gets or sets the argument name, or <c>null</c> for anonymous arguments.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WikitextDocument? Name
    {
        get => _name;
        set => AttachChild(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the argument value.
    /// </summary>
    [JsonPropertyName("value")]
    public WikitextDocument Value
    {
        get => _value;
        set => AttachRequiredChild(ref _value, value);
    }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        if (_name is not null) yield return _name;
        yield return _value;
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new TemplateArgument { Name = Name, Value = Value };

    /// <inheritdoc />
    public override string ToString()
    {
        return Name is null ? Value.ToString() : $"{Name}={Value}";
    }
}

/// <summary>
/// Represents a template argument reference ({{{arg|default}}}).
/// </summary>
public sealed class ArgumentReference : InlineNode
{
    private WikitextDocument _name = null!;
    private WikitextDocument? _defaultValue;

    /// <summary>
    /// Gets or sets the argument name.
    /// </summary>
    [JsonPropertyName("name")]
    public WikitextDocument Name
    {
        get => _name;
        set => AttachRequiredChild(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the default value if the argument is not provided.
    /// </summary>
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WikitextDocument? DefaultValue
    {
        get => _defaultValue;
        set => AttachChild(ref _defaultValue, value);
    }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren()
    {
        yield return _name;
        if (_defaultValue is not null) yield return _defaultValue;
    }

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new ArgumentReference
    {
        Name = Name,
        DefaultValue = DefaultValue
    };

    /// <inheritdoc />
    public override string ToString()
    {
        return DefaultValue is null ? $"{{{{{{{Name}}}}}}}" : $"{{{{{{{Name}|{DefaultValue}}}}}}}";
    }
}

/// <summary>
/// Represents a format switch for bold/italic ('' or ''').
/// </summary>
public sealed class FormatSwitch : InlineNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormatSwitch"/> class.
    /// </summary>
    public FormatSwitch()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatSwitch"/> class.
    /// </summary>
    /// <param name="switchBold">Whether to toggle bold.</param>
    /// <param name="switchItalics">Whether to toggle italics.</param>
    public FormatSwitch(bool switchBold, bool switchItalics)
    {
        SwitchBold = switchBold;
        SwitchItalics = switchItalics;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to toggle bold formatting.
    /// </summary>
    [JsonPropertyName("bold")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SwitchBold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to toggle italic formatting.
    /// </summary>
    [JsonPropertyName("italic")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SwitchItalics { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => [];

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new FormatSwitch(SwitchBold, SwitchItalics);

    /// <inheritdoc />
    public override string ToString()
    {
        return (SwitchBold, SwitchItalics) switch
        {
            (true, true) => "'''''",
            (true, false) => "'''",
            (false, true) => "''",
            _ => string.Empty
        };
    }
}

/// <summary>
/// Represents an HTML comment (&lt;!-- comment --&gt;).
/// </summary>
public sealed class Comment : InlineNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Comment"/> class.
    /// </summary>
    public Comment() : this(string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Comment"/> class with content.
    /// </summary>
    /// <param name="content">The comment content.</param>
    public Comment(string content)
    {
        Content = content;
    }

    /// <summary>
    /// Gets or sets the comment content.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }

    /// <inheritdoc />
    public override IEnumerable<WikiNode> EnumerateChildren() => [];

    /// <inheritdoc />
    protected override WikiNode CloneCore() => new Comment(Content);

    /// <inheritdoc />
    public override string ToString() => $"<!--{Content}-->";
}
