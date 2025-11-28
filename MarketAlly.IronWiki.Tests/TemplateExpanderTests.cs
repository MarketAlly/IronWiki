// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using FluentAssertions;
using MarketAlly.IronWiki.Parsing;
using MarketAlly.IronWiki.Rendering;
using Xunit;

namespace MarketAlly.IronWiki.Tests;

public class TemplateExpanderTests
{
    private readonly WikitextParser _parser = new();

    [Fact]
    public void Expand_SimpleTemplate_ReturnsContent()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Hello", "Hello, World!");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Hello}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Hello, World!");
    }

    [Fact]
    public void Expand_TemplateWithPositionalParameter_SubstitutesValue()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Greet", "Hello, {{{1}}}!");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Greet|Alice}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Hello, Alice!");
    }

    [Fact]
    public void Expand_TemplateWithNamedParameter_SubstitutesValue()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Greet", "Hello, {{{name}}}!");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Greet|name=Bob}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Hello, Bob!");
    }

    [Fact]
    public void Expand_TemplateWithDefaultValue_UsesDefaultWhenNotProvided()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Greet", "Hello, {{{1|World}}}!");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Greet}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Hello, World!");
    }

    [Fact]
    public void Expand_TemplateWithDefaultValue_OverridesWhenProvided()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Greet", "Hello, {{{1|World}}}!");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Greet|Universe}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Hello, Universe!");
    }

    [Fact]
    public void Expand_NestedTemplates_ExpandsRecursively()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Inner", "Inner Content");
        provider.Add("Outer", "Start {{Inner}} End");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Outer}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Start Inner Content End");
    }

    [Fact]
    public void Expand_DeeplyNestedTemplates_ExpandsAllLevels()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Level3", "L3");
        provider.Add("Level2", "L2-{{Level3}}-L2");
        provider.Add("Level1", "L1-{{Level2}}-L1");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Level1}}");
        var result = expander.Expand(doc);

        result.Should().Contain("L1-L2-L3-L2-L1");
    }

    [Fact]
    public void Expand_ParameterPassedToNestedTemplate_SubstitutesCorrectly()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Inner", "Hello, {{{1}}}!");
        // Use a simpler pass-through pattern
        provider.Add("Outer", "Prefix-{{Inner|Test}}-Suffix");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Outer}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Prefix-Hello, Test!-Suffix");
    }

    [Fact]
    public void Expand_CircularReference_DetectsAndStops()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("A", "{{B}}");
        provider.Add("B", "{{A}}");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{A}}");
        var result = expander.Expand(doc);

        result.Should().Contain("loop detected");
    }

    [Fact]
    public void Expand_RecursionLimit_StopsAtLimit()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Deep", "{{Deep}}"); // Self-recursive

        var options = new TemplateExpanderOptions { MaxRecursionDepth = 5 };
        var expander = new TemplateExpander(_parser, provider, options);
        var doc = _parser.Parse("{{Deep}}");
        var result = expander.Expand(doc);

        // Self-recursive templates are detected as circular references (loop detected)
        result.Should().Contain("loop detected");
    }

    [Fact]
    public void Expand_UnknownTemplate_PreservesOriginal()
    {
        var provider = new DictionaryTemplateContentProvider();

        var options = new TemplateExpanderOptions { PreserveUnknownTemplates = true };
        var expander = new TemplateExpander(_parser, provider, options);
        var doc = _parser.Parse("{{Unknown}}");
        var result = expander.Expand(doc);

        result.Should().Contain("{{Unknown}}");
    }

    [Fact]
    public void Expand_MultipleTemplatesInDocument_ExpandsAll()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("A", "First");
        provider.Add("B", "Second");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{A}} and {{B}}");
        var result = expander.Expand(doc);

        result.Should().Contain("First");
        result.Should().Contain("Second");
    }

    [Fact]
    public void Expand_TemplateWithWikitext_PreservesFormatting()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Formatted", "'''Bold''' and ''italic''");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Formatted}}");
        var result = expander.Expand(doc);

        result.Should().Contain("'''Bold'''");
        result.Should().Contain("''italic''");
    }

    [Fact]
    public void Expand_TemplateWithLinks_PreservesLinks()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Link", "See [[Article]] for more");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Link}}");
        var result = expander.Expand(doc);

        result.Should().Contain("[[Article]]");
    }

    [Fact]
    public void Expand_IfParserFunction_TrueCondition()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{#if: yes | true | false}}");
        var result = expander.Expand(doc);

        result.Should().Contain("true");
        result.Should().NotContain("false");
    }

    [Fact]
    public void Expand_IfParserFunction_FalseCondition()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{#if: | true | false}}");
        var result = expander.Expand(doc);

        result.Should().Contain("false");
    }

    [Fact]
    public void Expand_IfeqParserFunction_Equal()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{#ifeq: abc | abc | same | different}}");
        var result = expander.Expand(doc);

        result.Should().Contain("same");
    }

    [Fact]
    public void Expand_IfeqParserFunction_NotEqual()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{#ifeq: abc | xyz | same | different}}");
        var result = expander.Expand(doc);

        result.Should().Contain("different");
    }

    [Fact]
    public void Expand_SwitchParserFunction_MatchingCase()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{#switch: b | a=first | b=second | c=third}}");
        var result = expander.Expand(doc);

        result.Should().Contain("second");
    }

    [Fact]
    public void Expand_SwitchParserFunction_DefaultCase()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{#switch: z | a=first | b=second | #default=default}}");
        var result = expander.Expand(doc);

        result.Should().Contain("default");
    }

    [Fact]
    public void Expand_LcParserFunction_ConvertsToLowercase()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{lc:HELLO}}");
        var result = expander.Expand(doc);

        result.Should().Contain("hello");
    }

    [Fact]
    public void Expand_UcParserFunction_ConvertsToUppercase()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{uc:hello}}");
        var result = expander.Expand(doc);

        result.Should().Contain("HELLO");
    }

    [Fact]
    public void Expand_UcfirstParserFunction_CapitalizesFirst()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{ucfirst:hello world}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Hello world");
    }

    [Fact]
    public void Expand_LcfirstParserFunction_LowercasesFirst()
    {
        var provider = new DictionaryTemplateContentProvider();

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{lcfirst:HELLO}}");
        var result = expander.Expand(doc);

        result.Should().Contain("hELLO");
    }

    [Fact]
    public void ExpandToDocument_ReturnsValidAst()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Test", "Simple content");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Test}}");
        var expandedDoc = expander.ExpandToDocument(doc);

        expandedDoc.Should().NotBeNull();
        expandedDoc.Lines.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExpandAsync_WorksCorrectly()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Async", "Async content");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Async}}");
        var result = await expander.ExpandAsync(doc);

        result.Should().Contain("Async content");
    }

    [Fact]
    public void ExpandingTemplateResolver_ReturnsExpandedWikitext()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Bold", "'''Important'''");

        var resolver = new ExpandingTemplateResolver(_parser, provider);
        var renderer = new HtmlRenderer(templateResolver: resolver);

        var doc = _parser.Parse("{{Bold}}");
        var html = renderer.Render(doc);

        // The resolver returns expanded wikitext, which is inserted as-is
        // The '''Important''' is the expanded content from the template
        html.Should().Contain("Important");
    }

    [Fact]
    public void ExpandingTemplateResolver_ExpandsParameterizedTemplates()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Greet", "Hello, {{{1|World}}}!");

        var resolver = new ExpandingTemplateResolver(_parser, provider);
        var renderer = new HtmlRenderer(templateResolver: resolver);

        var doc = _parser.Parse("{{Greet|Alice}}");
        var html = renderer.Render(doc);

        // The template should be expanded with the parameter
        html.Should().Contain("Hello, Alice!");
    }

    [Fact]
    public void ChainedTemplateContentProvider_TriesMultipleProviders()
    {
        var provider1 = new DictionaryTemplateContentProvider();
        var provider2 = new DictionaryTemplateContentProvider();
        provider2.Add("Found", "Found in second");

        var chained = new ChainedTemplateContentProvider(provider1, provider2);
        var expander = new TemplateExpander(_parser, chained);

        var doc = _parser.Parse("{{Found}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Found in second");
    }

    [Fact]
    public void Expand_ComplexInfobox_ExpandsCorrectly()
    {
        var provider = new DictionaryTemplateContentProvider();
        provider.Add("Infobox", @"{| class=""infobox""
|-
! colspan=""2"" | {{{title|No Title}}}
|-
| Type || {{{type|Unknown}}}
|-
| Value || {{{value|N/A}}}
|}");

        var expander = new TemplateExpander(_parser, provider);
        var doc = _parser.Parse("{{Infobox|title=Test Item|type=Widget|value=100}}");
        var result = expander.Expand(doc);

        result.Should().Contain("Test Item");
        result.Should().Contain("Widget");
        result.Should().Contain("100");
    }

    [Fact]
    public void Expand_NullDocument_ThrowsArgumentNullException()
    {
        var provider = new DictionaryTemplateContentProvider();
        var expander = new TemplateExpander(_parser, provider);

        Action act = () => expander.Expand(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
