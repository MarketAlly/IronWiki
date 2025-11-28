// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using FluentAssertions;
using MarketAlly.IronWiki.Nodes;
using MarketAlly.IronWiki.Parsing;
using MarketAlly.IronWiki.Rendering;
using Xunit;

namespace MarketAlly.IronWiki.Tests;

public class ParserTests
{
    private readonly WikitextParser _parser = new();

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyDocument()
    {
        var result = _parser.Parse("");

        result.Should().NotBeNull();
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public void Parse_PlainText_ReturnsParagraphWithText()
    {
        var result = _parser.Parse("Hello, world!");

        result.Lines.Should().HaveCount(1);
        result.Lines[0].Should().BeOfType<Paragraph>();

        var para = (Paragraph)result.Lines[0];
        para.Inlines.Should().HaveCount(1);
        para.Inlines[0].Should().BeOfType<PlainText>();

        var text = (PlainText)para.Inlines[0];
        text.Content.Should().Be("Hello, world!");
    }

    [Fact]
    public void Parse_MultipleLines_ReturnsSeparateParagraphs()
    {
        var result = _parser.Parse("Line 1\n\nLine 2");

        result.Lines.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Theory]
    [InlineData("== Heading ==", 2)]
    [InlineData("=== Heading ===", 3)]
    [InlineData("==== Heading ====", 4)]
    [InlineData("===== Heading =====", 5)]
    [InlineData("====== Heading ======", 6)]
    public void Parse_Heading_ReturnsCorrectLevel(string wikitext, int expectedLevel)
    {
        var result = _parser.Parse(wikitext);

        result.Lines.Should().HaveCount(1);
        result.Lines[0].Should().BeOfType<Heading>();

        var heading = (Heading)result.Lines[0];
        heading.Level.Should().Be(expectedLevel);
    }

    [Fact]
    public void Parse_WikiLink_ReturnsWikiLinkNode()
    {
        var result = _parser.Parse("[[Article]]");

        var link = result.EnumerateDescendants<WikiLink>().FirstOrDefault();
        link.Should().NotBeNull();
        link!.Target.Should().NotBeNull();
        link.Text.Should().BeNull();
    }

    [Fact]
    public void Parse_WikiLinkWithText_ReturnsWikiLinkWithText()
    {
        var result = _parser.Parse("[[Article|Display Text]]");

        var link = result.EnumerateDescendants<WikiLink>().FirstOrDefault();
        link.Should().NotBeNull();
        link!.Target.Should().NotBeNull();
        link.Text.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WikiLinkWithMultilineText_ReturnsWikiLinkNode()
    {
        // This is a valid wikilink - the text part can contain line breaks
        var result = _parser.Parse("[[Test|abc\ndef]]");

        var link = result.EnumerateDescendants<WikiLink>().FirstOrDefault();
        link.Should().NotBeNull("multi-line wikilinks should be parsed as WikiLink nodes");
        link!.Target.Should().NotBeNull();
        link.Target!.ToString().Should().Be("Test");
        link.Text.Should().NotBeNull();
        link.Text!.ToString().Should().Be("abc\ndef");
    }

    [Fact]
    public void Parse_PathologicalBraces_ParsesCorrectly()
    {
        // {{{{{arg}} should be parsed as {{{ (plain text, unclosed arg ref) + {{arg}} (template)
        // Since there's no }}} to close an argument reference, the {{{ should be treated as plain text
        // and the remaining {{arg}} should be parsed as a template.
        var result = _parser.Parse("{{{{{arg}}");

        // The correct interpretation is {{{ (plain) + {{arg}} (template)
        var template = result.EnumerateDescendants<Template>().FirstOrDefault();
        template.Should().NotBeNull("should contain a Template for {{arg}}");
        template!.Name!.ToString().Should().Be("arg");

        // Should also have plain text with the unclosed {{{
        var plainTexts = result.EnumerateDescendants<PlainText>().ToList();
        plainTexts.Should().Contain(pt => pt.Content == "{{{",
            "the unclosed {{{ should appear as plain text");
    }

    [Fact]
    public void Parse_ExternalLink_ReturnsExternalLinkNode()
    {
        var result = _parser.Parse("[https://example.com Example]");

        var link = result.EnumerateDescendants<ExternalLink>().FirstOrDefault();
        link.Should().NotBeNull();
        link!.HasBrackets.Should().BeTrue();
        link.Target.Should().NotBeNull();
        link.Text.Should().NotBeNull();
    }

    [Fact]
    public void Parse_BareUrl_ReturnsExternalLinkNode()
    {
        var result = _parser.Parse("Visit https://example.com today");

        var link = result.EnumerateDescendants<ExternalLink>().FirstOrDefault();
        link.Should().NotBeNull();
        link!.HasBrackets.Should().BeFalse();
    }

    [Fact]
    public void Parse_Template_ReturnsTemplateNode()
    {
        var result = _parser.Parse("{{Template}}");

        var template = result.EnumerateDescendants<Template>().FirstOrDefault();
        template.Should().NotBeNull();
        template!.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Parse_TemplateWithArguments_ReturnsTemplateWithArgs()
    {
        var result = _parser.Parse("{{Template|arg1|name=value}}");

        var template = result.EnumerateDescendants<Template>().FirstOrDefault();
        template.Should().NotBeNull();
        template!.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ArgumentReference_ReturnsArgumentReferenceNode()
    {
        var result = _parser.Parse("{{{param}}}");

        var argRef = result.EnumerateDescendants<ArgumentReference>().FirstOrDefault();
        argRef.Should().NotBeNull();
        argRef!.DefaultValue.Should().BeNull();
    }

    [Fact]
    public void Parse_ArgumentReferenceWithDefault_ReturnsArgumentReferenceWithDefault()
    {
        var result = _parser.Parse("{{{param|default}}}");

        var argRef = result.EnumerateDescendants<ArgumentReference>().FirstOrDefault();
        argRef.Should().NotBeNull();
        argRef!.DefaultValue.Should().NotBeNull();
    }

    [Fact]
    public void Parse_BoldText_ReturnsFormatSwitch()
    {
        var result = _parser.Parse("'''bold'''");

        var switches = result.EnumerateDescendants<FormatSwitch>().ToList();
        switches.Should().HaveCount(2);
        switches[0].SwitchBold.Should().BeTrue();
        switches[0].SwitchItalics.Should().BeFalse();
    }

    [Fact]
    public void Parse_ItalicText_ReturnsFormatSwitch()
    {
        var result = _parser.Parse("''italic''");

        var switches = result.EnumerateDescendants<FormatSwitch>().ToList();
        switches.Should().HaveCount(2);
        switches[0].SwitchBold.Should().BeFalse();
        switches[0].SwitchItalics.Should().BeTrue();
    }

    [Fact]
    public void Parse_Comment_ReturnsCommentNode()
    {
        var result = _parser.Parse("<!-- comment -->");

        var comment = result.EnumerateDescendants<Comment>().FirstOrDefault();
        comment.Should().NotBeNull();
        comment!.Content.Should().Be(" comment ");
    }

    [Theory]
    [InlineData("* Item", "*")]
    [InlineData("# Item", "#")]
    [InlineData(": Indent", ":")]
    [InlineData("; Term", ";")]
    [InlineData("** Nested", "**")]
    public void Parse_ListItem_ReturnsListItemWithCorrectPrefix(string wikitext, string expectedPrefix)
    {
        var result = _parser.Parse(wikitext);

        result.Lines.Should().HaveCount(1);
        result.Lines[0].Should().BeOfType<ListItem>();

        var listItem = (ListItem)result.Lines[0];
        listItem.Prefix.Should().Be(expectedPrefix);
    }

    [Fact]
    public void Parse_HtmlTag_ReturnsHtmlTagNode()
    {
        var result = _parser.Parse("<span class=\"test\">content</span>");

        var tag = result.EnumerateDescendants<HtmlTag>().FirstOrDefault();
        tag.Should().NotBeNull();
        tag!.Name.Should().Be("span");
        tag.Attributes.Should().HaveCount(1);
        tag.Content.Should().NotBeNull();
    }

    [Fact]
    public void Parse_SelfClosingTag_ReturnsSelfClosingHtmlTag()
    {
        var result = _parser.Parse("<br />");

        var tag = result.EnumerateDescendants<HtmlTag>().FirstOrDefault();
        tag.Should().NotBeNull();
        tag!.Name.Should().Be("br");
        tag.TagStyle.Should().Be(TagStyle.SelfClosing);
    }

    [Fact]
    public void Parse_ParserTag_ReturnsParserTagNode()
    {
        var result = _parser.Parse("<ref>citation</ref>");

        var tag = result.EnumerateDescendants<ParserTag>().FirstOrDefault();
        tag.Should().NotBeNull();
        tag!.Name.Should().Be("ref");
        tag.Content.Should().Be("citation");
    }

    [Fact]
    public void Parse_ComplexWikitext_ParsesAllElements()
    {
        var wikitext = @"== Introduction ==
This is a '''bold''' and ''italic'' text with a [[link]].

=== Details ===
* First item
* Second item with {{template|arg=value}}

{{Infobox
|name = Test
|description = A [[link|custom text]]
}}

See also: [https://example.com External Site]";

        var result = _parser.Parse(wikitext);

        result.EnumerateDescendants<Heading>().Should().HaveCount(2);
        result.EnumerateDescendants<WikiLink>().Should().HaveCountGreaterOrEqualTo(2);
        result.EnumerateDescendants<ListItem>().Should().HaveCount(2);
        result.EnumerateDescendants<Template>().Should().HaveCountGreaterOrEqualTo(2);
        result.EnumerateDescendants<ExternalLink>().Should().HaveCount(1);
    }

    [Fact]
    public void ToPlainText_WikiLink_ReturnsTargetOrDisplayText()
    {
        var parser = new WikitextParser();

        var result1 = parser.Parse("[[Article]]");
        result1.ToPlainText().Should().Contain("Article");

        var result2 = parser.Parse("[[Article|Display]]");
        result2.ToPlainText().Should().Contain("Display");
    }

    [Fact]
    public void ToPlainText_Template_ReturnsEmptyString()
    {
        var result = _parser.Parse("Hello {{template}} world");

        result.ToPlainText().Should().Be("Hello  world");
    }

    [Fact]
    public void ToPlainText_Comment_ReturnsEmptyString()
    {
        var result = _parser.Parse("Hello <!-- comment --> world");

        result.ToPlainText().Should().Be("Hello  world");
    }

    [Fact]
    public void ToString_ReturnsOriginalWikitext()
    {
        var wikitext = "== Heading ==";
        var result = _parser.Parse(wikitext);

        result.ToString().Should().Be(wikitext);
    }

    [Fact]
    public void Parse_WithCancellation_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = () => _parser.Parse("test", cts.Token);

        action.Should().Throw<OperationCanceledException>();
    }
}
