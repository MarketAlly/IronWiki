// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Parsing;

/// <summary>
/// Core parsing engine for wikitext.
/// </summary>
internal sealed partial class ParserCore
{
    private WikitextParserOptions _options = null!;
    private string _text = null!;
    private int _position;
    private int _line;
    private int _column;
    private readonly Stack<ParsingContext> _contextStack = new();
    private CancellationToken _cancellationToken;
    private ICollection<ParsingDiagnostic>? _diagnostics;

    private static readonly Dictionary<string, Regex> TokenMatcherCache = new();
    private static readonly Dictionary<string, Terminator> TerminatorCache = new();

    /// <summary>
    /// Parses the wikitext and returns the AST.
    /// </summary>
    public WikitextDocument Parse(WikitextParserOptions options, string text, CancellationToken cancellationToken, ICollection<ParsingDiagnostic>? diagnostics = null)
    {
        // Initialize state
        _options = options;
        _text = text;
        _position = 0;
        _line = 0;
        _column = 0;
        _contextStack.Clear();
        _cancellationToken = cancellationToken;
        _diagnostics = diagnostics;

        try
        {
            var root = ParseWikitext();

            // Verify we consumed all input
            if (_position < _text.Length)
            {
                throw new InvalidOperationException(
                    $"Parser did not consume all input. Stopped at position {_position} of {_text.Length}.");
            }

            if (_contextStack.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Parser context stack not empty. {_contextStack.Count} contexts remaining.");
            }

            return root;
        }
        finally
        {
            // Clean up to avoid holding references
            _options = null!;
            _text = null!;
            _diagnostics = null;
        }
    }

    /// <summary>
    /// Adds a diagnostic message if diagnostics collection is enabled.
    /// </summary>
    private void AddDiagnostic(DiagnosticSeverity severity, string message, int? contextLength = 20)
    {
        if (_diagnostics is null)
        {
            return;
        }

        string? context = null;
        if (contextLength > 0 && _position < _text.Length)
        {
            var endPos = Math.Min(_position + contextLength.Value, _text.Length);
            context = _text[_position..endPos].Replace('\n', '↵').Replace('\r', ' ');
            if (endPos < _text.Length)
            {
                context += "...";
            }
        }

        _diagnostics.Add(new ParsingDiagnostic(severity, message, _line, _column, context));
    }

    #region Context Management

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BeginContext() => BeginContext(null, false);

    private void BeginContext(string? terminatorPattern, bool overridesTerminator)
    {
        var terminator = terminatorPattern is not null ? GetTerminator(terminatorPattern) : null;
        _contextStack.Push(new ParsingContext(terminator, overridesTerminator, _position, _line, _column));
    }

    private ref readonly ParsingContext CurrentContext => ref CollectionsMarshal.AsSpan(_contextStack.ToArray())[0];

    private T Accept<T>(T node, bool setSourceSpan = true) where T : WikiNode
    {
        Debug.Assert(node is not null);
        var context = _contextStack.Pop();

        if (setSourceSpan && _options.TrackSourceSpans)
        {
            node.SetSourceSpan(context.StartLine, context.StartColumn, _line, _column);
        }

        return node;
    }

    private void Accept()
    {
        _contextStack.Pop();
    }

    private T? Reject<T>() where T : WikiNode
    {
        Rollback();
        return null;
    }

    private void Rollback()
    {
        var context = _contextStack.Pop();
        _position = context.StartPosition;
        _line = context.StartLine;
        _column = context.StartColumn;
    }

    #endregion

    #region Termination

    private bool NeedsTerminate(Terminator? ignoredTerminator = null)
    {
        if (_position >= _text.Length)
        {
            return true;
        }

        foreach (var context in _contextStack)
        {
            if (context.Terminator is not null &&
                context.Terminator != ignoredTerminator &&
                context.Terminator.IsMatch(_text, _position))
            {
                return true;
            }

            if (context.OverridesTerminator)
            {
                break;
            }
        }

        return false;
    }

