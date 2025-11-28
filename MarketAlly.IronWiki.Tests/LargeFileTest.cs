using MarketAlly.IronWiki;
using MarketAlly.IronWiki.Nodes;
using MarketAlly.IronWiki.Parsing;
using Xunit;

namespace MarketAlly.IronWiki.Tests;

public class LargeFileTest
{
    [Fact]
    public void Parse_WikiBridgeArticle_Succeeds()
    {
        // Arrange
        var filePath = Path.Combine(AppContext.BaseDirectory, "examples", "wiki_en_3397.txt");
        if (!File.Exists(filePath))
        {
            // Try relative path from test project
            filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "examples", "wiki_en_3397.txt");
        }

        Assert.True(File.Exists(filePath), $"Test file not found at {filePath}");

        var wikitext = File.ReadAllText(filePath);
        var parser = new WikitextParser();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = parser.Parse(wikitext);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Lines.Count > 0, "Document should have lines");

        // Output some stats
        var nodeCount = CountNodes(result);
        Console.WriteLine($"Parsed {wikitext.Length:N0} characters in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Total nodes: {nodeCount}");
        Console.WriteLine($"Lines/blocks: {result.Lines.Count}");

        // Count specific node types
        var templates = CountNodeType<Template>(result);
        var wikiLinks = CountNodeType<WikiLink>(result);
        var externalLinks = CountNodeType<ExternalLink>(result);
        var headings = result.Lines.OfType<Heading>().Count();
        var tables = result.Lines.OfType<Table>().Count();

        Console.WriteLine($"Templates: {templates}");
        Console.WriteLine($"Wiki links: {wikiLinks}");
        Console.WriteLine($"External links: {externalLinks}");
        Console.WriteLine($"Headings: {headings}");
        Console.WriteLine($"Tables: {tables}");

        // Verify round-trip
        var output = result.ToString();
        Console.WriteLine($"Output length: {output.Length:N0} characters");

        // The output should be similar in length (may differ slightly due to normalization)
        var lengthDiff = Math.Abs(output.Length - wikitext.Length);
        var percentDiff = (double)lengthDiff / wikitext.Length * 100;
        Console.WriteLine($"Length difference: {lengthDiff:N0} ({percentDiff:F2}%)");
    }

    [Fact]
    public void Parse_WikiAstrosArticleWithTables_Succeeds()
    {
        // Arrange - this article has wiki tables
        var filePath = Path.Combine(AppContext.BaseDirectory, "examples", "wiki_en_58817434.txt");
        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "examples", "wiki_en_58817434.txt");
        }

        Assert.True(File.Exists(filePath), $"Test file not found at {filePath}");

        var wikitext = File.ReadAllText(filePath);

        // Test the full file
        var testText = wikitext;
        Console.WriteLine($"Testing with {testText.Length:N0} characters");

        var parser = new WikitextParser();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = parser.Parse(testText);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Lines.Count > 0, "Document should have lines");

        // Output some stats
        var nodeCount = CountNodes(result);
        Console.WriteLine($"Parsed {testText.Length:N0} characters in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Total nodes: {nodeCount}");
        Console.WriteLine($"Lines/blocks: {result.Lines.Count}");

        // Count specific node types
        var templates = CountNodeType<Template>(result);
        var wikiLinks = CountNodeType<WikiLink>(result);
        var externalLinks = CountNodeType<ExternalLink>(result);
        var headings = result.Lines.OfType<Heading>().Count();
        var tables = result.Lines.OfType<Table>().Count();

        Console.WriteLine($"Templates: {templates}");
        Console.WriteLine($"Wiki links: {wikiLinks}");
        Console.WriteLine($"External links: {externalLinks}");
        Console.WriteLine($"Headings: {headings}");
        Console.WriteLine($"Tables: {tables}");

        // This article should have tables (in first 100 lines, table starts at line 48)
        Assert.True(tables > 0, "Article should contain wiki tables");

        // Verify round-trip
        var output = result.ToString();
        Console.WriteLine($"Output length: {output.Length:N0} characters");

        var lengthDiff = Math.Abs(output.Length - testText.Length);
        var percentDiff = (double)lengthDiff / testText.Length * 100;
        Console.WriteLine($"Length difference: {lengthDiff:N0} ({percentDiff:F2}%)");
    }

    [Fact]
    public void Parse_WithDiagnostics_CollectsRecoveryInfo()
    {
        // Arrange - parse a file that may require recovery
        var filePath = Path.Combine(AppContext.BaseDirectory, "examples", "wiki_en_58817434.txt");
        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "examples", "wiki_en_58817434.txt");
        }

        Assert.True(File.Exists(filePath), $"Test file not found at {filePath}");

        var wikitext = File.ReadAllText(filePath);
        var parser = new WikitextParser();
        var diagnostics = new List<ParsingDiagnostic>();

        // Act
        var result = parser.Parse(wikitext, diagnostics);

        // Assert
        Assert.NotNull(result);
        Console.WriteLine($"Total diagnostics: {diagnostics.Count}");
        foreach (var diag in diagnostics.Take(10))
        {
            Console.WriteLine(diag.ToString());
        }
        if (diagnostics.Count > 10)
        {
            Console.WriteLine($"... and {diagnostics.Count - 10} more");
        }
    }

    private static int CountNodes(WikiNode node)
    {
        var count = 1;
        foreach (var child in node.EnumerateChildren())
        {
            count += CountNodes(child);
        }
        return count;
    }

    private static int CountNodeType<T>(WikiNode node) where T : WikiNode
    {
        var count = node is T ? 1 : 0;
        foreach (var child in node.EnumerateChildren())
        {
            count += CountNodeType<T>(child);
        }
        return count;
    }
}
