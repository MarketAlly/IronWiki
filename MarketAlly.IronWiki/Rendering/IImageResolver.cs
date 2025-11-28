// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using MarketAlly.IronWiki.Nodes;

#pragma warning disable CA1054 // URI parameters should not be strings
#pragma warning disable CA1056 // URI properties should not be strings

namespace MarketAlly.IronWiki.Rendering;

/// <summary>
/// Resolves image links to their URLs and metadata.
/// </summary>
/// <remarks>
/// <para>Implement this interface to provide image URL resolution during rendering.
/// Images can be resolved from MediaWiki APIs, local storage, CDNs, or other sources.</para>
/// <para>If no resolver is provided to a renderer, images will be rendered as placeholders or alt text.</para>
/// </remarks>
public interface IImageResolver
{
    /// <summary>
    /// Resolves an image link to its display information.
    /// </summary>
    /// <param name="imageLink">The image link node to resolve.</param>
    /// <param name="context">The rendering context.</param>
    /// <returns>
    /// The resolved image information, or <c>null</c> if the image cannot be resolved
    /// (in which case the renderer will use alt text or a placeholder).
    /// </returns>
    ImageInfo? Resolve(ImageLink imageLink, RenderContext context);

    /// <summary>
    /// Asynchronously resolves an image link to its display information.
    /// </summary>
    /// <param name="imageLink">The image link node to resolve.</param>
    /// <param name="context">The rendering context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The resolved image information, or <c>null</c> if the image cannot be resolved.
    /// </returns>
    Task<ImageInfo?> ResolveAsync(ImageLink imageLink, RenderContext context, CancellationToken cancellationToken = default)
    {
        // Default implementation calls sync method
        return Task.FromResult(Resolve(imageLink, context));
    }
}

/// <summary>
/// Contains resolved image information for rendering.
/// </summary>
public sealed class ImageInfo
{
    /// <summary>
    /// Gets or sets the URL to the image file.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets or sets the display width in pixels, if specified.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Gets or sets the display height in pixels, if specified.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Gets or sets the URL to the image description page.
    /// </summary>
    public string? DescriptionUrl { get; init; }

    /// <summary>
    /// Gets or sets the URL to a thumbnail version of the image.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Gets or sets the thumbnail width in pixels.
    /// </summary>
    public int? ThumbnailWidth { get; init; }

    /// <summary>
    /// Gets or sets the thumbnail height in pixels.
    /// </summary>
    public int? ThumbnailHeight { get; init; }

    /// <summary>
    /// Gets or sets the MIME type of the image.
    /// </summary>
    public string? MimeType { get; init; }
}

/// <summary>
/// Chains multiple image resolvers, trying each in order until one succeeds.
/// </summary>
public class ChainedImageResolver : IImageResolver
{
    private readonly IImageResolver[] _resolvers;

    /// <summary>
    /// Initializes a new instance with the specified resolvers.
    /// </summary>
    /// <param name="resolvers">The resolvers to chain, in priority order.</param>
    public ChainedImageResolver(params IImageResolver[] resolvers)
    {
        _resolvers = resolvers ?? throw new ArgumentNullException(nameof(resolvers));
    }

    /// <inheritdoc />
    public ImageInfo? Resolve(ImageLink imageLink, RenderContext context)
    {
        foreach (var resolver in _resolvers)
        {
            var result = resolver.Resolve(imageLink, context);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<ImageInfo?> ResolveAsync(ImageLink imageLink, RenderContext context, CancellationToken cancellationToken = default)
    {
        foreach (var resolver in _resolvers)
        {
            var result = await resolver.ResolveAsync(imageLink, context, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }
}

/// <summary>
/// A simple URL-pattern-based image resolver that constructs URLs from file names.
/// </summary>
/// <remarks>
/// This is useful when images are stored in a predictable location, such as a CDN or local folder.
/// </remarks>
/// <example>
/// <code>
/// // Resolve images to a local folder
/// var resolver = new UrlPatternImageResolver("/images/{0}");
///
/// // Resolve images to Wikimedia Commons (simplified - real URLs are more complex)
/// var resolver = new UrlPatternImageResolver("https://upload.wikimedia.org/wikipedia/commons/{0}");
/// </code>
/// </example>
public class UrlPatternImageResolver : IImageResolver
{
    private readonly string _urlPattern;

    /// <summary>
    /// Initializes a new instance with the specified URL pattern.
    /// </summary>
    /// <param name="urlPattern">
    /// The URL pattern with {0} as a placeholder for the file name.
    /// Example: "/images/{0}" or "https://example.com/media/{0}"
    /// </param>
    public UrlPatternImageResolver(string urlPattern)
    {
        _urlPattern = urlPattern ?? throw new ArgumentNullException(nameof(urlPattern));
    }

    /// <inheritdoc />
    public ImageInfo? Resolve(ImageLink imageLink, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(imageLink);
        var target = imageLink.Target?.ToString().Trim();
        if (string.IsNullOrEmpty(target))
        {
            return null;
        }

        // Extract file name (remove namespace prefix like "File:" or "Image:")
        var colonIndex = target.IndexOf(':', StringComparison.Ordinal);
        var fileName = colonIndex >= 0 ? target[(colonIndex + 1)..].Trim() : target;

        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        // Parse size from arguments
        int? width = null;
        int? height = null;

        foreach (var arg in imageLink.Arguments)
        {
            var value = arg.Value?.ToString().Trim();
            if (value is null) continue;

            // Check for size specifications like "300px" or "300x200px"
            if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                var sizeStr = value[..^2];
                if (sizeStr.Contains('x', StringComparison.Ordinal))
                {
                    var parts = sizeStr.Split('x');
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0], out var w)) width = w;
                        if (int.TryParse(parts[1], out var h)) height = h;
                    }
                }
                else if (int.TryParse(sizeStr, out var w))
                {
                    width = w;
                }
            }
        }

        return new ImageInfo
        {
            Url = string.Format(CultureInfo.InvariantCulture, _urlPattern, Uri.EscapeDataString(fileName)),
            Width = width,
            Height = height
        };
    }
}

/// <summary>
/// A dictionary-based image resolver for known images.
/// </summary>
public class DictionaryImageResolver : IImageResolver
{
    private readonly Dictionary<string, ImageInfo> _images;
    private readonly StringComparer _comparer;

    /// <summary>
    /// Initializes a new instance with an empty dictionary.
    /// </summary>
    /// <param name="ignoreCase">Whether file names should be case-insensitive.</param>
    public DictionaryImageResolver(bool ignoreCase = true)
    {
        _comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _images = new Dictionary<string, ImageInfo>(_comparer);
    }

    /// <summary>
    /// Adds or updates an image.
    /// </summary>
    /// <param name="fileName">The file name (without namespace prefix).</param>
    /// <param name="info">The image information.</param>
    public void Add(string fileName, ImageInfo info)
    {
        _images[fileName] = info;
    }

    /// <inheritdoc />
    public ImageInfo? Resolve(ImageLink imageLink, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(imageLink);
        var target = imageLink.Target?.ToString().Trim();
        if (string.IsNullOrEmpty(target))
        {
            return null;
        }

        // Extract file name (remove namespace prefix)
        var colonIndex = target.IndexOf(':', StringComparison.Ordinal);
        var fileName = colonIndex >= 0 ? target[(colonIndex + 1)..].Trim() : target;

        return _images.GetValueOrDefault(fileName);
    }
}
