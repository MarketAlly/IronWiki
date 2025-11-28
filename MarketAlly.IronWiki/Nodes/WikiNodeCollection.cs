// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MarketAlly.IronWiki.Nodes;

/// <summary>
/// Interface for node collections to support node manipulation operations.
/// </summary>
internal interface IWikiNodeCollection
{
    /// <summary>
    /// Inserts a node before the specified reference node.
    /// </summary>
    void InsertBefore(WikiNode reference, WikiNode node);

    /// <summary>
    /// Inserts a node after the specified reference node.
    /// </summary>
    void InsertAfter(WikiNode reference, WikiNode node);

    /// <summary>
    /// Removes the specified node from the collection.
    /// </summary>
    bool Remove(WikiNode node);
}

/// <summary>
/// A collection of wiki nodes that maintains parent-child relationships and sibling links.
/// </summary>
/// <typeparam name="T">The type of nodes in the collection.</typeparam>
/// <remarks>
/// This collection relies on <see cref="JsonObjectCreationHandling.Populate"/> mode for deserialization.
/// The serializer will populate the existing collection through the IList interface.
/// </remarks>
public sealed class WikiNodeCollection<T> : IList<T>, IReadOnlyList<T>, IWikiNodeCollection where T : WikiNode
{
    private readonly WikiNode _owner;
    private readonly List<T> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiNodeCollection{T}"/> class.
    /// </summary>
    /// <param name="owner">The parent node that owns this collection.</param>
    internal WikiNodeCollection(WikiNode owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _items = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiNodeCollection{T}"/> class with initial capacity.
    /// </summary>
    /// <param name="owner">The parent node that owns this collection.</param>
    /// <param name="capacity">The initial capacity of the collection.</param>
    internal WikiNodeCollection(WikiNode owner, int capacity)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _items = new List<T>(capacity);
    }

    /// <summary>
    /// Gets the number of nodes in the collection.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Gets the first node in the collection, or <c>null</c> if empty.
    /// </summary>
    public T? FirstNode => _items.Count > 0 ? _items[0] : null;

    /// <summary>
    /// Gets the last node in the collection, or <c>null</c> if empty.
    /// </summary>
    public T? LastNode => _items.Count > 0 ? _items[^1] : null;

    /// <summary>
    /// Gets or sets the node at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the node.</param>
    /// <returns>The node at the specified index.</returns>
    public T this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var oldItem = _items[index];
            if (ReferenceEquals(oldItem, value))
            {
                return;
            }

            var newItem = _owner.Attach(value);

            // Update sibling links
            newItem.PreviousSibling = oldItem.PreviousSibling;
            newItem.NextSibling = oldItem.NextSibling;

            if (newItem.PreviousSibling is not null)
            {
                newItem.PreviousSibling.NextSibling = newItem;
            }
            if (newItem.NextSibling is not null)
            {
                newItem.NextSibling.PreviousSibling = newItem;
            }

            newItem.ParentCollection = this;

            // Detach old item
            _owner.Detach(oldItem);
            oldItem.PreviousSibling = null;
            oldItem.NextSibling = null;
            oldItem.ParentCollection = null;

