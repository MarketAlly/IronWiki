// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace MarketAlly.IronWiki.Parsing;

/// <summary>
/// Represents a diagnostic message generated during parsing.
/// </summary>
public sealed class ParsingDiagnostic
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParsingDiagnostic"/> class.
    /// </summary>
    public ParsingDiagnostic(DiagnosticSeverity severity, string message, int line, int column, string? context = null)
    {
        Severity = severity;
        Message = message;
        Line = line;
        Column = column;
        Context = context;
    }

    /// <summary>
    /// Gets the severity of the diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the line number (0-based) where the issue occurred.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column number (0-based) where the issue occurred.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the context string showing surrounding text, if available.
    /// </summary>
    public string? Context { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var location = $"({Line + 1}:{Column + 1})";
        var contextPart = Context is not null ? $" near '{Context}'" : "";
        return $"[{Severity}] {location}: {Message}{contextPart}";
    }
}

/// <summary>
/// Diagnostic severity levels.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Informational message - parsing succeeded but with notes.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - parsing succeeded but with recovery or potential issues.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - parsing encountered a significant problem.
    /// </summary>
    Error
}
