# IronWiki

[![NuGet](https://img.shields.io/nuget/v/MarketAlly.IronWiki.svg)](https://www.nuget.org/packages/MarketAlly.IronWiki/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)

A production-ready MediaWiki wikitext parser and renderer for .NET. Parse wikitext into a full AST, render to HTML/Markdown/PlainText, expand templates with 40+ parser functions, and extract document metadata.

## Features

- **Complete Wikitext Parsing** - Full AST with source span tracking for all MediaWiki syntax
- **Multiple Renderers** - HTML, Markdown (GitHub-flavored), and PlainText output
- **Template Expansion** - Recursive expansion with 40+ parser functions (`#if`, `#switch`, `#expr`, `#time`, etc.)
- **Document Analysis** - Extract categories, sections, TOC, references, links, images, and templates
- **Security-First** - HTML sanitization, XSS prevention, safe tag whitelisting
- **Extensible** - Interfaces for custom template resolvers and image handlers
- **Modern .NET** - Targets .NET 9.0 with nullable reference types and async support

## Installation

```bash
dotnet add package MarketAlly.IronWiki
```

Or via the NuGet Package Manager:

```powershell
Install-Package MarketAlly.IronWiki
```

## Quick Start

### Basic Parsing and Rendering

```csharp
using MarketAlly.IronWiki.Parsing;
using MarketAlly.IronWiki.Rendering;

// Parse wikitext
var parser = new WikitextParser();
var document = parser.Parse("'''Hello''' ''World''! See [[Main Page]].");

// Render to HTML
var htmlRenderer = new HtmlRenderer();
string html = htmlRenderer.Render(document);
// Output: <p><b>Hello</b> <i>World</i>! See <a href="/wiki/Main_Page">Main Page</a>.</p>

// Render to Markdown
var markdownRenderer = new MarkdownRenderer();
string markdown = markdownRenderer.Render(document);
// Output: **Hello** *World*! See [Main Page](/wiki/Main_Page).

// Render to plain text
string plainText = document.ToPlainText();
// Output: Hello World! See Main Page.
```

### Template Expansion

```csharp
using MarketAlly.IronWiki.Parsing;
using MarketAlly.IronWiki.Rendering;

var parser = new WikitextParser();

// Create a template content provider
var provider = new DictionaryTemplateContentProvider();
provider.Add("Greeting", "Hello, {{{1|World}}}!");
provider.Add("Infobox", @"
{| class=""infobox""
|-
! {{{title}}}
|-
| Type: {{{type|Unknown}}}
|}");

// Expand templates
var expander = new TemplateExpander(parser, provider);
var document = parser.Parse("{{Greeting|Alice}} {{Infobox|title=Example|type=Demo}}");
string result = expander.Expand(document);
```

### Parser Functions

IronWiki supports 40+ MediaWiki parser functions:

```csharp
var parser = new WikitextParser();
var provider = new DictionaryTemplateContentProvider();
var expander = new TemplateExpander(parser, provider);

// Conditionals
var doc1 = parser.Parse("{{#if: yes | True | False}}");
expander.Expand(doc1); // "True"

// String case
var doc2 = parser.Parse("{{uc:hello world}}");
expander.Expand(doc2); // "HELLO WORLD"

// Math expressions
var doc3 = parser.Parse("{{#expr: 2 + 3 * 4}}");
expander.Expand(doc3); // "14"

// Switch statements
var doc4 = parser.Parse("{{#switch: b | a=First | b=Second | c=Third}}");
expander.Expand(doc4); // "Second"

// String manipulation
var doc5 = parser.Parse("{{#len:Hello}}");
expander.Expand(doc5); // "5"
```

**Supported Parser Functions:**
- **Conditionals:** `#if`, `#ifeq`, `#ifexpr`, `#ifexist`, `#iferror`, `#switch`
- **String Case:** `lc`, `uc`, `lcfirst`, `ucfirst`
- **String Functions:** `#len`, `#pos`, `#rpos`, `#sub`, `#replace`, `#explode`, `#pad`, `padleft`, `padright`
- **URL Functions:** `#urlencode`, `#urldecode`, `#anchorencode`, `fullurl`, `localurl`
- **Title Functions:** `#titleparts`, `ns`
- **Date/Time:** `#time`, `#timel`, `currentyear`, `currentmonth`, `currentday`, `currenttimestamp`, etc.
- **Math:** `#expr` (full expression evaluator with `+`, `-`, `*`, `/`, `^`, `mod`, parentheses)
- **Formatting:** `formatnum`, `plural`
- **Misc:** `#tag`, `!` (pipe escape)

### Document Analysis

Extract metadata from parsed documents:

```csharp
using MarketAlly.IronWiki.Parsing;
using MarketAlly.IronWiki.Analysis;

var parser = new WikitextParser();
var analyzer = new DocumentAnalyzer();

var document = parser.Parse(@"
#REDIRECT [[Target Page]]
");

// Or analyze a full article
var article = parser.Parse(@"
== Introduction ==
This article is about [[Topic]].

{{Infobox|title=Example}}

[[File:Example.jpg|thumb|A caption]]

== Details ==
More content here.<ref name=""source1"">Citation text</ref>

=== Subsection ===
Additional details.<ref>Another citation</ref>

== References ==
<references/>

[[Category:Examples]]
[[Category:Documentation|IronWiki]]
");

var metadata = analyzer.Analyze(article);

// Check for redirect
if (metadata.IsRedirect)
{
    Console.WriteLine($"Redirects to: {metadata.Redirect.Target}");
}

// Categories
foreach (var category in metadata.Categories)
{
    Console.WriteLine($"Category: {category.Name}, Sort Key: {category.SortKey}");
}

// Sections and Table of Contents
foreach (var section in metadata.Sections)
{
    Console.WriteLine($"Section: {section.Title} (Level {section.Level}, Anchor: {section.Anchor})");
}

// References
foreach (var reference in metadata.References)
{
    Console.WriteLine($"Ref #{reference.Number}: {reference.Content}");
}

// Links
Console.WriteLine($"Internal links: {metadata.InternalLinks.Count}");
Console.WriteLine($"External links: {metadata.ExternalLinks.Count}");
Console.WriteLine($"Images: {metadata.Images.Count}");
Console.WriteLine($"Templates: {metadata.Templates.Count}");

// Unique values
var linkedArticles = metadata.LinkedArticles; // Unique article titles
var templateNames = metadata.TemplateNames;   // Unique template names
var imageFiles = metadata.ImageFileNames;     // Unique image filenames
```

### Custom Template Resolution

Integrate with your own template sources:

```csharp
using MarketAlly.IronWiki.Rendering;
using MarketAlly.IronWiki.Nodes;

// Implement ITemplateContentProvider for raw wikitext
public class DatabaseTemplateProvider : ITemplateContentProvider
{
    private readonly IDatabase _db;

    public string? GetContent(string templateName)
    {
        return _db.GetTemplateWikitext(templateName);
    }

    public async Task<string?> GetContentAsync(string templateName, CancellationToken ct)
    {
        return await _db.GetTemplateWikitextAsync(templateName, ct);
    }
}

// Or implement ITemplateResolver for pre-rendered content
public class ApiTemplateResolver : ITemplateResolver
{
    public string? Resolve(Template template, RenderContext context)
    {
        // Call external API to expand template
        return CallMediaWikiApi(template);
    }
}

// Chain multiple providers with fallback
var provider = new ChainedTemplateContentProvider(
    new MemoryCacheProvider(cache),
    new DatabaseTemplateProvider(db),
    new WikiApiProvider(httpClient)
);

var expander = new TemplateExpander(parser, provider);
```

### Custom Image Resolution

Handle image URLs for your environment:

```csharp
using MarketAlly.IronWiki.Rendering;

// Simple pattern-based resolver
var imageResolver = new UrlPatternImageResolver(
    "https://upload.wikimedia.org/wikipedia/commons/{0}"
);

var renderer = new HtmlRenderer(imageResolver: imageResolver);

// Or implement custom logic
public class CustomImageResolver : IImageResolver
{
    public string? ResolveUrl(string fileName, int? width, int? height)
    {
        var hash = ComputeMd5Hash(fileName);
        return $"https://cdn.example.com/{hash[0]}/{hash[0..2]}/{fileName}";
    }
}
```

### HTML Rendering Options

```csharp
var options = new HtmlRenderOptions
{
    // Link generation
    ArticleUrlTemplate = "/wiki/{0}",

    // Template handling when no resolver provided
    TemplateOutputMode = TemplateOutputMode.Placeholder, // or Comment, Skip

    // Image handling when no resolver provided
    ImageOutputMode = ImageOutputMode.AltText, // or Placeholder, Skip

    // Table of contents
    GenerateTableOfContents = true,
    TocMinHeadings = 4,

    // Security (defaults are secure)
    AllowRawHtml = false,
    AllowedHtmlTags = ["span", "div", "abbr", "cite", "code", "data", "mark", "q", "s", "small", "sub", "sup", "time", "u", "var"],
    DisallowedAttributes = ["style", "class", "id"]
};

var renderer = new HtmlRenderer(options);
```

### Async Support

All major operations support async/await:

```csharp
// Async template expansion
var result = await expander.ExpandAsync(document, cancellationToken);

// Async template resolution
var resolver = new AsyncTemplateResolver();
var html = await resolver.ResolveAsync(template, context, cancellationToken);

// Async content provider
var content = await provider.GetContentAsync("Template:Example", cancellationToken);
```

### JSON Serialization

Serialize and deserialize the AST:

```csharp
using MarketAlly.IronWiki.Serialization;

// Serialize to JSON
var json = WikiJsonSerializer.Serialize(document, writeIndented: true);

// Or use extension method
var json2 = document.ToJson();

// Deserialize back
var restored = WikiJsonSerializer.DeserializeDocument(json);
```

### Error Handling

The parser provides diagnostics instead of throwing exceptions for malformed input:

```csharp
var diagnostics = new List<ParsingDiagnostic>();
var document = parser.Parse(wikitext, diagnostics);

foreach (var diagnostic in diagnostics)
{
    Console.WriteLine($"{diagnostic.Severity}: {diagnostic.Message} at position {diagnostic.Span}");
}
```

## Supported Wikitext Syntax

| Feature | Status | Notes |
|---------|--------|-------|
| **Formatting** | Full | Bold, italic, combined |
| **Headings** | Full | Levels 1-6 |
| **Links** | Full | Internal, external, interwiki, categories |
| **Images** | Full | All parameters (size, alignment, frame, caption) |
| **Lists** | Full | Ordered, unordered, definition lists |
| **Tables** | Full | Full syntax with attributes |
| **Templates** | Full | With parameter substitution |
| **Parser Functions** | 40+ | See list above |
| **Parser Tags** | Full | ref, references, nowiki, code, pre, math, gallery, etc. |
| **HTML Tags** | Sanitized | Safe subset with attribute filtering |
| **Comments** | Full | HTML comments |
| **Magic Words** | Partial | Date/time, namespaces |
| **Redirects** | Full | Detection and extraction |

## Architecture

```
MarketAlly.IronWiki/
├── Parsing/
│   ├── WikitextParser.cs      # Main parser entry point
│   ├── ParserCore.cs          # Core parsing engine
│   └── ParsingDiagnostic.cs   # Error reporting
├── Nodes/
│   ├── WikiNode.cs            # Base AST node
│   ├── BlockNodes.cs          # Paragraphs, headings, lists
│   ├── InlineNodes.cs         # Text, links, formatting
│   └── TableNodes.cs          # Table structure
├── Rendering/
│   ├── HtmlRenderer.cs        # HTML output
│   ├── MarkdownRenderer.cs    # Markdown output
│   ├── PlainTextRenderer.cs   # Text extraction
│   ├── TemplateExpander.cs    # Template processing
│   ├── ITemplateResolver.cs   # Template resolution interface
│   └── IImageResolver.cs      # Image URL interface
├── Analysis/
│   ├── DocumentAnalyzer.cs    # Metadata extraction
│   └── DocumentMetadata.cs    # Metadata models
└── Serialization/
    └── WikiJsonSerializer.cs  # JSON AST serialization
```

## Performance

- **Single-pass parsing** - Efficient recursive descent parser
- **Object pooling** - Reuses parser instances
- **Async support** - Non-blocking I/O for template resolution
- **Lazy evaluation** - Deferred processing where possible
- **StringBuilder** - Efficient string building throughout

## Security

IronWiki is designed with security in mind:

- **HTML Sanitization** - Only whitelisted tags allowed
- **Attribute Filtering** - Blocks `on*` event handlers, `javascript:` URLs
- **XSS Prevention** - Proper escaping of all user content
- **Safe Defaults** - Secure configuration out of the box

## Acknowledgments

This project draws significant inspiration from [MwParserFromScratch](https://github.com/CXuesong/MwParserFromScratch) by CXuesong. The original project provided an excellent foundation for understanding MediaWiki wikitext parsing in .NET. IronWiki builds upon these concepts with:

- Modern .NET 9.0 target
- Enhanced template expansion with 40+ parser functions
- Multiple renderer implementations (HTML, Markdown, PlainText)
- Comprehensive document analysis and metadata extraction
- Production-ready security features

We are grateful to CXuesong for their pioneering work in this space.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

```
MIT License

Copyright (c) 2024-2025 MarketAlly LLC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Author

**David H Friedel Jr.** - [MarketAlly LLC](https://github.com/MarketAlly)

## Links

- [GitHub Repository](https://github.com/MarketAlly/IronWiki)
- [NuGet Package](https://www.nuget.org/packages/MarketAlly.IronWiki/)
- [Issue Tracker](https://github.com/MarketAlly/IronWiki/issues)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

Please make sure to update tests as appropriate.

## See Also

- [MediaWiki Markup Specification](https://www.mediawiki.org/wiki/Markup_spec)
- [Help:Formatting](https://www.mediawiki.org/wiki/Help:Formatting)
- [Help:Tables](https://www.mediawiki.org/wiki/Help:Tables)
- [Help:Parser Functions](https://www.mediawiki.org/wiki/Help:Extension:ParserFunctions)