            _items[index] = newItem;
        }
    }

    /// <summary>
    /// Adds a node to the end of the collection.
    /// </summary>
    /// <param name="item">The node to add.</param>
    public void Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var node = _owner.Attach(item);
        node.ParentCollection = this;

        if (_items.Count > 0)
        {
            var last = _items[^1];
            last.NextSibling = node;
            node.PreviousSibling = last;
        }

        _items.Add(node);
    }

    /// <summary>
    /// Adds multiple nodes to the end of the collection.
    /// </summary>
    /// <param name="items">The nodes to add.</param>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Adds nodes from another collection, transferring ownership.
    /// </summary>
    /// <param name="source">The source collection.</param>
    public void AddFrom(WikiNodeCollection<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        // Create a copy to avoid modification during iteration
        var itemsToAdd = source._items.ToList();
        foreach (var item in itemsToAdd)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Inserts a node at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert.</param>
    /// <param name="item">The node to insert.</param>
    public void Insert(int index, T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (index < 0 || index > _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var node = _owner.Attach(item);
        node.ParentCollection = this;

        // Update sibling links
        if (index > 0)
        {
            var previous = _items[index - 1];
            previous.NextSibling = node;
            node.PreviousSibling = previous;
        }

        if (index < _items.Count)
        {
            var next = _items[index];
            next.PreviousSibling = node;
            node.NextSibling = next;
        }

        _items.Insert(index, node);
    }

    /// <summary>
    /// Removes the first occurrence of a node from the collection.
    /// </summary>
    /// <param name="item">The node to remove.</param>
    /// <returns><c>true</c> if the node was removed; otherwise, <c>false</c>.</returns>
    public bool Remove(T item)
    {
        var index = _items.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes the node at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the node to remove.</param>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var item = _items[index];

        // Update sibling links
        if (item.PreviousSibling is not null)
        {
            item.PreviousSibling.NextSibling = item.NextSibling;
        }
        if (item.NextSibling is not null)
        {
            item.NextSibling.PreviousSibling = item.PreviousSibling;
        }

        _owner.Detach(item);
        item.PreviousSibling = null;
        item.NextSibling = null;
        item.ParentCollection = null;

        _items.RemoveAt(index);
    }

    /// <summary>
    /// Removes all nodes from the collection.
    /// </summary>
    public void Clear()
    {
        foreach (var item in _items)
        {
            _owner.Detach(item);
            item.PreviousSibling = null;
            item.NextSibling = null;
            item.ParentCollection = null;
        }
        _items.Clear();
    }

    /// <summary>
    /// Determines whether the collection contains a specific node.
    /// </summary>
    /// <param name="item">The node to locate.</param>
    /// <returns><c>true</c> if the node is found; otherwise, <c>false</c>.</returns>
    public bool Contains(T item) => _items.Contains(item);

    /// <summary>
    /// Gets the index of a specific node.
    /// </summary>
    /// <param name="item">The node to locate.</param>
    /// <returns>The index of the node, or -1 if not found.</returns>
    public int IndexOf(T item) => _items.IndexOf(item);

    /// <summary>
    /// Copies the collection to an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting index in the array.</param>
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    public List<T>.Enumerator GetEnumerator() => _items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    #region IWikiNodeCollection Implementation

    void IWikiNodeCollection.InsertBefore(WikiNode reference, WikiNode node)
    {
        if (reference is not T typedRef)
        {
            throw new ArgumentException($"Reference node must be of type {typeof(T).Name}.", nameof(reference));
        }
        if (node is not T typedNode)
        {
            throw new ArgumentException($"Node must be of type {typeof(T).Name}.", nameof(node));
        }

        var index = _items.IndexOf(typedRef);
        if (index < 0)
        {
            throw new InvalidOperationException("Reference node is not in this collection.");
        }

        Insert(index, typedNode);
    }

    void IWikiNodeCollection.InsertAfter(WikiNode reference, WikiNode node)
    {
        if (reference is not T typedRef)
        {
            throw new ArgumentException($"Reference node must be of type {typeof(T).Name}.", nameof(reference));
        }
        if (node is not T typedNode)
        {
            throw new ArgumentException($"Node must be of type {typeof(T).Name}.", nameof(node));
        }

        var index = _items.IndexOf(typedRef);
        if (index < 0)
        {
            throw new InvalidOperationException("Reference node is not in this collection.");
        }

        Insert(index + 1, typedNode);
    }

    bool IWikiNodeCollection.Remove(WikiNode node)
    {
        if (node is not T typedNode)
        {
            return false;
        }
        return Remove(typedNode);
    }

    #endregion
}

/// <summary>
/// Factory for creating JSON converters for <see cref="WikiNodeCollection{T}"/>.
/// </summary>
#pragma warning disable CA1812 // Instantiated via JsonConverterAttribute
internal sealed class WikiNodeCollectionJsonConverterFactory : JsonConverterFactory
#pragma warning restore CA1812
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(WikiNodeCollection<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var elementType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(WikiNodeCollectionJsonConverter<>).MakeGenericType(elementType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// JSON converter for <see cref="WikiNodeCollection{T}"/>.
/// </summary>
#pragma warning disable CA1812 // Instantiated via reflection in WikiNodeCollectionJsonConverterFactory
internal sealed class WikiNodeCollectionJsonConverter<T> : JsonConverter<WikiNodeCollection<T>> where T : WikiNode
#pragma warning restore CA1812
{
    public override WikiNodeCollection<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Cannot create a WikiNodeCollection without an owner, so we need special handling
        // The JsonSerializerOptions should use PreferredObjectCreationHandling = Populate
        // But since we can't modify the existing object here, we throw
        throw new JsonException(
            "WikiNodeCollection cannot be deserialized directly. " +
            "Use JsonSerializerOptions with PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate.");
    }

    public override void Write(Utf8JsonWriter writer, WikiNodeCollection<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }
        writer.WriteEndArray();
    }
}