    private int FindTerminator(int skipChars)
    {
        var startIndex = _position + skipChars;
        if (startIndex >= _text.Length)
        {
            return _text.Length;
        }

        var minIndex = _text.Length;

        foreach (var context in _contextStack)
        {
            if (context.Terminator is not null)
            {
                var index = context.Terminator.Search(_text, startIndex);
                if (index >= 0 && index < minIndex)
                {
                    minIndex = index;
                }
            }

            if (context.OverridesTerminator)
            {
                break;
            }
        }

        return minIndex;
    }

    private static Terminator GetTerminator(string pattern)
    {
        lock (TerminatorCache)
        {
            if (!TerminatorCache.TryGetValue(pattern, out var terminator))
            {
                terminator = new Terminator(pattern);
                TerminatorCache[pattern] = terminator;
            }
            return terminator;
        }
    }

    #endregion

    #region Token Matching

    private string? LookAhead(string pattern)
    {
        var regex = GetTokenMatcher(pattern);
        var match = regex.Match(_text, _position);
        if (!match.Success || match.Index != _position)
        {
            return null;
        }
        // Zero-length matches are valid for patterns like [a-z]* which can match empty strings
        return match.Value;
    }

    private string? Consume(string pattern)
    {
        var token = LookAhead(pattern);
        if (token is null)
        {
            return null;
        }
        if (token.Length > 0)
        {
            AdvancePosition(token.Length);
        }
        return token;
    }

    private static Regex GetTokenMatcher(string pattern)
    {
        lock (TokenMatcherCache)
        {
            if (!TokenMatcherCache.TryGetValue(pattern, out var regex))
            {
                regex = new Regex(@"\G(" + pattern + ")", RegexOptions.Compiled);
                TokenMatcherCache[pattern] = regex;
            }
            return regex;
        }
    }

    private void AdvancePosition(int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (_text[_position] == '\n')
            {
                _line++;
                _column = 0;
            }
            else
            {
                _column++;
            }
            _position++;
        }
    }

    private void AdvanceTo(int newPosition)
    {
        Debug.Assert(newPosition > _position);
        while (_position < newPosition)
        {
            if (_text[_position] == '\n')
            {
                _line++;
                _column = 0;
            }
            else
            {
                _column++;
            }
            _position++;
        }
    }

    private bool IsAtLineStart => _column == 0;

    /// <summary>
    /// Consumes a single character for recovery when the parser is stuck.
    /// Returns null if at end of input.
    /// </summary>
    private string? ConsumeRecoveryChar()
    {
        if (_position >= _text.Length)
        {
            return null;
        }

        var c = _text[_position];
        AdvancePosition(1);
        return c.ToString();
    }

    #endregion

    #region Structures

    private readonly record struct ParsingContext(
        Terminator? Terminator,
        bool OverridesTerminator,
        int StartPosition,
        int StartLine,
        int StartColumn);

    private sealed class Terminator
    {
        private readonly Regex _matcher;
        private readonly Regex _searcher;

        public Terminator(string pattern)
        {
            Debug.Assert(!pattern.StartsWith('^'));
            _matcher = new Regex(@"\G(" + pattern + ")", RegexOptions.Compiled);
            _searcher = new Regex(pattern, RegexOptions.Compiled);
        }

        public bool IsMatch(string text, int startIndex) => _matcher.IsMatch(text, startIndex);

        public int Search(string text, int startIndex)
        {
            var match = _searcher.Match(text, startIndex);
            return match.Success ? match.Index : -1;
        }
    }

    private enum RunParsingMode
    {
        /// <summary>Full inline content including links, formatting, etc.</summary>
        Run,
        /// <summary>Expandable text (templates, comments, plain text only).</summary>
        ExpandableText,
        /// <summary>URL text for external links.</summary>
        ExpandableUrl
    }

    #endregion
}

// Helper for Stack access
file static class CollectionsMarshal
{
    public static Span<T> AsSpan<T>(T[] array) => array.AsSpan();
}
