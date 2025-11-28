// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using MarketAlly.IronWiki.Nodes;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace MarketAlly.IronWiki.Rendering;

/// <summary>
/// Resolves templates to their rendered output.
/// </summary>
/// <remarks>
/// <para>Implement this interface to provide template expansion during rendering.
/// Templates can be resolved from various sources: databases, APIs, local files, or bundled content.</para>
/// <para>If no resolver is provided to a renderer, templates will be rendered as placeholders.</para>
/// </remarks>
/// <example>
/// <code>
/// // Chain multiple providers with fallback
/// var resolver = new ChainedTemplateResolver(
///     new MemoryCacheTemplateResolver(cache),
///     new DatabaseTemplateResolver(db),
///     new MediaWikiApiTemplateResolver(httpClient, "https://en.wikipedia.org/w/api.php")
/// );
///
/// var renderer = new HtmlRenderer(templateResolver: resolver);
/// </code>
/// </example>
public interface ITemplateResolver
{
    /// <summary>
    /// Resolves a template to its rendered content.
    /// </summary>
    /// <param name="template">The template node to resolve.</param>
    /// <param name="context">The rendering context.</param>
    /// <returns>
    /// The resolved content as a string, or <c>null</c> if the template cannot be resolved
    /// (in which case the renderer will use a placeholder).
    /// </returns>
    string? Resolve(Template template, RenderContext context);

    /// <summary>
    /// Asynchronously resolves a template to its rendered content.
    /// </summary>
    /// <param name="template">The template node to resolve.</param>
    /// <param name="context">The rendering context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The resolved content as a string, or <c>null</c> if the template cannot be resolved.
    /// </returns>
    Task<string?> ResolveAsync(Template template, RenderContext context, CancellationToken cancellationToken = default)
    {
        // Default implementation calls sync method
        return Task.FromResult(Resolve(template, context));
    }
}

/// <summary>
/// Chains multiple template resolvers, trying each in order until one succeeds.
/// </summary>
public class ChainedTemplateResolver : ITemplateResolver
{
    private readonly ITemplateResolver[] _resolvers;

    /// <summary>
    /// Initializes a new instance with the specified resolvers.
    /// </summary>
    /// <param name="resolvers">The resolvers to chain, in priority order.</param>
    public ChainedTemplateResolver(params ITemplateResolver[] resolvers)
    {
        _resolvers = resolvers ?? throw new ArgumentNullException(nameof(resolvers));
    }

    /// <inheritdoc />
    public string? Resolve(Template template, RenderContext context)
    {
        foreach (var resolver in _resolvers)
        {
            var result = resolver.Resolve(template, context);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(Template template, RenderContext context, CancellationToken cancellationToken = default)
    {
        foreach (var resolver in _resolvers)
        {
            var result = await resolver.ResolveAsync(template, context, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }
}

/// <summary>
/// A simple dictionary-based template resolver for bundled/known templates.
/// Returns pre-rendered content without expansion.
/// </summary>
/// <remarks>
/// For full template expansion with parameter substitution and recursion,
/// use <see cref="ExpandingTemplateResolver"/> instead.
/// </remarks>
public class DictionaryTemplateResolver : ITemplateResolver
{
    private readonly Dictionary<string, string> _templates;

    /// <summary>
    /// Initializes a new instance with an empty dictionary.
    /// </summary>
    /// <param name="ignoreCase">Whether template names should be case-insensitive.</param>
    public DictionaryTemplateResolver(bool ignoreCase = true)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _templates = new Dictionary<string, string>(comparer);
    }

    /// <summary>
    /// Initializes a new instance with the specified templates.
    /// </summary>
    /// <param name="templates">The templates to include.</param>
    /// <param name="ignoreCase">Whether template names should be case-insensitive.</param>
    public DictionaryTemplateResolver(IDictionary<string, string> templates, bool ignoreCase = true)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _templates = new Dictionary<string, string>(templates, comparer);
    }

    /// <summary>
    /// Adds or updates a template.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <param name="content">The template content.</param>
    public void Add(string name, string content)
    {
        _templates[name] = content;
    }

    /// <summary>
    /// Removes a template.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <returns><c>true</c> if the template was removed; otherwise, <c>false</c>.</returns>
    public bool Remove(string name) => _templates.Remove(name);

    /// <inheritdoc />
    public string? Resolve(Template template, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(template);
        var name = template.Name?.ToString().Trim();
        if (name is null)
        {
            return null;
        }

        return _templates.GetValueOrDefault(name);
    }
}

/// <summary>
/// A template resolver that performs full MediaWiki-style template expansion
/// with parameter substitution and recursive template processing.
/// </summary>
/// <remarks>
/// <para>This resolver uses a <see cref="TemplateExpander"/> to:</para>
/// <list type="bullet">
/// <item>Fetch template wikitext content from an <see cref="ITemplateContentProvider"/></item>
/// <item>Substitute parameter references ({{{1}}}, {{{name}}}, {{{arg|default}}})</item>
/// <item>Recursively expand nested templates</item>
/// <item>Handle parser functions (#if, #switch, etc.)</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var parser = new WikitextParser();
/// var provider = new DictionaryTemplateContentProvider();
/// provider.Add("Greeting", "Hello, {{{1|World}}}!");
/// provider.Add("Formal", "Dear {{{name}}}, {{Greeting|{{{name}}}}}");
///
/// var resolver = new ExpandingTemplateResolver(parser, provider);
/// var renderer = new HtmlRenderer(templateResolver: resolver);
///
/// var doc = parser.Parse("{{Formal|name=Alice}}");
/// var html = renderer.Render(doc);
/// // Output contains: "Dear Alice, Hello, Alice!"
/// </code>
/// </example>
public class ExpandingTemplateResolver : ITemplateResolver
{
    private readonly TemplateExpander _expander;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpandingTemplateResolver"/> class.
    /// </summary>
    /// <param name="expander">The template expander to use.</param>
    public ExpandingTemplateResolver(TemplateExpander expander)
    {
        _expander = expander ?? throw new ArgumentNullException(nameof(expander));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpandingTemplateResolver"/> class.
    /// </summary>
    /// <param name="parser">The parser to use for parsing template content.</param>
    /// <param name="contentProvider">The provider for template content.</param>
    /// <param name="options">Optional expansion options.</param>
    public ExpandingTemplateResolver(
        Parsing.WikitextParser parser,
        ITemplateContentProvider contentProvider,
        TemplateExpanderOptions? options = null)
    {
        _expander = new TemplateExpander(parser, contentProvider, options);
    }

    /// <inheritdoc />
    public string? Resolve(Template template, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(template);

        // Create a minimal document containing just the template
        var doc = new WikitextDocument();
        var para = new Paragraph { Compact = true };
        para.Inlines.Add((Template)template.Clone());
        doc.Lines.Add(para);

        // Create expansion context with render context info
        var expansionContext = new TemplateExpansionContext();

        // Expand and return the result
        var result = _expander.Expand(doc, expansionContext);

        // Trim trailing newlines that may have been added
        return result.TrimEnd('\r', '\n');
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(Template template, RenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        // Create a minimal document containing just the template
        var doc = new WikitextDocument();
        var para = new Paragraph { Compact = true };
        para.Inlines.Add((Template)template.Clone());
        doc.Lines.Add(para);

        // Create expansion context
        var expansionContext = new TemplateExpansionContext();

        // Expand and return the result
        var result = await _expander.ExpandAsync(doc, expansionContext, cancellationToken).ConfigureAwait(false);

        // Trim trailing newlines
        return result.TrimEnd('\r', '\n');
    }
}
