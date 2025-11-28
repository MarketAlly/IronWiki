// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using MarketAlly.IronWiki.Nodes;

namespace MarketAlly.IronWiki.Parsing;

/// <summary>
/// Parses wikitext markup into an Abstract Syntax Tree (AST).
/// </summary>
/// <remarks>
/// <para>This class is thread-safe. Multiple threads can use the same <see cref="WikitextParser"/>
/// instance to parse different wikitext strings concurrently.</para>
/// <para>For best performance in single-threaded scenarios, reuse the parser instance.</para>
/// </remarks>
/// <example>
/// <code>
/// var parser = new WikitextParser();
/// var ast = parser.Parse("== Hello ==\nThis is a '''test'''.");
///
/// // Access nodes
/// foreach (var heading in ast.EnumerateDescendants&lt;Heading&gt;())
/// {
///     Console.WriteLine($"Found heading level {heading.Level}");
/// }
/// </code>
/// </example>
public sealed class WikitextParser
{
    private readonly WikitextParserOptions _options;
    private ParserCore? _cachedCore;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikitextParser"/> class with default options.
    /// </summary>
    public WikitextParser() : this(new WikitextParserOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WikitextParser"/> class with the specified options.
    /// </summary>
    /// <param name="options">The parser options to use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    public WikitextParser(WikitextParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Freeze();
    }

    /// <summary>
    /// Gets the options used by this parser.
    /// </summary>
    public WikitextParserOptions Options => _options;

    /// <summary>
    /// Parses the specified wikitext into an AST.
    /// </summary>
    /// <param name="wikitext">The wikitext to parse.</param>
    /// <returns>A <see cref="WikitextDocument"/> containing the parsed AST.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wikitext"/> is <c>null</c>.</exception>
    public WikitextDocument Parse(string wikitext)
    {
        return Parse(wikitext, CancellationToken.None);
    }

    /// <summary>
    /// Parses the specified wikitext into an AST.
    /// </summary>
    /// <param name="wikitext">The wikitext to parse.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="WikitextDocument"/> containing the parsed AST.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wikitext"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public WikitextDocument Parse(string wikitext, CancellationToken cancellationToken)
    {
        return Parse(wikitext, null, cancellationToken);
    }

    /// <summary>
    /// Parses the specified wikitext into an AST, collecting any diagnostics.
    /// </summary>
    /// <param name="wikitext">The wikitext to parse.</param>
    /// <param name="diagnostics">A collection to receive parsing diagnostics (warnings about recovery, etc.).</param>
    /// <returns>A <see cref="WikitextDocument"/> containing the parsed AST.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wikitext"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>When the parser encounters content it cannot fully parse, it will recover by treating
    /// the problematic content as plain text. Diagnostics are added to help identify these locations.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var parser = new WikitextParser();
    /// var diagnostics = new List&lt;ParsingDiagnostic&gt;();
    /// var ast = parser.Parse(wikitext, diagnostics);
    ///
    /// foreach (var diag in diagnostics)
    /// {
    ///     Console.WriteLine(diag); // e.g., "[Warning] (42:10): Parser recovery: consumed unparseable character as plain text near '{{invalid...'"
    /// }
    /// </code>
    /// </example>
    public WikitextDocument Parse(string wikitext, ICollection<ParsingDiagnostic> diagnostics)
    {
        return Parse(wikitext, diagnostics, CancellationToken.None);
    }

    /// <summary>
    /// Parses the specified wikitext into an AST, collecting any diagnostics.
    /// </summary>
    /// <param name="wikitext">The wikitext to parse.</param>
    /// <param name="diagnostics">A collection to receive parsing diagnostics (warnings about recovery, etc.), or null to ignore diagnostics.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="WikitextDocument"/> containing the parsed AST.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wikitext"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public WikitextDocument Parse(string wikitext, ICollection<ParsingDiagnostic>? diagnostics, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wikitext);
        cancellationToken.ThrowIfCancellationRequested();

        // Try to reuse a cached parser core for better performance
        var core = Interlocked.Exchange(ref _cachedCore, null) ?? new ParserCore();

        try
        {
            return core.Parse(_options, wikitext, cancellationToken, diagnostics);
        }
        finally
        {
            // Return the core to the cache if possible
            Interlocked.CompareExchange(ref _cachedCore, core, null);
        }
    }

    /// <summary>
    /// Parses the specified wikitext asynchronously.
    /// </summary>
    /// <param name="wikitext">The wikitext to parse.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous parse operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wikitext"/> is <c>null</c>.</exception>
    public Task<WikitextDocument> ParseAsync(string wikitext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wikitext);

        // For small inputs, parse synchronously
        if (wikitext.Length < 10000)
        {
            return Task.FromResult(Parse(wikitext, cancellationToken));
        }

        // For larger inputs, run on thread pool
        return Task.Run(() => Parse(wikitext, cancellationToken), cancellationToken);
    }
}
