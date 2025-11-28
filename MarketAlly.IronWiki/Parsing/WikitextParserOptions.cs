// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace MarketAlly.IronWiki.Parsing;

/// <summary>
/// Configuration options for the wikitext parser.
/// </summary>
public sealed class WikitextParserOptions
{
    private FrozenSet<string>? _parserTagsSet;
    private FrozenSet<string>? _selfClosingOnlyTagsSet;
    private FrozenSet<string>? _imageNamespacesSet;
    private FrozenSet<string>? _caseSensitiveMagicWordsSet;
    private FrozenSet<string>? _caseInsensitiveMagicWordsSet;
    private Regex? _imageNamespaceRegex;
    private bool _frozen;

    /// <summary>
    /// Gets the default parser tags.
    /// </summary>
    public static IReadOnlyList<string> DefaultParserTags { get; } =
    [
        // Built-ins
        "gallery", "includeonly", "noinclude", "nowiki", "onlyinclude", "pre",
        // Extensions
        "categorytree", "charinsert", "dynamicpagelist", "graph", "hiero", "imagemap",
        "indicator", "inputbox", "languages", "math", "poem", "ref", "references",
        "score", "section", "syntaxhighlight", "source", "templatedata", "timeline"
    ];

    /// <summary>
    /// Gets the default self-closing only tags.
    /// </summary>
    public static IReadOnlyList<string> DefaultSelfClosingOnlyTags { get; } =
    [
        "br", "wbr", "hr", "meta", "link"
    ];

    /// <summary>
    /// Gets the default image namespace names.
    /// </summary>
    public static IReadOnlyList<string> DefaultImageNamespaces { get; } =
    [
        "File", "Image"
    ];

    /// <summary>
    /// Gets the default case-insensitive magic words (parser functions).
    /// </summary>
    public static IReadOnlyList<string> DefaultCaseInsensitiveMagicWords { get; } =
    [
        "ARTICLEPATH", "PAGEID", "SERVER", "SERVERNAME", "SCRIPTPATH", "STYLEPATH",
        "NS", "NSE", "URLENCODE", "LCFIRST", "UCFIRST", "LC", "UC",
        "LOCALURL", "LOCALURLE", "FULLURL", "FULLURLE", "CANONICALURL", "CANONICALURLE",
        "FORMATNUM", "GRAMMAR", "GENDER", "PLURAL", "BIDI", "PADLEFT", "PADRIGHT",
        "ANCHORENCODE", "FILEPATH", "INT", "MSG", "RAW", "MSGNW", "SUBST"
    ];

    /// <summary>
    /// Gets the default case-sensitive magic words (variables).
    /// </summary>
    public static IReadOnlyList<string> DefaultCaseSensitiveMagicWords { get; } =
    [
        "!", "CURRENTMONTH", "CURRENTMONTH1", "CURRENTMONTHNAME", "CURRENTMONTHNAMEGEN",
        "CURRENTMONTHABBREV", "CURRENTDAY", "CURRENTDAY2", "CURRENTDAYNAME", "CURRENTYEAR",
        "CURRENTTIME", "CURRENTHOUR", "LOCALMONTH", "LOCALMONTH1", "LOCALMONTHNAME",
        "LOCALMONTHNAMEGEN", "LOCALMONTHABBREV", "LOCALDAY", "LOCALDAY2", "LOCALDAYNAME",
        "LOCALYEAR", "LOCALTIME", "LOCALHOUR", "NUMBEROFARTICLES", "NUMBEROFFILES",
        "NUMBEROFEDITS", "SITENAME", "PAGENAME", "PAGENAMEE", "FULLPAGENAME", "FULLPAGENAMEE",
        "NAMESPACE", "NAMESPACEE", "NAMESPACENUMBER", "CURRENTWEEK", "CURRENTDOW",
        "LOCALWEEK", "LOCALDOW", "REVISIONID", "REVISIONDAY", "REVISIONDAY2", "REVISIONMONTH",
        "REVISIONMONTH1", "REVISIONYEAR", "REVISIONTIMESTAMP", "REVISIONUSER", "REVISIONSIZE",
        "SUBPAGENAME", "SUBPAGENAMEE", "TALKSPACE", "TALKSPACEE", "SUBJECTSPACE", "SUBJECTSPACEE",
        "TALKPAGENAME", "TALKPAGENAMEE", "SUBJECTPAGENAME", "SUBJECTPAGENAMEE",
        "NUMBEROFUSERS", "NUMBEROFACTIVEUSERS", "NUMBEROFPAGES", "CURRENTVERSION",
        "ROOTPAGENAME", "ROOTPAGENAMEE", "BASEPAGENAME", "BASEPAGENAMEE",
        "CURRENTTIMESTAMP", "LOCALTIMESTAMP", "DIRECTIONMARK", "CONTENTLANGUAGE",
        "NUMBEROFADMINS", "CASCADINGSOURCES", "NUMBERINGROUP", "LANGUAGE",
        "DEFAULTSORT", "PAGESINCATEGORY", "PAGESIZE", "PROTECTIONLEVEL", "PROTECTIONEXPIRY",
        "DISPLAYTITLE", "DEFAULTSORTKEY", "DEFAULTCATEGORYSORT", "PAGESINNS"
    ];

