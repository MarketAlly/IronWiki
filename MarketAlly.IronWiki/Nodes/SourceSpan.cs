// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MarketAlly.IronWiki.Nodes;

/// <summary>
/// Represents a span of source text with start and end positions.
/// </summary>
/// <remarks>
/// Line and column numbers are zero-based to match common editor conventions.
/// </remarks>
[JsonConverter(typeof(SourceSpanJsonConverter))]
public readonly struct SourceSpan : IEquatable<SourceSpan>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceSpan"/> struct.
    /// </summary>
    /// <param name="startLine">The zero-based starting line number.</param>
    /// <param name="startColumn">The zero-based starting column number.</param>
    /// <param name="endLine">The zero-based ending line number.</param>
    /// <param name="endColumn">The zero-based ending column number.</param>
    public SourceSpan(int startLine, int startColumn, int endLine, int endColumn)
    {
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    /// <summary>
    /// Gets the zero-based starting line number.
    /// </summary>
    public int StartLine { get; }

    /// <summary>
    /// Gets the zero-based starting column number.
    /// </summary>
    public int StartColumn { get; }

    /// <summary>
    /// Gets the zero-based ending line number.
    /// </summary>
    public int EndLine { get; }

    /// <summary>
    /// Gets the zero-based ending column number.
    /// </summary>
    public int EndColumn { get; }

    /// <summary>
    /// Gets a value indicating whether this span represents a single point (zero-length span).
    /// </summary>
    public bool IsEmpty => StartLine == EndLine && StartColumn == EndColumn;

    /// <summary>
    /// Gets the starting position as a tuple.
    /// </summary>
    public (int Line, int Column) Start => (StartLine, StartColumn);

    /// <summary>
    /// Gets the ending position as a tuple.
    /// </summary>
    public (int Line, int Column) End => (EndLine, EndColumn);

    /// <summary>
    /// Creates a new span that encompasses both this span and another span.
    /// </summary>
    /// <param name="other">The other span to merge with.</param>
    /// <returns>A new span that covers both input spans.</returns>
    public SourceSpan Merge(SourceSpan other)
    {
        var startLine = Math.Min(StartLine, other.StartLine);
        var startColumn = StartLine < other.StartLine ? StartColumn :
                          other.StartLine < StartLine ? other.StartColumn :
                          Math.Min(StartColumn, other.StartColumn);

        var endLine = Math.Max(EndLine, other.EndLine);
        var endColumn = EndLine > other.EndLine ? EndColumn :
                        other.EndLine > EndLine ? other.EndColumn :
                        Math.Max(EndColumn, other.EndColumn);

        return new SourceSpan(startLine, startColumn, endLine, endColumn);
    }

    /// <summary>
    /// Determines whether this span contains the specified position.
    /// </summary>
    /// <param name="line">The zero-based line number.</param>
    /// <param name="column">The zero-based column number.</param>
    /// <returns><c>true</c> if the position is within this span; otherwise, <c>false</c>.</returns>
    public bool Contains(int line, int column)
    {
        if (line < StartLine || line > EndLine)
        {
            return false;
        }

        if (line == StartLine && column < StartColumn)
        {
            return false;
        }

        if (line == EndLine && column > EndColumn)
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public bool Equals(SourceSpan other)
    {
        return StartLine == other.StartLine &&
               StartColumn == other.StartColumn &&
               EndLine == other.EndLine &&
               EndColumn == other.EndColumn;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SourceSpan other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(StartLine, StartColumn, EndLine, EndColumn);

    /// <summary>
    /// Determines whether two spans are equal.
    /// </summary>
    public static bool operator ==(SourceSpan left, SourceSpan right) => left.Equals(right);

    /// <summary>
    /// Determines whether two spans are not equal.
    /// </summary>
    public static bool operator !=(SourceSpan left, SourceSpan right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"({StartLine},{StartColumn})-({EndLine},{EndColumn})";
}

/// <summary>
/// JSON converter for <see cref="SourceSpan"/> that produces compact array format.
/// </summary>
#pragma warning disable CA1812 // Instantiated via JsonConverterAttribute
internal sealed class SourceSpanJsonConverter : JsonConverter<SourceSpan>
#pragma warning restore CA1812
{
    public override SourceSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected array for SourceSpan.");
        }

        reader.Read();
        var startLine = reader.GetInt32();
        reader.Read();
        var startColumn = reader.GetInt32();
        reader.Read();
        var endLine = reader.GetInt32();
        reader.Read();
        var endColumn = reader.GetInt32();
        reader.Read();

        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Expected end of array for SourceSpan.");
        }

        return new SourceSpan(startLine, startColumn, endLine, endColumn);
    }

    public override void Write(Utf8JsonWriter writer, SourceSpan value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.StartLine);
        writer.WriteNumberValue(value.StartColumn);
        writer.WriteNumberValue(value.EndLine);
        writer.WriteNumberValue(value.EndColumn);
        writer.WriteEndArray();
    }
}
