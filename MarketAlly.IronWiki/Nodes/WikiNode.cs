// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MarketAlly.IronWiki.Nodes;

/// <summary>
/// Represents the abstract base class for all nodes in the wikitext Abstract Syntax Tree (AST).
/// </summary>
/// <remarks>
/// <para>
/// This class provides core functionality for tree navigation, node manipulation, annotations,
/// source location tracking, and serialization support.
/// </para>
/// <para>
/// All concrete node types inherit from this class and implement the abstract members
/// to provide type-specific behavior.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(WikitextDocument), "document")]
[JsonDerivedType(typeof(Paragraph), "paragraph")]
[JsonDerivedType(typeof(Heading), "heading")]
[JsonDerivedType(typeof(ListItem), "listItem")]
[JsonDerivedType(typeof(HorizontalRule), "horizontalRule")]
[JsonDerivedType(typeof(Table), "table")]
[JsonDerivedType(typeof(TableRow), "tableRow")]
[JsonDerivedType(typeof(TableCell), "tableCell")]
[JsonDerivedType(typeof(TableCaption), "tableCaption")]
[JsonDerivedType(typeof(PlainText), "plainText")]
[JsonDerivedType(typeof(WikiLink), "wikiLink")]
[JsonDerivedType(typeof(ExternalLink), "externalLink")]
[JsonDerivedType(typeof(ImageLink), "imageLink")]
[JsonDerivedType(typeof(ImageLinkArgument), "imageLinkArgument")]
[JsonDerivedType(typeof(Template), "template")]
[JsonDerivedType(typeof(TemplateArgument), "templateArgument")]
[JsonDerivedType(typeof(ArgumentReference), "argumentReference")]
[JsonDerivedType(typeof(FormatSwitch), "formatSwitch")]
[JsonDerivedType(typeof(Comment), "comment")]
[JsonDerivedType(typeof(HtmlTag), "htmlTag")]
[JsonDerivedType(typeof(ParserTag), "parserTag")]
[JsonDerivedType(typeof(TagAttributeNode), "tagAttribute")]
[JsonDerivedType(typeof(Run), "run")]
public abstract class WikiNode
{
    private object? _annotations;
    private SourceSpan _sourceSpan;

    /// <summary>
    /// Gets the parent node in the AST, or <c>null</c> if this is the root node.
    /// </summary>
    [JsonIgnore]
    public WikiNode? Parent { get; internal set; }

    /// <summary>
    /// Gets the previous sibling node, or <c>null</c> if this is the first child.
    /// </summary>
    [JsonIgnore]
    public WikiNode? PreviousSibling { get; internal set; }

    /// <summary>
    /// Gets the next sibling node, or <c>null</c> if this is the last child.
    /// </summary>
    [JsonIgnore]
    public WikiNode? NextSibling { get; internal set; }

    /// <summary>
    /// Gets the parent collection that contains this node, if any.
    /// </summary>
    [JsonIgnore]
    internal IWikiNodeCollection? ParentCollection { get; set; }

