// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Serialization;

/// <summary>
/// Provides JSON serialization and deserialization for wiki AST nodes.
/// </summary>
public static class WikiJsonSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = CreateOptions(false);
    private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(true);

    /// <summary>
    /// Creates JSON serializer options configured for wiki AST serialization.
    /// </summary>
    /// <param name="writeIndented">Whether to format the JSON with indentation.</param>
    /// <returns>Configured <see cref="JsonSerializerOptions"/>.</returns>
    public static JsonSerializerOptions CreateOptions(bool writeIndented = false)
    {
        return new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Use Populate mode so existing collections (like Lines, Inlines) are populated
            // rather than replaced during deserialization
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }

    /// <summary>
    /// Serializes a wiki node to JSON.
    /// </summary>
    /// <param name="node">The node to serialize.</param>
    /// <param name="writeIndented">Whether to format the JSON with indentation.</param>
    /// <returns>The JSON string representation.</returns>
    public static string Serialize(WikiNode node, bool writeIndented = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        var options = writeIndented ? IndentedOptions : DefaultOptions;
        return JsonSerializer.Serialize(node, options);
    }

    /// <summary>
    /// Serializes a wiki node to a UTF-8 byte array.
    /// </summary>
    /// <param name="node">The node to serialize.</param>
    /// <param name="writeIndented">Whether to format the JSON with indentation.</param>
    /// <returns>The UTF-8 encoded JSON bytes.</returns>
    public static byte[] SerializeToUtf8Bytes(WikiNode node, bool writeIndented = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        var options = writeIndented ? IndentedOptions : DefaultOptions;
        return JsonSerializer.SerializeToUtf8Bytes(node, options);
    }

    /// <summary>
    /// Serializes a wiki node to a stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="node">The node to serialize.</param>
    /// <param name="writeIndented">Whether to format the JSON with indentation.</param>
    public static void Serialize(Stream stream, WikiNode node, bool writeIndented = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(node);
        var options = writeIndented ? IndentedOptions : DefaultOptions;
        JsonSerializer.Serialize(stream, node, options);
    }

    /// <summary>
    /// Asynchronously serializes a wiki node to a stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="node">The node to serialize.</param>
    /// <param name="writeIndented">Whether to format the JSON with indentation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static Task SerializeAsync(Stream stream, WikiNode node, bool writeIndented = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(node);
        var options = writeIndented ? IndentedOptions : DefaultOptions;
        return JsonSerializer.SerializeAsync(stream, node, options, cancellationToken);
    }

    /// <summary>
    /// Deserializes a wiki node from JSON.
    /// </summary>
    /// <typeparam name="T">The type of node to deserialize.</typeparam>
    /// <param name="json">The JSON string.</param>
    /// <returns>The deserialized node, or <c>null</c> if the JSON is null.</returns>
    public static T? Deserialize<T>(string json) where T : WikiNode
    {
        ArgumentNullException.ThrowIfNull(json);
        var node = JsonSerializer.Deserialize<T>(json, DefaultOptions);
        if (node is not null)
        {
            ReconstructTree(node);
        }
        return node;
    }

    /// <summary>
    /// Deserializes a wiki document from JSON.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The deserialized document, or <c>null</c> if the JSON is null.</returns>
    public static WikitextDocument? DeserializeDocument(string json)
    {
        return Deserialize<WikitextDocument>(json);
    }

    /// <summary>
    /// Deserializes a wiki node from UTF-8 bytes.
    /// </summary>
    /// <typeparam name="T">The type of node to deserialize.</typeparam>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    /// <returns>The deserialized node, or <c>null</c> if the JSON is null.</returns>
    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json) where T : WikiNode
    {
        var node = JsonSerializer.Deserialize<T>(utf8Json, DefaultOptions);
        if (node is not null)
        {
            ReconstructTree(node);
        }
        return node;
    }

    /// <summary>
    /// Deserializes a wiki node from a stream.
    /// </summary>
    /// <typeparam name="T">The type of node to deserialize.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The deserialized node, or <c>null</c> if the JSON is null.</returns>
    public static T? Deserialize<T>(Stream stream) where T : WikiNode
    {
        ArgumentNullException.ThrowIfNull(stream);
        var node = JsonSerializer.Deserialize<T>(stream, DefaultOptions);
        if (node is not null)
        {
            ReconstructTree(node);
        }
        return node;
    }

    /// <summary>
    /// Asynchronously deserializes a wiki node from a stream.
    /// </summary>
    /// <typeparam name="T">The type of node to deserialize.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : WikiNode
    {
        ArgumentNullException.ThrowIfNull(stream);
        var node = await JsonSerializer.DeserializeAsync<T>(stream, DefaultOptions, cancellationToken).ConfigureAwait(false);
        if (node is not null)
        {
            ReconstructTree(node);
        }
        return node;
    }

    /// <summary>
    /// Reconstructs parent and sibling relationships after deserialization.
    /// </summary>
    /// <param name="node">The root node to process.</param>
    public static void ReconstructTree(WikiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ReconstructTreeRecursive(node);
    }

    private static void ReconstructTreeRecursive(WikiNode node)
    {
        WikiNode? previousChild = null;

        foreach (var child in node.EnumerateChildren())
        {
            child.Parent = node;
            child.PreviousSibling = previousChild;

            if (previousChild is not null)
            {
                previousChild.NextSibling = child;
            }

            ReconstructTreeRecursive(child);
            previousChild = child;
        }

        if (previousChild is not null)
        {
            previousChild.NextSibling = null;
        }
    }
}

/// <summary>
/// Extension methods for JSON serialization of wiki nodes.
/// </summary>
public static class WikiJsonExtensions
{
    /// <summary>
    /// Converts a wiki node to JSON.
    /// </summary>
    /// <param name="node">The node to convert.</param>
    /// <param name="writeIndented">Whether to format the JSON with indentation.</param>
    /// <returns>The JSON string representation.</returns>
    public static string ToJson(this WikiNode node, bool writeIndented = false)
    {
        return WikiJsonSerializer.Serialize(node, writeIndented);
    }
}
