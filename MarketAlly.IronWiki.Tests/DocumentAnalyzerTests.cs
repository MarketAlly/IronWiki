// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using FluentAssertions;
using MarketAlly.IronWiki.Analysis;
using MarketAlly.IronWiki.Parsing;
using Xunit;

namespace MarketAlly.IronWiki.Tests;

public class DocumentAnalyzerTests
{
    private readonly WikitextParser _parser = new();
    private readonly DocumentAnalyzer _analyzer = new();

    #region Redirect Tests

    [Fact]
    public void Analyze_RedirectPage_DetectsRedirect()
    {
        var doc = _parser.Parse("#REDIRECT [[Target Page]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.IsRedirect.Should().BeTrue();
        metadata.Redirect.Should().NotBeNull();
        metadata.Redirect!.Target.Should().Be("Target Page");
    }

    [Fact]
    public void Analyze_RedirectWithAnchor_IncludesAnchor()
    {
        var doc = _parser.Parse("#REDIRECT [[Target Page#Section]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.IsRedirect.Should().BeTrue();
        metadata.Redirect!.Target.Should().Be("Target Page#Section");
    }

    [Fact]
    public void Analyze_NormalPage_NotRedirect()
    {
        var doc = _parser.Parse("Normal content here.");
        var metadata = _analyzer.Analyze(doc);

        metadata.IsRedirect.Should().BeFalse();
        metadata.Redirect.Should().BeNull();
    }

    #endregion

    #region Category Tests

    [Fact]
    public void Analyze_SingleCategory_ExtractsCategory()
    {
        var doc = _parser.Parse("Content\n[[Category:Test Category]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Categories.Should().HaveCount(1);
        metadata.Categories[0].Name.Should().Be("Test Category");
    }

    [Fact]
    public void Analyze_CategoryWithSortKey_ExtractsSortKey()
    {
        var doc = _parser.Parse("[[Category:People|Smith, John]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Categories.Should().HaveCount(1);
        metadata.Categories[0].Name.Should().Be("People");
        metadata.Categories[0].SortKey.Should().Be("Smith, John");
    }

    [Fact]
    public void Analyze_MultipleCategories_ExtractsAll()
    {
        var doc = _parser.Parse("[[Category:Cat1]]\n[[Category:Cat2]]\n[[Category:Cat3]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Categories.Should().HaveCount(3);
        metadata.CategoryNames.Should().BeEquivalentTo(["Cat1", "Cat2", "Cat3"]);
    }

    [Fact]
    public void Analyze_CaseInsensitiveCategory_ExtractsName()
    {
        var doc = _parser.Parse("[[category:lowercase]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Categories.Should().HaveCount(1);
        metadata.Categories[0].Name.Should().Be("lowercase");
    }

    #endregion

    #region Section Tests

    [Fact]
    public void Analyze_SingleSection_ExtractsSection()
    {
        var doc = _parser.Parse("== Introduction ==\nContent here.");
        var metadata = _analyzer.Analyze(doc);

        metadata.Sections.Should().HaveCount(1);
        metadata.Sections[0].Title.Should().Be("Introduction");
        metadata.Sections[0].Level.Should().Be(2);
    }

    [Fact]
    public void Analyze_NestedSections_ExtractsHierarchy()
    {
        var doc = _parser.Parse("== Level 2 ==\n=== Level 3 ===\n==== Level 4 ====");
        var metadata = _analyzer.Analyze(doc);

        metadata.Sections.Should().HaveCount(3);
        metadata.Sections[0].Level.Should().Be(2);
        metadata.Sections[1].Level.Should().Be(3);
        metadata.Sections[2].Level.Should().Be(4);
    }

    [Fact]
    public void Analyze_Sections_GeneratesAnchors()
    {
        var doc = _parser.Parse("== My Section ==");
        var metadata = _analyzer.Analyze(doc);

        metadata.Sections[0].Anchor.Should().Be("My_Section");
    }

    [Fact]
    public void Analyze_SectionWithSpecialChars_GeneratesSafeAnchor()
    {
        var doc = _parser.Parse("== Section: Test & Example ==");
        var metadata = _analyzer.Analyze(doc);

        metadata.Sections[0].Anchor.Should().NotContain(":");
        metadata.Sections[0].Anchor.Should().NotContain("&");
    }

    [Fact]
    public void Analyze_DuplicateSectionTitles_GeneratesUniqueAnchors()
    {
        var doc = _parser.Parse("== Section ==\n== Section ==\n== Section ==");
        var metadata = _analyzer.Analyze(doc);

        var anchors = metadata.Sections.Select(s => s.Anchor).ToList();
        anchors.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Table of Contents Tests

    [Fact]
    public void Analyze_MultipleSections_GeneratesTOC()
    {
        var doc = _parser.Parse("== Section 1 ==\n=== Subsection ===\n== Section 2 ==");
        var metadata = _analyzer.Analyze(doc);

        metadata.TableOfContents.Should().NotBeNull();
        metadata.TableOfContents!.Entries.Should().NotBeEmpty();
    }

    [Fact]
    public void Analyze_TOC_ContainsCorrectHierarchy()
    {
        var doc = _parser.Parse("== Parent ==\n=== Child 1 ===\n=== Child 2 ===\n== Sibling ==");
        var metadata = _analyzer.Analyze(doc);

        metadata.TableOfContents.Should().NotBeNull();
        var toc = metadata.TableOfContents!;

        // Should have 2 top-level entries
        toc.Entries.Should().HaveCount(2);
        toc.Entries[0].Title.Should().Be("Parent");
        toc.Entries[0].Children.Should().HaveCount(2);
        toc.Entries[1].Title.Should().Be("Sibling");
    }

    [Fact]
    public void Analyze_TOC_FlatListContainsAllEntries()
    {
        var doc = _parser.Parse("== Section 1 ==\n=== Sub 1 ===\n== Section 2 ==");
        var metadata = _analyzer.Analyze(doc);

        var flatList = metadata.TableOfContents!.GetFlatList().ToList();
        flatList.Should().HaveCount(3);
    }

    #endregion

    #region Internal Link Tests

    [Fact]
    public void Analyze_InternalLink_ExtractsTarget()
    {
        var doc = _parser.Parse("See [[Article Name]] for more.");
        var metadata = _analyzer.Analyze(doc);

        metadata.InternalLinks.Should().HaveCount(1);
        metadata.InternalLinks[0].Target.Should().Be("Article Name");
        metadata.InternalLinks[0].Title.Should().Be("Article Name");
    }

    [Fact]
    public void Analyze_InternalLinkWithAnchor_ExtractsAnchor()
    {
        var doc = _parser.Parse("See [[Article#Section]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.InternalLinks[0].Target.Should().Contain("#Section");
        metadata.InternalLinks[0].Anchor.Should().Be("Section");
    }

    [Fact]
    public void Analyze_InternalLinkWithDisplayText_ExtractsDisplayText()
    {
        var doc = _parser.Parse("See [[Target|Display Text]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.InternalLinks[0].Target.Should().Be("Target");
        metadata.InternalLinks[0].DisplayText.Should().Be("Display Text");
    }

    [Fact]
    public void Analyze_NamespacedLink_ExtractsNamespace()
    {
        // Use a namespace that's not an interwiki prefix
        var doc = _parser.Parse("See [[Talk:Main Page]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.InternalLinks.Should().HaveCount(1);
        metadata.InternalLinks[0].Namespace.Should().Be("Talk");
        metadata.InternalLinks[0].Title.Should().Be("Main Page");
    }

    [Fact]
    public void Analyze_LinkedArticles_ReturnsUniqueTargets()
    {
        var doc = _parser.Parse("[[Article]] and [[Article]] again");
        var metadata = _analyzer.Analyze(doc);

        metadata.LinkedArticles.Should().HaveCount(1);
    }

    #endregion

    #region External Link Tests

    [Fact]
    public void Analyze_ExternalLink_ExtractsUrl()
    {
        var doc = _parser.Parse("[https://example.com Example Site]");
        var metadata = _analyzer.Analyze(doc);

        metadata.ExternalLinks.Should().HaveCount(1);
        metadata.ExternalLinks[0].Url.Should().Be("https://example.com");
        metadata.ExternalLinks[0].DisplayText.Should().Be("Example Site");
    }

    [Fact]
    public void Analyze_BareExternalLink_DetectsHasBrackets()
    {
        var doc = _parser.Parse("[https://example.com]");
        var metadata = _analyzer.Analyze(doc);

        metadata.ExternalLinks[0].HasBrackets.Should().BeTrue();
    }

    #endregion

    #region Image Tests

    [Fact]
    public void Analyze_Image_ExtractsFileName()
    {
        var doc = _parser.Parse("[[File:Example.jpg]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Images.Should().HaveCount(1);
        metadata.Images[0].FileName.Should().Be("Example.jpg");
    }

    [Fact]
    public void Analyze_ImageWithCaption_ExtractsCaption()
    {
        var doc = _parser.Parse("[[File:Example.jpg|thumb|A beautiful image]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Images[0].Caption.Should().Be("A beautiful image");
    }

    [Fact]
    public void Analyze_ImageWithSize_ExtractsSize()
    {
        var doc = _parser.Parse("[[File:Example.jpg|200px]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Images[0].Width.Should().Be(200);
    }

    [Fact]
    public void Analyze_ImageWithWidthAndHeight_ExtractsBoth()
    {
        var doc = _parser.Parse("[[File:Example.jpg|200x150px]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Images[0].Width.Should().Be(200);
        metadata.Images[0].Height.Should().Be(150);
    }

    [Fact]
    public void Analyze_ImageWithAlignment_ExtractsAlignment()
    {
        var doc = _parser.Parse("[[File:Example.jpg|right]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Images[0].Alignment.Should().Be("right");
    }

    [Fact]
    public void Analyze_ImageWithFrame_ExtractsFrame()
    {
        var doc = _parser.Parse("[[File:Example.jpg|thumb]]");
        var metadata = _analyzer.Analyze(doc);

        metadata.Images[0].Frame.Should().Be("thumb");
    }

    [Fact]
    public void Analyze_ImageFileNames_ReturnsUniqueNames()
    {
        var doc = _parser.Parse("[[File:A.jpg]] and [[File:A.jpg]] again");
        var metadata = _analyzer.Analyze(doc);

        metadata.ImageFileNames.Should().HaveCount(1);
    }

    #endregion

    #region Template Tests

    [Fact]
    public void Analyze_Template_ExtractsName()
    {
        var doc = _parser.Parse("{{Infobox}}");
        var metadata = _analyzer.Analyze(doc);

        metadata.Templates.Should().HaveCount(1);
        metadata.Templates[0].Name.Should().Be("Infobox");
    }

    [Fact]
    public void Analyze_TemplateWithNamedArgs_ExtractsArguments()
    {
        var doc = _parser.Parse("{{Infobox|title=Test|type=Example}}");
        var metadata = _analyzer.Analyze(doc);

        metadata.Templates[0].Arguments.Should().ContainKey("title");
        metadata.Templates[0].Arguments["title"].Should().Be("Test");
    }

    [Fact]
    public void Analyze_TemplateWithPositionalArgs_ExtractsAsNumbers()
    {
        var doc = _parser.Parse("{{Template|First|Second}}");
        var metadata = _analyzer.Analyze(doc);

        metadata.Templates[0].Arguments.Should().ContainKey("1");
        metadata.Templates[0].Arguments["1"].Should().Be("First");
        metadata.Templates[0].Arguments["2"].Should().Be("Second");
    }

    [Fact]
    public void Analyze_ParserFunction_MarksMagicWord()
    {
        var doc = _parser.Parse("{{#if:yes|true|false}}");
        var metadata = _analyzer.Analyze(doc);

        metadata.Templates[0].IsMagicWord.Should().BeTrue();
    }

    [Fact]
    public void Analyze_TemplateNames_ReturnsUniqueNames()
    {
        var doc = _parser.Parse("{{A}} {{B}} {{A}}");
        var metadata = _analyzer.Analyze(doc);

        metadata.TemplateNames.Should().BeEquivalentTo(["A", "B"]);
    }

    #endregion

    #region Reference Tests

    [Fact]
    public void Analyze_Reference_ExtractsContent()
    {
        var doc = _parser.Parse("Text<ref>Citation here</ref> more text.");
        var metadata = _analyzer.Analyze(doc);

        metadata.References.Should().HaveCount(1);
        metadata.References[0].Content.Should().Be("Citation here");
    }

    [Fact]
    public void Analyze_NamedReference_ExtractsName()
    {
        var doc = _parser.Parse("Text<ref name=\"source1\">Citation</ref>");
        var metadata = _analyzer.Analyze(doc);

        metadata.References[0].Name.Should().Be("source1");
    }

    [Fact]
    public void Analyze_ReferencesSection_SetsHasReferencesSection()
    {
        // HasReferencesSection is set when a <references/> or <references> tag is found
        var doc = _parser.Parse("Text<ref>Citation</ref>\n== References ==\n<references/>");
        var metadata = _analyzer.Analyze(doc);

        metadata.HasReferencesSection.Should().BeTrue();
    }

    [Fact]
    public void Analyze_MultipleReferences_AssignsNumbers()
    {
        var doc = _parser.Parse("<ref>First</ref> <ref>Second</ref> <ref>Third</ref>");
        var metadata = _analyzer.Analyze(doc);

        metadata.References.Should().HaveCount(3);
        metadata.References[0].Number.Should().Be(1);
        metadata.References[1].Number.Should().Be(2);
        metadata.References[2].Number.Should().Be(3);
    }

    [Fact]
    public void Analyze_ReferenceGroup_ExtractsGroup()
    {
        var doc = _parser.Parse("<ref group=\"notes\">A note</ref>");
        var metadata = _analyzer.Analyze(doc);

        metadata.References[0].Group.Should().Be("notes");
    }

    #endregion

    #region Language Link Tests

    [Fact]
    public void Analyze_LanguageLink_ExtractsLanguageCode()
    {
        var analyzer = new DocumentAnalyzer(new DocumentAnalyzerOptions
        {
            LanguageCodes = ["de", "fr", "es"]
        });

        var doc = _parser.Parse("[[de:German Article]]");
        var metadata = analyzer.Analyze(doc);

        metadata.LanguageLinks.Should().HaveCount(1);
        metadata.LanguageLinks[0].LanguageCode.Should().Be("de");
        metadata.LanguageLinks[0].Title.Should().Be("German Article");
    }

    [Fact]
    public void Analyze_MultipleLanguageLinks_ExtractsAll()
    {
        var analyzer = new DocumentAnalyzer(new DocumentAnalyzerOptions
        {
            LanguageCodes = ["de", "fr", "es"]
        });

        var doc = _parser.Parse("[[de:German]]\n[[fr:French]]\n[[es:Spanish]]");
        var metadata = analyzer.Analyze(doc);

        metadata.LanguageLinks.Should().HaveCount(3);
    }

    #endregion

    #region Interwiki Link Tests

    [Fact]
    public void Analyze_InterwikiLink_ExtractsPrefix()
    {
        var analyzer = new DocumentAnalyzer(new DocumentAnalyzerOptions
        {
            InterwikiPrefixes = ["wikt", "commons", "meta"]
        });

        var doc = _parser.Parse("[[wikt:example]]");
        var metadata = analyzer.Analyze(doc);

        metadata.InterwikiLinks.Should().HaveCount(1);
        metadata.InterwikiLinks[0].Prefix.Should().Be("wikt");
        metadata.InterwikiLinks[0].Title.Should().Be("example");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Analyze_EmptyDocument_ReturnsEmptyMetadata()
    {
        var doc = _parser.Parse("");
        var metadata = _analyzer.Analyze(doc);

        metadata.Categories.Should().BeEmpty();
        metadata.Sections.Should().BeEmpty();
        metadata.InternalLinks.Should().BeEmpty();
        metadata.Templates.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_ComplexDocument_ExtractsAllMetadata()
    {
        var wikitext = @"
== Introduction ==
This is an article about [[Topic]] with [[File:Example.jpg|thumb|Caption]].

{{Infobox|title=Test}}

See also [[Related Article]].

== Details ==
More content<ref>Source citation</ref>.

=== Subsection ===
Final content.

[[Category:Main Category]]
[[Category:Secondary Category]]
";
        var doc = _parser.Parse(wikitext);
        var metadata = _analyzer.Analyze(doc);

        metadata.Sections.Should().HaveCount(3);
        metadata.Categories.Should().HaveCount(2);
        metadata.InternalLinks.Should().HaveCountGreaterThan(0);
        metadata.Images.Should().HaveCount(1);
        metadata.Templates.Should().HaveCount(1);
        metadata.References.Should().HaveCount(1);
        metadata.TableOfContents.Should().NotBeNull();
    }

    [Fact]
    public void Analyze_NullDocument_ThrowsArgumentNullException()
    {
        Action act = () => _analyzer.Analyze(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