    /// <summary>
    /// Gets or sets the source location information for this node.
    /// </summary>
    [JsonPropertyName("span")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public SourceSpan SourceSpan
    {
        get => _sourceSpan;
        set => _sourceSpan = value;
    }

    /// <summary>
    /// Gets a value indicating whether this node has source location information.
    /// </summary>
    [JsonIgnore]
    public bool HasSourceSpan => _sourceSpan != default;

    /// <summary>
    /// Gets a value indicating whether the closing mark for this node was inferred
    /// rather than explicitly present in the source.
    /// </summary>
    [JsonPropertyName("inferredClose")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool InferredClosingMark { get; internal set; }

    #region Tree Navigation

    /// <summary>
    /// Enumerates all direct child nodes of this node.
    /// </summary>
    /// <returns>An enumerable sequence of child nodes.</returns>
    public abstract IEnumerable<WikiNode> EnumerateChildren();

    /// <summary>
    /// Enumerates all descendant nodes in document order (depth-first traversal).
    /// </summary>
    /// <returns>An enumerable sequence of all descendant nodes.</returns>
    public IEnumerable<WikiNode> EnumerateDescendants()
    {
        var stack = new Stack<IEnumerator<WikiNode>>();
        stack.Push(EnumerateChildren().GetEnumerator());

        while (stack.Count > 0)
        {
            var enumerator = stack.Peek();
            if (!enumerator.MoveNext())
            {
                enumerator.Dispose();
                stack.Pop();
                continue;
            }

            var current = enumerator.Current;
            yield return current;
            stack.Push(current.EnumerateChildren().GetEnumerator());
        }
    }

    /// <summary>
    /// Enumerates all descendant nodes of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of nodes to enumerate.</typeparam>
    /// <returns>An enumerable sequence of descendant nodes of the specified type.</returns>
    public IEnumerable<T> EnumerateDescendants<T>() where T : WikiNode
    {
        return EnumerateDescendants().OfType<T>();
    }

    /// <summary>
    /// Enumerates all following sibling nodes.
    /// </summary>
    /// <returns>An enumerable sequence of following sibling nodes.</returns>
    public IEnumerable<WikiNode> EnumerateFollowingSiblings()
    {
        var node = NextSibling;
        while (node is not null)
        {
            yield return node;
            node = node.NextSibling;
        }
    }

    /// <summary>
    /// Enumerates all preceding sibling nodes.
    /// </summary>
    /// <returns>An enumerable sequence of preceding sibling nodes.</returns>
    public IEnumerable<WikiNode> EnumeratePrecedingSiblings()
    {
        var node = PreviousSibling;
        while (node is not null)
        {
            yield return node;
            node = node.PreviousSibling;
        }
    }

    /// <summary>
    /// Enumerates all ancestor nodes from parent to root.
    /// </summary>
    /// <returns>An enumerable sequence of ancestor nodes.</returns>
    public IEnumerable<WikiNode> EnumerateAncestors()
    {
        var node = Parent;
        while (node is not null)
        {
            yield return node;
            node = node.Parent;
        }
    }

    #endregion

    #region Node Manipulation

    /// <summary>
    /// Inserts a node before this node in the parent's child collection.
    /// </summary>
    /// <param name="node">The node to insert.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">This node is not attached to a parent collection.</exception>
    public void InsertBefore(WikiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (ParentCollection is null)
        {
            throw new InvalidOperationException("Cannot insert a sibling node when this node is not part of a collection.");
        }
        ParentCollection.InsertBefore(this, node);
    }

    /// <summary>
    /// Inserts a node after this node in the parent's child collection.
    /// </summary>
    /// <param name="node">The node to insert.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">This node is not attached to a parent collection.</exception>
    public void InsertAfter(WikiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (ParentCollection is null)
        {
            throw new InvalidOperationException("Cannot insert a sibling node when this node is not part of a collection.");
        }
        ParentCollection.InsertAfter(this, node);
    }

    /// <summary>
    /// Removes this node from its parent collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">This node is not attached to a parent collection.</exception>
    public void Remove()
    {
        if (ParentCollection is null)
        {
            throw new InvalidOperationException("Cannot remove a node that is not part of a collection.");
        }
        var removed = ParentCollection.Remove(this);
        Debug.Assert(removed, "Node should have been in the collection.");
    }

    /// <summary>
    /// Attaches a child node to this node, cloning if already attached elsewhere.
    /// </summary>
    /// <typeparam name="T">The type of node to attach.</typeparam>
    /// <param name="node">The node to attach.</param>
    /// <returns>The attached node (may be a clone if the original was already attached).</returns>
    internal T Attach<T>(T node) where T : WikiNode
    {
        Debug.Assert(node is not null);
        if (node.Parent is not null)
        {
            node = (T)node.Clone();
        }
        node.Parent = this;
        return node;
    }

    /// <summary>
    /// Attaches a child node to a storage field, handling detachment of old nodes.
    /// </summary>
    /// <typeparam name="T">The type of node to attach.</typeparam>
    /// <param name="storage">Reference to the storage field.</param>
    /// <param name="newValue">The new node value to attach.</param>
    internal void AttachChild<T>(ref T? storage, T? newValue) where T : WikiNode
    {
        if (ReferenceEquals(newValue, storage))
        {
            return;
        }

        if (newValue is not null)
        {
            newValue = Attach(newValue);
        }

        if (storage is not null)
        {
            Detach(storage);
        }

        storage = newValue;
    }

    /// <summary>
    /// Attaches a required (non-null) child node to a storage field.
    /// </summary>
    /// <typeparam name="T">The type of node to attach.</typeparam>
    /// <param name="storage">Reference to the storage field.</param>
    /// <param name="newValue">The new node value to attach.</param>
    /// <exception cref="ArgumentNullException"><paramref name="newValue"/> is <c>null</c>.</exception>
    internal void AttachRequiredChild<T>(ref T storage, T newValue) where T : WikiNode
    {
        ArgumentNullException.ThrowIfNull(newValue);
        AttachChild(ref storage!, newValue);
    }

    /// <summary>
    /// Detaches a child node from this node.
    /// </summary>
    /// <param name="node">The node to detach.</param>
    internal void Detach(WikiNode node)
    {
        Debug.Assert(node is not null);
        Debug.Assert(ReferenceEquals(node.Parent, this));
        node.Parent = null;
    }

    #endregion

    #region Annotations

    /// <summary>
    /// Adds an annotation object to this node.
    /// </summary>
    /// <param name="annotation">The annotation to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <c>null</c>.</exception>
    public void AddAnnotation(object annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (_annotations is null)
        {
            // Optimize for the common case of a single annotation that is not a list
            if (annotation is not List<object>)
            {
                _annotations = annotation;
                return;
            }
        }

        if (_annotations is not List<object> list)
        {
            list = new List<object>(4);
            if (_annotations is not null)
            {
                list.Add(_annotations);
            }
            _annotations = list;
        }

        list.Add(annotation);
    }

    /// <summary>
    /// Gets the first annotation of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of annotation to retrieve.</typeparam>
    /// <returns>The first matching annotation, or <c>null</c> if not found.</returns>
    public T? GetAnnotation<T>() where T : class
    {
        return _annotations switch
        {
            null => null,
            List<object> list => list.OfType<T>().FirstOrDefault(),
            T annotation => annotation,
            _ => null
        };
    }

    /// <summary>
    /// Gets all annotations of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of annotations to retrieve.</typeparam>
    /// <returns>An enumerable sequence of matching annotations.</returns>
    public IEnumerable<T> GetAnnotations<T>() where T : class
    {
        return _annotations switch
        {
            null => [],
            List<object> list => list.OfType<T>(),
            T annotation => [annotation],
            _ => []
        };
    }

    /// <summary>
    /// Removes all annotations of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of annotations to remove.</typeparam>
    public void RemoveAnnotations<T>() where T : class
    {
        if (_annotations is List<object> list)
        {
            list.RemoveAll(static item => item is T);
        }
        else if (_annotations is T)
        {
            _annotations = null;
        }
    }

    #endregion

    #region Cloning

    /// <summary>
    /// Creates a deep copy of this node.
    /// </summary>
    /// <returns>A new node that is a deep copy of this node.</returns>
    public WikiNode Clone()
    {
        var clone = CloneCore();
        Debug.Assert(clone is not null);
        Debug.Assert(clone.GetType() == GetType());
        return clone;
    }

    /// <summary>
    /// When overridden in a derived class, creates a deep copy of this node.
    /// </summary>
    /// <returns>A new node that is a deep copy of this node.</returns>
    protected abstract WikiNode CloneCore();

    #endregion

    #region Source Span Management

    /// <summary>
    /// Sets the source span for this node.
    /// </summary>
    internal void SetSourceSpan(int startLine, int startColumn, int endLine, int endColumn)
    {
        Debug.Assert(startLine >= 0);
        Debug.Assert(startColumn >= 0);
        Debug.Assert(endLine >= 0);
        Debug.Assert(endColumn >= 0);
        _sourceSpan = new SourceSpan(startLine, startColumn, endLine, endColumn);
    }

    /// <summary>
    /// Sets the source span by copying from another node.
    /// </summary>
    internal void SetSourceSpan(WikiNode other)
    {
        Debug.Assert(other is not null);
        _sourceSpan = other._sourceSpan;
    }

    /// <summary>
    /// Extends the end position of the source span.
    /// </summary>
    internal void ExtendSourceSpan(int endLine, int endColumn)
    {
        Debug.Assert(endLine >= 0);
        Debug.Assert(endColumn >= 0);
        _sourceSpan = new SourceSpan(_sourceSpan.StartLine, _sourceSpan.StartColumn, endLine, endColumn);
    }

    #endregion

    /// <summary>
    /// Returns the wikitext representation of this node.
    /// </summary>
    /// <returns>A string containing the wikitext representation.</returns>
    public abstract override string ToString();
}
