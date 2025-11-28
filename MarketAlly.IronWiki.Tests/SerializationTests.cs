// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using FluentAssertions;
using MarketAlly.IronWiki.Nodes;
using MarketAlly.IronWiki.Parsing;
using MarketAlly.IronWiki.Serialization;
using Xunit;

namespace MarketAlly.IronWiki.Tests;

public class SerializationTests
{
    private readonly WikitextParser _parser = new();

    [Fact]
    public void Serialize_SimpleDocument_ReturnsValidJson()
    {
        var doc = _parser.Parse("Hello, world!");

        var json = WikiJsonSerializer.Serialize(doc);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"$type\"");
        json.Should().Contain("document");
    }

    [Fact]
    public void Serialize_WithIndentation_ReturnsFormattedJson()
    {
        var doc = _parser.Parse("Hello, world!");

        var json = WikiJsonSerializer.Serialize(doc, writeIndented: true);

        json.Should().Contain("\n");
        json.Should().Contain("  ");
    }

    [Fact]
    public void Deserialize_SimpleDocument_ReturnsEquivalentDocument()
    {
        var original = _parser.Parse("Hello, world!");
        var json = WikiJsonSerializer.Serialize(original);

        var deserialized = WikiJsonSerializer.DeserializeDocument(json);

        deserialized.Should().NotBeNull();
        deserialized!.ToString().Should().Be(original.ToString());
    }

    [Fact]
    public void RoundTrip_WikiLink_PreservesStructure()
    {
        var original = _parser.Parse("[[Article|Display Text]]");
        var json = WikiJsonSerializer.Serialize(original);
        var deserialized = WikiJsonSerializer.DeserializeDocument(json);

        var originalLink = original.EnumerateDescendants<WikiLink>().First();
        var deserializedLink = deserialized!.EnumerateDescendants<WikiLink>().First();

        deserializedLink.ToString().Should().Be(originalLink.ToString());
    }

    [Fact]
    public void RoundTrip_Template_PreservesArguments()
    {
        var original = _parser.Parse("{{Template|arg1|name=value}}");
        var json = WikiJsonSerializer.Serialize(original);
        var deserialized = WikiJsonSerializer.DeserializeDocument(json);

        var originalTemplate = original.EnumerateDescendants<Template>().First();
        var deserializedTemplate = deserialized!.EnumerateDescendants<Template>().First();

        deserializedTemplate.Arguments.Count.Should().Be(originalTemplate.Arguments.Count);
    }

    [Fact]
    public void RoundTrip_Heading_PreservesLevel()
    {
        var original = _parser.Parse("=== Test Heading ===");
        var json = WikiJsonSerializer.Serialize(original);
        var deserialized = WikiJsonSerializer.DeserializeDocument(json);

        var originalHeading = original.EnumerateDescendants<Heading>().First();
        var deserializedHeading = deserialized!.EnumerateDescendants<Heading>().First();

        deserializedHeading.Level.Should().Be(originalHeading.Level);
    }

    [Fact]
    public void RoundTrip_ComplexDocument_PreservesAllElements()
    {
        var wikitext = @"== Introduction ==
This is '''bold''' and [[link]].

* Item 1
* Item 2

{{Template|arg=value}}";

        var original = _parser.Parse(wikitext);
        var json = WikiJsonSerializer.Serialize(original);
        var deserialized = WikiJsonSerializer.DeserializeDocument(json);

        deserialized.Should().NotBeNull();
        deserialized!.EnumerateDescendants<Heading>().Count().Should().Be(
            original.EnumerateDescendants<Heading>().Count());
        deserialized.EnumerateDescendants<WikiLink>().Count().Should().Be(
            original.EnumerateDescendants<WikiLink>().Count());
        deserialized.EnumerateDescendants<ListItem>().Count().Should().Be(
            original.EnumerateDescendants<ListItem>().Count());
        deserialized.EnumerateDescendants<Template>().Count().Should().Be(
            original.EnumerateDescendants<Template>().Count());
    }

    [Fact]
    public void ReconstructTree_SetsParentReferences()
    {
        var original = _parser.Parse("[[Link]]");
        var json = WikiJsonSerializer.Serialize(original);
        var deserialized = WikiJsonSerializer.DeserializeDocument(json);

        var link = deserialized!.EnumerateDescendants<WikiLink>().First();

        link.Parent.Should().NotBeNull();
    }

    [Fact]
    public void ReconstructTree_SetsSiblingReferences()
    {
        var original = _parser.Parse("* Item 1\n* Item 2\n* Item 3");
        var json = WikiJsonSerializer.Serialize(original);
        var deserialized = WikiJsonSerializer.DeserializeDocument(json);

        var lines = deserialized!.Lines.ToList();
        if (lines.Count >= 2)
        {
            lines[0].NextSibling.Should().Be(lines[1]);
            lines[1].PreviousSibling.Should().Be(lines[0]);
        }
    }

    [Fact]
    public void ToJson_ExtensionMethod_Works()
    {
        var doc = _parser.Parse("Test");

        var json = doc.ToJson();

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("document");
    }

    [Fact]
    public void Serialize_SourceSpans_IncludedWhenPresent()
    {
        var parser = new WikitextParser(new WikitextParserOptions { TrackSourceSpans = true });
        var doc = parser.Parse("Hello");

        var json = WikiJsonSerializer.Serialize(doc, writeIndented: true);

        json.Should().Contain("span");
    }

    [Fact]
    public async Task SerializeAsync_WritesToStream()
    {
        var doc = _parser.Parse("Test");
        using var stream = new MemoryStream();

        await WikiJsonSerializer.SerializeAsync(stream, doc);

        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeserializeAsync_ReadsFromStream()
    {
        var original = _parser.Parse("Test");
        using var stream = new MemoryStream();
        await WikiJsonSerializer.SerializeAsync(stream, original);
        stream.Position = 0;

        var deserialized = await WikiJsonSerializer.DeserializeAsync<WikitextDocument>(stream);

        deserialized.Should().NotBeNull();
    }
}
