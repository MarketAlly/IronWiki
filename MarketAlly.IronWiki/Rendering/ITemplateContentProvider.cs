// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace MarketAlly.IronWiki.Rendering;

/// <summary>
/// Provides raw wikitext content for templates.
/// </summary>
/// <remarks>
/// <para>Unlike <see cref="ITemplateResolver"/> which returns already-rendered content,
/// this interface returns the raw wikitext source of a template, allowing the
/// <see cref="TemplateExpander"/> to perform full parameter substitution and recursive expansion.</para>
/// <para>Implement this interface to fetch template content from MediaWiki APIs, databases,
/// local files, or other sources.</para>
/// </remarks>
/// <example>
/// <code>
/// // Simple dictionary-based provider
/// var provider = new DictionaryTemplateContentProvider();
/// provider.Add("Infobox", "{| class=\"infobox\"\n| {{{title|No title}}}\n|}");
///
/// // Use with TemplateExpander
/// var expander = new TemplateExpander(parser, provider);
/// </code>
/// </example>
public interface ITemplateContentProvider
{
    /// <summary>
    /// Gets the raw wikitext content of a template.
    /// </summary>
    /// <param name="templateName">The template name (without "Template:" prefix).</param>
    /// <returns>
    /// The raw wikitext content of the template, or <c>null</c> if the template is not found.
    /// </returns>
    string? GetContent(string templateName);

    /// <summary>
    /// Asynchronously gets the raw wikitext content of a template.
    /// </summary>
    /// <param name="templateName">The template name (without "Template:" prefix).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The raw wikitext content of the template, or <c>null</c> if the template is not found.
    /// </returns>
    Task<string?> GetContentAsync(string templateName, CancellationToken cancellationToken = default)
    {
        // Default implementation calls sync method
        return Task.FromResult(GetContent(templateName));
    }
}

/// <summary>
/// A dictionary-based template content provider for testing and simple use cases.
/// </summary>
public class DictionaryTemplateContentProvider : ITemplateContentProvider
{
    private readonly Dictionary<string, string> _templates;

    /// <summary>
    /// Initializes a new instance with an empty dictionary.
    /// </summary>
    /// <param name="ignoreCase">Whether template names should be case-insensitive.</param>
    public DictionaryTemplateContentProvider(bool ignoreCase = true)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _templates = new Dictionary<string, string>(comparer);
    }

    /// <summary>
    /// Initializes a new instance with the specified templates.
    /// </summary>
    /// <param name="templates">The templates to include (name → wikitext content).</param>
    /// <param name="ignoreCase">Whether template names should be case-insensitive.</param>
    public DictionaryTemplateContentProvider(IDictionary<string, string> templates, bool ignoreCase = true)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _templates = new Dictionary<string, string>(templates, comparer);
    }

    /// <summary>
    /// Adds or updates a template.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <param name="content">The raw wikitext content.</param>
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
    public string? GetContent(string templateName)
    {
        return _templates.GetValueOrDefault(templateName);
    }
}

/// <summary>
/// Chains multiple template content providers, trying each in order until one succeeds.
/// </summary>
public class ChainedTemplateContentProvider : ITemplateContentProvider
{
    private readonly ITemplateContentProvider[] _providers;

    /// <summary>
    /// Initializes a new instance with the specified providers.
    /// </summary>
    /// <param name="providers">The providers to chain, in priority order.</param>
    public ChainedTemplateContentProvider(params ITemplateContentProvider[] providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <inheritdoc />
    public string? GetContent(string templateName)
    {
        foreach (var provider in _providers)
        {
            var content = provider.GetContent(templateName);
            if (content is not null)
            {
                return content;
            }
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<string?> GetContentAsync(string templateName, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            var content = await provider.GetContentAsync(templateName, cancellationToken).ConfigureAwait(false);
            if (content is not null)
            {
                return content;
            }
        }
        return null;
    }
}