    /// <summary>
    /// Gets or sets the list of parser tag names.
    /// </summary>
    public IReadOnlyList<string> ParserTags { get; set; } = DefaultParserTags;

    /// <summary>
    /// Gets or sets the list of self-closing only tag names.
    /// </summary>
    public IReadOnlyList<string> SelfClosingOnlyTags { get; set; } = DefaultSelfClosingOnlyTags;

    /// <summary>
    /// Gets or sets the list of image namespace names.
    /// </summary>
    public IReadOnlyList<string> ImageNamespaces { get; set; } = DefaultImageNamespaces;

    /// <summary>
    /// Gets or sets the list of case-insensitive magic words.
    /// </summary>
    public IReadOnlyList<string> CaseInsensitiveMagicWords { get; set; } = DefaultCaseInsensitiveMagicWords;

    /// <summary>
    /// Gets or sets the list of case-sensitive magic words.
    /// </summary>
    public IReadOnlyList<string> CaseSensitiveMagicWords { get; set; } = DefaultCaseSensitiveMagicWords;

    /// <summary>
    /// Gets or sets a value indicating whether to allow empty template names.
    /// </summary>
    public bool AllowEmptyTemplateName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow empty wiki link targets.
    /// </summary>
    public bool AllowEmptyWikiLinkTarget { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow empty external link targets.
    /// </summary>
    public bool AllowEmptyExternalLinkTarget { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to infer missing closing marks.
    /// </summary>
    public bool AllowClosingMarkInference { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to track source location information.
    /// </summary>
    public bool TrackSourceSpans { get; set; } = true;

    /// <summary>
    /// Creates a frozen copy of the options optimized for parsing.
    /// </summary>
    internal WikitextParserOptions Freeze()
    {
        if (_frozen)
        {
            return this;
        }

        var copy = new WikitextParserOptions
        {
            ParserTags = ParserTags,
            SelfClosingOnlyTags = SelfClosingOnlyTags,
            ImageNamespaces = ImageNamespaces,
            CaseInsensitiveMagicWords = CaseInsensitiveMagicWords,
            CaseSensitiveMagicWords = CaseSensitiveMagicWords,
            AllowEmptyTemplateName = AllowEmptyTemplateName,
            AllowEmptyWikiLinkTarget = AllowEmptyWikiLinkTarget,
            AllowEmptyExternalLinkTarget = AllowEmptyExternalLinkTarget,
            AllowClosingMarkInference = AllowClosingMarkInference,
            TrackSourceSpans = TrackSourceSpans,
            _frozen = true
        };

        copy._parserTagsSet = ParserTags.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        copy._selfClosingOnlyTagsSet = SelfClosingOnlyTags.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        copy._imageNamespacesSet = ImageNamespaces.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        copy._caseSensitiveMagicWordsSet = CaseSensitiveMagicWords.ToFrozenSet();
        copy._caseInsensitiveMagicWordsSet = CaseInsensitiveMagicWords.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        copy._imageNamespaceRegex = new Regex(
            string.Join("|", ImageNamespaces.Select(Regex.Escape)),
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        return copy;
    }

    /// <summary>
    /// Determines whether the specified tag name is a parser tag.
    /// </summary>
    internal bool IsParserTag(string tagName)
    {
        return _parserTagsSet?.Contains(tagName) ?? ParserTags.Contains(tagName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified tag name is self-closing only.
    /// </summary>
    internal bool IsSelfClosingOnlyTag(string tagName)
    {
        return _selfClosingOnlyTagsSet?.Contains(tagName) ?? SelfClosingOnlyTags.Contains(tagName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified namespace is an image namespace.
    /// </summary>
    internal bool IsImageNamespace(string ns)
    {
        return _imageNamespacesSet?.Contains(ns) ?? ImageNamespaces.Contains(ns, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified name is a magic word.
    /// </summary>
    internal bool IsMagicWord(string name)
    {
        if (_caseSensitiveMagicWordsSet is not null)
        {
            return _caseSensitiveMagicWordsSet.Contains(name) || _caseInsensitiveMagicWordsSet!.Contains(name);
        }
        return CaseSensitiveMagicWords.Contains(name) ||
               CaseInsensitiveMagicWords.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the regex pattern for matching image namespaces.
    /// </summary>
    internal string ImageNamespacePattern => _imageNamespaceRegex?.ToString() ??
        string.Join("|", ImageNamespaces.Select(Regex.Escape));
}
