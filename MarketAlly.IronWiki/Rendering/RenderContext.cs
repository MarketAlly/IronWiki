// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#pragma warning disable CA1056 // URI properties should not be strings

namespace MarketAlly.IronWiki.Rendering;

/// <summary>
/// Provides context information during rendering operations.
/// </summary>
public class RenderContext
{
    /// <summary>
    /// Gets or sets the title of the current page being rendered.
    /// </summary>
    public string? PageTitle { get; set; }

    /// <summary>
    /// Gets or sets the namespace of the current page.
    /// </summary>
    public string? PageNamespace { get; set; }

    /// <summary>
    /// Gets or sets the base URL for wiki links.
    /// </summary>
    /// <remarks>
    /// Example: "/wiki/" for URLs like "/wiki/Article_Name"
    /// </remarks>
    public string WikiLinkBaseUrl { get; set; } = "/wiki/";

    /// <summary>
    /// Gets or sets the base URL for image description pages.
    /// </summary>
    public string ImageDescriptionBaseUrl { get; set; } = "/wiki/File:";

    /// <summary>
    /// Gets or sets the current recursion depth for template expansion.
    /// </summary>
    public int RecursionDepth { get; set; }

    /// <summary>
    /// Gets or sets the maximum recursion depth for template expansion.
    /// </summary>
    public int MaxRecursionDepth { get; set; } = 100;

    /// <summary>
    /// Gets a value indicating whether the maximum recursion depth has been exceeded.
    /// </summary>
    public bool IsRecursionLimitExceeded => RecursionDepth >= MaxRecursionDepth;

    /// <summary>
    /// Gets or sets custom data associated with this render context.
    /// </summary>
    public IDictionary<string, object?> Data { get; } = new Dictionary<string, object?>();

    /// <summary>
    /// Creates a child context for nested rendering with incremented recursion depth.
    /// </summary>
    /// <returns>A new context with incremented recursion depth.</returns>
    public RenderContext CreateChildContext()
    {
        return new RenderContext
        {
            PageTitle = PageTitle,
            PageNamespace = PageNamespace,
            WikiLinkBaseUrl = WikiLinkBaseUrl,
            ImageDescriptionBaseUrl = ImageDescriptionBaseUrl,
            RecursionDepth = RecursionDepth + 1,
            MaxRecursionDepth = MaxRecursionDepth
        };
    }
}
