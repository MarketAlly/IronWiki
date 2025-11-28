// Copyright (c) MarketAlly LLC. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using MarketAlly.IronWiki.Nodes;
using MarketAlly.IronWiki.Parsing;

#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable CA1307 // Specify StringComparison for clarity
#pragma warning disable CA1308 // Normalize strings to uppercase
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CA1849 // Call async methods when in async method
#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter

namespace MarketAlly.IronWiki.Rendering;

/// <summary>
/// Expands templates by substituting parameters and recursively processing nested templates.
/// </summary>
/// <remarks>
/// <para>The TemplateExpander performs full MediaWiki-style template expansion:</para>
/// <list type="bullet">
/// <item>Fetches template content from an <see cref="ITemplateContentProvider"/></item>
/// <item>Substitutes parameter references ({{{1}}}, {{{name}}}, {{{arg|default}}})</item>
/// <item>Recursively expands nested templates</item>
/// <item>Handles recursion limits to prevent infinite loops</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var parser = new WikitextParser();
/// var provider = new DictionaryTemplateContentProvider();
/// provider.Add("Hello", "Hello, {{{1|World}}}!");
/// provider.Add("Greeting", "{{Hello|{{{name}}}}}");
///
/// var expander = new TemplateExpander(parser, provider);
/// var doc = parser.Parse("{{Greeting|name=Alice}}");
/// var expanded = expander.Expand(doc);
/// // Result: "Hello, Alice!"
/// </code>
/// </example>
public class TemplateExpander
{
    private readonly WikitextParser _parser;
    private readonly ITemplateContentProvider _contentProvider;
    private readonly TemplateExpanderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateExpander"/> class.
    /// </summary>
    /// <param name="parser">The parser to use for parsing template content.</param>
    /// <param name="contentProvider">The provider for template content.</param>
    /// <param name="options">Optional expansion options.</param>
    public TemplateExpander(
        WikitextParser parser,
        ITemplateContentProvider contentProvider,
        TemplateExpanderOptions? options = null)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _options = options ?? new TemplateExpanderOptions();
    }

    /// <summary>
    /// Expands all templates in a document, returning the expanded wikitext as a string.
    /// </summary>
    /// <param name="document">The document containing templates to expand.</param>
    /// <param name="context">Optional expansion context.</param>
    /// <returns>The expanded wikitext as a string.</returns>
    public string Expand(WikitextDocument document, TemplateExpansionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        context ??= new TemplateExpansionContext();

        var sb = new StringBuilder();
        ExpandNode(document, context, sb);
        return sb.ToString();
    }

    /// <summary>
    /// Expands all templates in a document, returning the expanded document AST.
    /// </summary>
    /// <param name="document">The document containing templates to expand.</param>
    /// <param name="context">Optional expansion context.</param>
    /// <returns>A new document with all templates expanded.</returns>
    public WikitextDocument ExpandToDocument(WikitextDocument document, TemplateExpansionContext? context = null)
    {
        var expandedText = Expand(document, context);
        return _parser.Parse(expandedText);
    }

    /// <summary>
    /// Asynchronously expands all templates in a document.
    /// </summary>
    /// <param name="document">The document containing templates to expand.</param>
    /// <param name="context">Optional expansion context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The expanded wikitext as a string.</returns>
    public async Task<string> ExpandAsync(
        WikitextDocument document,
        TemplateExpansionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        context ??= new TemplateExpansionContext();

        var sb = new StringBuilder();
        await ExpandNodeAsync(document, context, sb, cancellationToken).ConfigureAwait(false);
        return sb.ToString();
    }

    private void ExpandNode(WikiNode node, TemplateExpansionContext context, StringBuilder sb)
    {
        switch (node)
        {
            case WikitextDocument doc:
                foreach (var line in doc.Lines)
                {
                    ExpandNode(line, context, sb);
                }
                break;

            case Template template:
                ExpandTemplate(template, context, sb);
                break;

            case ArgumentReference argRef:
                ExpandArgumentReference(argRef, context, sb);
                break;

            case Paragraph para:
                foreach (var inline in para.Inlines)
                {
                    ExpandNode(inline, context, sb);
                }
                if (!para.Compact)
                {
                    sb.AppendLine();
                }
                break;

            case Heading heading:
                var equals = new string('=', heading.Level);
                sb.Append(equals);
                sb.Append(' ');
                foreach (var inline in heading.Inlines)
                {
                    ExpandNode(inline, context, sb);
                }
                sb.Append(' ');
                sb.Append(equals);
                sb.AppendLine();
                break;

            case ListItem listItem:
                sb.Append(listItem.Prefix);
                foreach (var inline in listItem.Inlines)
                {
                    ExpandNode(inline, context, sb);
                }
                sb.AppendLine();
                break;

            case HorizontalRule:
                sb.AppendLine("----");
                break;

            case Table table:
                ExpandTable(table, context, sb);
                break;

            case WikiLink wikiLink:
                sb.Append("[[");
                if (wikiLink.Target is not null)
                {
                    ExpandNode(wikiLink.Target, context, sb);
                }
                if (wikiLink.Text is not null)
                {
                    sb.Append('|');
                    ExpandNode(wikiLink.Text, context, sb);
                }
                sb.Append("]]");
                break;

            case ExternalLink extLink:
                if (extLink.HasBrackets)
                {
                    sb.Append('[');
                }
                if (extLink.Target is not null)
                {
                    ExpandNode(extLink.Target, context, sb);
                }
                if (extLink.Text is not null)
                {
                    sb.Append(' ');
                    ExpandNode(extLink.Text, context, sb);
                }
                if (extLink.HasBrackets)
                {
                    sb.Append(']');
                }
                break;

            case ImageLink imageLink:
                sb.Append("[[");
                ExpandNode(imageLink.Target, context, sb);
                foreach (var arg in imageLink.Arguments)
                {
                    sb.Append('|');
                    if (arg.Name is not null)
                    {
                        ExpandNode(arg.Name, context, sb);
                        sb.Append('=');
                    }
                    ExpandNode(arg.Value, context, sb);
                }
                sb.Append("]]");
                break;

            case Run run:
                foreach (var inline in run.Inlines)
                {
                    ExpandNode(inline, context, sb);
                }
                break;

            case PlainText plainText:
                sb.Append(plainText.Content);
                break;

            case FormatSwitch formatSwitch:
                sb.Append(formatSwitch.ToString());
                break;

            case Comment comment:
                sb.Append("<!--");
                sb.Append(comment.Content);
                sb.Append("-->");
                break;

            case HtmlTag htmlTag:
                sb.Append(htmlTag.ToString());
                break;

            case ParserTag parserTag:
                // Use ToString() which handles all the complexity
                sb.Append(parserTag.ToString());
                break;

            default:
                // For any unhandled node types, use their ToString() representation
                sb.Append(node.ToString());
                break;
        }
    }

    private void ExpandTable(Table table, TemplateExpansionContext context, StringBuilder sb)
    {
        sb.Append("{|");
        foreach (var attr in table.Attributes)
        {
            sb.Append(attr.ToString());
        }
        sb.Append(table.AttributeTrailingWhitespace);
        sb.AppendLine();

        if (table.Caption is not null)
        {
            sb.Append("|+");
            if (table.Caption.Content is not null)
            {
                ExpandNode(table.Caption.Content, context, sb);
            }
            sb.AppendLine();
        }

        foreach (var row in table.Rows)
        {
            if (row.HasExplicitRowMarker)
            {
                sb.Append("|-");
                foreach (var attr in row.Attributes)
                {
                    sb.Append(attr.ToString());
                }
                sb.Append(row.AttributeTrailingWhitespace);
                sb.AppendLine();
            }

            foreach (var cell in row.Cells)
            {
                var marker = cell.IsHeader ? '!' : '|';
                if (cell.IsInlineSibling)
                {
                    sb.Append(marker);
                    sb.Append(marker);
                }
                else
                {
                    sb.Append(marker);
                }

                if (cell.HasAttributePipe)
                {
                    foreach (var attr in cell.Attributes)
                    {
                        sb.Append(attr.ToString());
                    }
                    sb.Append(cell.AttributeTrailingWhitespace);
                    sb.Append('|');
                }

                if (cell.Content is not null)
                {
                    ExpandNode(cell.Content, context, sb);
                }

                if (!cell.IsInlineSibling)
                {
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("|}");
    }

    private void ExpandTemplate(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // Check recursion limit
        if (context.Depth >= _options.MaxRecursionDepth)
        {
            sb.Append(_options.RecursionLimitMessage);
            return;
        }

        // Get template name
        var templateName = template.Name?.ToString().Trim();
        if (string.IsNullOrEmpty(templateName))
        {
            sb.Append(template.ToString());
            return;
        }

        // Check for circular reference
        if (context.ExpandingTemplates.Contains(templateName, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(_options.CircularReferenceMessage);
            return;
        }

        // Handle magic words / parser functions
        if (template.IsMagicWord || templateName.StartsWith('#'))
        {
            ExpandMagicWord(template, templateName, context, sb);
            return;
        }

        // Get template content
        var content = _contentProvider.GetContent(templateName);
        if (content is null)
        {
            // Template not found - output as-is or placeholder
            if (_options.PreserveUnknownTemplates)
            {
                sb.Append(template.ToString());
            }
            else
            {
                sb.Append("{{");
                sb.Append(templateName);
                sb.Append("}}");
            }
            return;
        }

        // Build argument dictionary
        var arguments = BuildArgumentDictionary(template);

        // Create child context
        var childContext = context.CreateChildContext(templateName, arguments);

        // Parse and expand template content
        var templateDoc = _parser.Parse(content);
        ExpandNode(templateDoc, childContext, sb);
    }

    private void ExpandMagicWord(Template template, string name, TemplateExpansionContext context, StringBuilder sb)
    {
        // Handle common parser functions
        var lowerName = name.ToLowerInvariant();

        // Remove trailing colon if present
        if (lowerName.EndsWith(':'))
        {
            lowerName = lowerName[..^1];
        }

        switch (lowerName)
        {
            // Conditional functions
            case "#if":
                ExpandIfFunction(template, context, sb);
                break;

            case "#ifeq":
                ExpandIfEqFunction(template, context, sb);
                break;

            case "#ifexpr":
                ExpandIfExprFunction(template, context, sb);
                break;

            case "#ifexist":
                ExpandIfExistFunction(template, context, sb);
                break;

            case "#iferror":
                ExpandIfErrorFunction(template, context, sb);
                break;

            case "#switch":
                ExpandSwitchFunction(template, context, sb);
                break;

            // Expression evaluation
            case "#expr":
                ExpandExprFunction(template, context, sb);
                break;

            // String functions
            case "lc":
                if (template.Arguments.Count > 0)
                {
                    var value = GetExpandedArgumentValue(template.Arguments[0], context);
                    sb.Append(value.ToLowerInvariant());
                }
                break;

            case "uc":
                if (template.Arguments.Count > 0)
                {
                    var value = GetExpandedArgumentValue(template.Arguments[0], context);
                    sb.Append(value.ToUpperInvariant());
                }
                break;

            case "lcfirst":
                if (template.Arguments.Count > 0)
                {
                    var value = GetExpandedArgumentValue(template.Arguments[0], context);
                    if (value.Length > 0)
                    {
                        sb.Append(char.ToLowerInvariant(value[0]));
                        sb.Append(value.AsSpan(1));
                    }
                }
                break;

            case "ucfirst":
                if (template.Arguments.Count > 0)
                {
                    var value = GetExpandedArgumentValue(template.Arguments[0], context);
                    if (value.Length > 0)
                    {
                        sb.Append(char.ToUpperInvariant(value[0]));
                        sb.Append(value.AsSpan(1));
                    }
                }
                break;

            case "#len":
                if (template.Arguments.Count > 0)
                {
                    var value = GetExpandedArgumentValue(template.Arguments[0], context);
                    sb.Append(value.Length);
                }
                break;

            case "#pos":
                ExpandPosFunction(template, context, sb);
                break;

            case "#rpos":
                ExpandRPosFunction(template, context, sb);
                break;

            case "#sub":
                ExpandSubFunction(template, context, sb);
                break;

            case "#pad":
            case "padleft":
                ExpandPadLeftFunction(template, context, sb);
                break;

            case "padright":
                ExpandPadRightFunction(template, context, sb);
                break;

            case "#replace":
                ExpandReplaceFunction(template, context, sb);
                break;

            case "#explode":
                ExpandExplodeFunction(template, context, sb);
                break;

            case "#urlencode":
            case "urlencode":
                if (template.Arguments.Count > 0)
                {
                    var value = GetExpandedArgumentValue(template.Arguments[0], context);
                    sb.Append(Uri.EscapeDataString(value));
                }
                break;

            case "#urldecode":
                if (template.Arguments.Count > 0)
                {
                    var value = GetExpandedArgumentValue(template.Arguments[0], context);
                    sb.Append(Uri.UnescapeDataString(value));
                }
                break;

            case "#anchorencode":
            case "anchorencode":
                if (template.Arguments.Count > 0)
                {
                    var value = GetExpandedArgumentValue(template.Arguments[0], context);
                    sb.Append(AnchorEncode(value));
                }
                break;

            // Title functions
            case "#titleparts":
                ExpandTitlePartsFunction(template, context, sb);
                break;

            case "ns":
                ExpandNsFunction(template, context, sb);
                break;

            case "fullurl":
            case "#fullurl":
                ExpandFullUrlFunction(template, context, sb, false);
                break;

            case "localurl":
            case "#localurl":
                ExpandFullUrlFunction(template, context, sb, true);
                break;

            // Date/time functions
            case "#time":
            case "#formatdate":
                ExpandTimeFunction(template, context, sb);
                break;

            case "#timel":
                ExpandTimeFunction(template, context, sb, useLocal: true);
                break;

            case "currentyear":
                sb.Append(DateTime.UtcNow.Year);
                break;

            case "currentmonth":
                sb.Append(DateTime.UtcNow.Month.ToString("D2"));
                break;

            case "currentmonth1":
                sb.Append(DateTime.UtcNow.Month);
                break;

            case "currentmonthname":
                sb.Append(DateTime.UtcNow.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture));
                break;

            case "currentmonthabbrev":
                sb.Append(DateTime.UtcNow.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture));
                break;

            case "currentday":
                sb.Append(DateTime.UtcNow.Day);
                break;

            case "currentday2":
                sb.Append(DateTime.UtcNow.Day.ToString("D2"));
                break;

            case "currentdow":
                sb.Append((int)DateTime.UtcNow.DayOfWeek);
                break;

            case "currentdayname":
                sb.Append(DateTime.UtcNow.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture));
                break;

            case "currenttime":
                sb.Append(DateTime.UtcNow.ToString("HH:mm"));
                break;

            case "currenthour":
                sb.Append(DateTime.UtcNow.Hour.ToString("D2"));
                break;

            case "currentweek":
                sb.Append(System.Globalization.ISOWeek.GetWeekOfYear(DateTime.UtcNow));
                break;

            case "currenttimestamp":
                sb.Append(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                break;

            // Math functions
            case "#tag":
                ExpandTagFunction(template, context, sb);
                break;

            case "formatnum":
                ExpandFormatNumFunction(template, context, sb);
                break;

            case "plural":
            case "#plural":
                ExpandPluralFunction(template, context, sb);
                break;

            // Misc
            case "#invoke":
                // Lua module invocation - not supported, output placeholder
                sb.Append("{{#invoke:");
                if (template.Arguments.Count > 0)
                {
                    sb.Append(GetExpandedArgumentValue(template.Arguments[0], context));
                }
                sb.Append("}}");
                break;

            case "#property":
            case "#statements":
                // Wikidata - not supported, output empty
                break;

            case "!":
                // Escape for pipe character
                sb.Append('|');
                break;

            default:
                // Unknown magic word - preserve as-is
                sb.Append(template.ToString());
                break;
        }
    }

    private void ExpandExprFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        if (template.Arguments.Count == 0) return;

        var expr = GetExpandedArgumentValue(template.Arguments[0], context).Trim();
        try
        {
            var result = EvaluateExpression(expr);
            sb.Append(FormatExprResult(result));
        }
        catch
        {
            sb.Append("<strong class=\"error\">Expression error</strong>");
        }
    }

    private void ExpandIfExprFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#ifexpr: expression | then | else}}
        if (template.Arguments.Count == 0) return;

        var expr = GetExpandedArgumentValue(template.Arguments[0], context).Trim();
        bool isTrue;

        try
        {
            var result = EvaluateExpression(expr);
            isTrue = result != 0;
        }
        catch
        {
            isTrue = false;
        }

        if (isTrue && template.Arguments.Count > 1)
        {
            ExpandNode(template.Arguments[1].Value, context, sb);
        }
        else if (!isTrue && template.Arguments.Count > 2)
        {
            ExpandNode(template.Arguments[2].Value, context, sb);
        }
    }

    private void ExpandIfExistFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#ifexist: page | then | else}} - without API access, always assume false
        if (template.Arguments.Count > 2)
        {
            ExpandNode(template.Arguments[2].Value, context, sb);
        }
    }

    private void ExpandIfErrorFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#iferror: test | then | else}}
        if (template.Arguments.Count == 0) return;

        var test = GetExpandedArgumentValue(template.Arguments[0], context);
        var hasError = test.Contains("<strong class=\"error\"") || test.Contains("error");

        if (hasError && template.Arguments.Count > 1)
        {
            ExpandNode(template.Arguments[1].Value, context, sb);
        }
        else if (!hasError)
        {
            if (template.Arguments.Count > 2)
            {
                ExpandNode(template.Arguments[2].Value, context, sb);
            }
            else
            {
                sb.Append(test);
            }
        }
    }

    private void ExpandPosFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#pos:string|search|offset}}
        if (template.Arguments.Count < 2) return;

        var text = GetExpandedArgumentValue(template.Arguments[0], context);
        var search = GetExpandedArgumentValue(template.Arguments[1], context);
        var offset = 0;

        if (template.Arguments.Count > 2 && int.TryParse(GetExpandedArgumentValue(template.Arguments[2], context), out var o))
        {
            offset = o;
        }

        var pos = text.IndexOf(search, offset, StringComparison.Ordinal);
        if (pos >= 0)
        {
            sb.Append(pos);
        }
    }

    private void ExpandRPosFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#rpos:string|search}}
        if (template.Arguments.Count < 2) return;

        var text = GetExpandedArgumentValue(template.Arguments[0], context);
        var search = GetExpandedArgumentValue(template.Arguments[1], context);

        var pos = text.LastIndexOf(search, StringComparison.Ordinal);
        sb.Append(pos);
    }

    private void ExpandSubFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#sub:string|start|length}}
        if (template.Arguments.Count == 0) return;

        var text = GetExpandedArgumentValue(template.Arguments[0], context);
        var start = 0;
        var length = text.Length;

        if (template.Arguments.Count > 1 && int.TryParse(GetExpandedArgumentValue(template.Arguments[1], context), out var s))
        {
            start = s < 0 ? Math.Max(0, text.Length + s) : Math.Min(s, text.Length);
        }

        if (template.Arguments.Count > 2 && int.TryParse(GetExpandedArgumentValue(template.Arguments[2], context), out var l))
        {
            length = l < 0 ? Math.Max(0, text.Length - start + l) : l;
        }

        if (start < text.Length)
        {
            sb.Append(text.AsSpan(start, Math.Min(length, text.Length - start)));
        }
    }

    private void ExpandPadLeftFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{padleft:string|length|pad}}
        if (template.Arguments.Count == 0) return;

        var text = GetExpandedArgumentValue(template.Arguments[0], context);
        var length = text.Length;
        var pad = "0";

        if (template.Arguments.Count > 1 && int.TryParse(GetExpandedArgumentValue(template.Arguments[1], context), out var l))
        {
            length = l;
        }

        if (template.Arguments.Count > 2)
        {
            var p = GetExpandedArgumentValue(template.Arguments[2], context);
            if (!string.IsNullOrEmpty(p)) pad = p;
        }

        while (text.Length < length)
        {
            text = pad + text;
        }

        sb.Append(text.Length > length ? text[..length] : text);
    }

    private void ExpandPadRightFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{padright:string|length|pad}}
        if (template.Arguments.Count == 0) return;

        var text = GetExpandedArgumentValue(template.Arguments[0], context);
        var length = text.Length;
        var pad = "0";

        if (template.Arguments.Count > 1 && int.TryParse(GetExpandedArgumentValue(template.Arguments[1], context), out var l))
        {
            length = l;
        }

        if (template.Arguments.Count > 2)
        {
            var p = GetExpandedArgumentValue(template.Arguments[2], context);
            if (!string.IsNullOrEmpty(p)) pad = p;
        }

        while (text.Length < length)
        {
            text += pad;
        }

        sb.Append(text.Length > length ? text[..length] : text);
    }

    private void ExpandReplaceFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#replace:string|search|replace}}
        if (template.Arguments.Count < 2) return;

        var text = GetExpandedArgumentValue(template.Arguments[0], context);
        var search = GetExpandedArgumentValue(template.Arguments[1], context);
        var replace = template.Arguments.Count > 2 ? GetExpandedArgumentValue(template.Arguments[2], context) : "";

        sb.Append(text.Replace(search, replace, StringComparison.Ordinal));
    }

    private void ExpandExplodeFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#explode:string|delimiter|position|limit}}
        if (template.Arguments.Count < 2) return;

        var text = GetExpandedArgumentValue(template.Arguments[0], context);
        var delimiter = GetExpandedArgumentValue(template.Arguments[1], context);
        var position = 0;

        if (template.Arguments.Count > 2 && int.TryParse(GetExpandedArgumentValue(template.Arguments[2], context), out var p))
        {
            position = p;
        }

        var parts = text.Split(delimiter);
        if (position < 0)
        {
            position = parts.Length + position;
        }

        if (position >= 0 && position < parts.Length)
        {
            sb.Append(parts[position]);
        }
    }

    private void ExpandTitlePartsFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#titleparts:title|segments|first}}
        if (template.Arguments.Count == 0) return;

        var title = GetExpandedArgumentValue(template.Arguments[0], context);
        var segments = int.MaxValue;
        var first = 0;

        if (template.Arguments.Count > 1 && int.TryParse(GetExpandedArgumentValue(template.Arguments[1], context), out var s))
        {
            segments = s == 0 ? int.MaxValue : Math.Abs(s);
        }

        if (template.Arguments.Count > 2 && int.TryParse(GetExpandedArgumentValue(template.Arguments[2], context), out var f))
        {
            first = f;
        }

        var parts = title.Split('/');
        var startIndex = first < 0 ? Math.Max(0, parts.Length + first) : Math.Min(first, parts.Length);
        var count = Math.Min(segments, parts.Length - startIndex);

        if (count > 0)
        {
            sb.Append(string.Join("/", parts.Skip(startIndex).Take(count)));
        }
    }

    private void ExpandNsFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{ns:number}} - return namespace name
        if (template.Arguments.Count == 0) return;

        var nsArg = GetExpandedArgumentValue(template.Arguments[0], context).Trim();

        // Standard MediaWiki namespace numbers
        var nsName = nsArg switch
        {
            "0" => "",
            "1" => "Talk",
            "2" => "User",
            "3" => "User talk",
            "4" => "Project",
            "5" => "Project talk",
            "6" => "File",
            "7" => "File talk",
            "8" => "MediaWiki",
            "9" => "MediaWiki talk",
            "10" => "Template",
            "11" => "Template talk",
            "12" => "Help",
            "13" => "Help talk",
            "14" => "Category",
            "15" => "Category talk",
            _ => nsArg
        };

        sb.Append(nsName);
    }

    private void ExpandFullUrlFunction(Template template, TemplateExpansionContext context, StringBuilder sb, bool isLocal)
    {
        // {{fullurl:page|query}}
        if (template.Arguments.Count == 0) return;

        var page = GetExpandedArgumentValue(template.Arguments[0], context);
        var query = template.Arguments.Count > 1 ? GetExpandedArgumentValue(template.Arguments[1], context) : "";

        var encodedPage = Uri.EscapeDataString(page.Replace(' ', '_'));

        if (isLocal)
        {
            sb.Append("/wiki/");
        }
        else
        {
            sb.Append("//en.wikipedia.org/wiki/");
        }

        sb.Append(encodedPage);

        if (!string.IsNullOrEmpty(query))
        {
            sb.Append('?');
            sb.Append(query);
        }
    }

    private void ExpandTimeFunction(Template template, TemplateExpansionContext context, StringBuilder sb, bool useLocal = false)
    {
        // {{#time:format|date|language}}
        if (template.Arguments.Count == 0) return;

        var format = GetExpandedArgumentValue(template.Arguments[0], context);
        var dateStr = template.Arguments.Count > 1 ? GetExpandedArgumentValue(template.Arguments[1], context) : "";

        DateTime date;
        if (string.IsNullOrWhiteSpace(dateStr))
        {
            date = useLocal ? DateTime.Now : DateTime.UtcNow;
        }
        else if (!DateTime.TryParse(dateStr, out date))
        {
            date = useLocal ? DateTime.Now : DateTime.UtcNow;
        }

        // Convert MediaWiki format codes to .NET format codes (simplified)
        var dotNetFormat = ConvertMwTimeFormat(format);
        sb.Append(date.ToString(dotNetFormat, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string ConvertMwTimeFormat(string mwFormat)
    {
        // Simplified conversion of common MediaWiki time format codes
        return mwFormat
            .Replace("Y", "yyyy")
            .Replace("y", "yy")
            .Replace("n", "M")
            .Replace("m", "MM")
            .Replace("F", "MMMM")
            .Replace("M", "MMM")
            .Replace("j", "d")
            .Replace("d", "dd")
            .Replace("l", "dddd")
            .Replace("D", "ddd")
            .Replace("H", "HH")
            .Replace("G", "H")
            .Replace("i", "mm")
            .Replace("s", "ss");
    }

    private void ExpandTagFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#tag:tagname|content|attr=value}}
        if (template.Arguments.Count == 0) return;

        var tagName = GetExpandedArgumentValue(template.Arguments[0], context);
        var content = template.Arguments.Count > 1 ? GetExpandedArgumentValue(template.Arguments[1], context) : "";

        sb.Append('<');
        sb.Append(tagName);

        // Add attributes
        for (int i = 2; i < template.Arguments.Count; i++)
        {
            var arg = template.Arguments[i];
            if (arg.Name is not null)
            {
                var attrName = arg.Name.ToString().Trim();
                var attrValue = GetExpandedArgumentValue(arg, context);
                sb.Append(' ');
                sb.Append(attrName);
                sb.Append("=\"");
                sb.Append(attrValue.Replace("\"", "&quot;"));
                sb.Append('"');
            }
        }

        if (string.IsNullOrEmpty(content))
        {
            sb.Append(" />");
        }
        else
        {
            sb.Append('>');
            sb.Append(content);
            sb.Append("</");
            sb.Append(tagName);
            sb.Append('>');
        }
    }

    private void ExpandFormatNumFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{formatnum:number|R}}
        if (template.Arguments.Count == 0) return;

        var numStr = GetExpandedArgumentValue(template.Arguments[0], context).Trim();
        var raw = template.Arguments.Count > 1 && GetExpandedArgumentValue(template.Arguments[1], context).Equals("R", StringComparison.OrdinalIgnoreCase);

        if (raw)
        {
            // Remove formatting
            sb.Append(numStr.Replace(",", "").Replace(" ", ""));
        }
        else if (double.TryParse(numStr.Replace(",", ""), out var num))
        {
            sb.Append(num.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(numStr);
        }
    }

    private void ExpandPluralFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{plural:number|singular|plural}}
        if (template.Arguments.Count < 2) return;

        var numStr = GetExpandedArgumentValue(template.Arguments[0], context).Trim();
        if (!double.TryParse(numStr, out var num))
        {
            num = 0;
        }

        var isPlural = Math.Abs(num - 1) > 0.0001;
        var index = isPlural && template.Arguments.Count > 2 ? 2 : 1;

        ExpandNode(template.Arguments[index].Value, context, sb);
    }

    private static string AnchorEncode(string value)
    {
        // Encode for use as an anchor/ID
        return Uri.EscapeDataString(value.Replace(' ', '_'))
            .Replace("%3A", ":")
            .Replace("%2F", "/");
    }

    private static double EvaluateExpression(string expr)
    {
        // Simple expression evaluator - handles basic math
        expr = expr.Trim();

        // Handle empty expression
        if (string.IsNullOrEmpty(expr)) return 0;

        // Simple number
        if (double.TryParse(expr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var simple))
        {
            return simple;
        }

        // Handle parentheses first
        while (expr.Contains('('))
        {
            var start = expr.LastIndexOf('(');
            var end = expr.IndexOf(')', start);
            if (end < 0) break;

            var inner = expr[(start + 1)..end];
            var innerResult = EvaluateExpression(inner);
            expr = string.Concat(expr.AsSpan(0, start), innerResult.ToString(System.Globalization.CultureInfo.InvariantCulture), expr.AsSpan(end + 1));
        }

        // Handle operators in order of precedence (simplified)
        // Addition/Subtraction (lowest precedence)
        var plusIdx = FindOperator(expr, '+');
        var minusIdx = FindOperator(expr, '-');

        if (plusIdx > 0 || minusIdx > 0)
        {
            var opIdx = (plusIdx > minusIdx) ? plusIdx : minusIdx;
            var op = expr[opIdx];
            var left = EvaluateExpression(expr[..opIdx]);
            var right = EvaluateExpression(expr[(opIdx + 1)..]);
            return op == '+' ? left + right : left - right;
        }

        // Multiplication/Division
        var mulIdx = FindOperator(expr, '*');
        var divIdx = FindOperator(expr, '/');
        var modIdx = expr.IndexOf(" mod ", StringComparison.OrdinalIgnoreCase);

        if (mulIdx > 0 || divIdx > 0 || modIdx > 0)
        {
            int opIdx;
            char op;

            if (modIdx > 0 && (mulIdx < 0 || modIdx < mulIdx) && (divIdx < 0 || modIdx < divIdx))
            {
                var left = EvaluateExpression(expr[..modIdx]);
                var right = EvaluateExpression(expr[(modIdx + 5)..]);
                return left % right;
            }

            opIdx = (mulIdx > divIdx) ? mulIdx : divIdx;
            op = expr[opIdx];
            var leftVal = EvaluateExpression(expr[..opIdx]);
            var rightVal = EvaluateExpression(expr[(opIdx + 1)..]);
            return op == '*' ? leftVal * rightVal : leftVal / rightVal;
        }

        // Power (^)
        var powIdx = expr.IndexOf('^');
        if (powIdx > 0)
        {
            var leftVal = EvaluateExpression(expr[..powIdx]);
            var rightVal = EvaluateExpression(expr[(powIdx + 1)..]);
            return Math.Pow(leftVal, rightVal);
        }

        // Unary minus
        if (expr.StartsWith('-'))
        {
            return -EvaluateExpression(expr[1..]);
        }

        // If we get here, try parsing as number again
        if (double.TryParse(expr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0;
    }

    private static int FindOperator(string expr, char op)
    {
        // Find operator not inside parentheses, searching from right
        var depth = 0;
        for (var i = expr.Length - 1; i >= 0; i--)
        {
            var c = expr[i];
            if (c == ')') depth++;
            else if (c == '(') depth--;
            else if (c == op && depth == 0)
            {
                // Don't match negative sign at start or after operator
                if (op == '-' && (i == 0 || "+-*/^(".Contains(expr[i - 1])))
                {
                    continue;
                }
                return i;
            }
        }
        return -1;
    }

    private static string FormatExprResult(double value)
    {
        // Format like MediaWiki does
        if (Math.Abs(value - Math.Round(value)) < 0.0000001)
        {
            return ((long)value).ToString();
        }
        return value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void ExpandIfFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#if: test | then | else}}
        if (template.Arguments.Count == 0)
        {
            return;
        }

        var test = GetExpandedArgumentValue(template.Arguments[0], context).Trim();
        var isTrue = !string.IsNullOrEmpty(test);

        if (isTrue && template.Arguments.Count > 1)
        {
            ExpandNode(template.Arguments[1].Value, context, sb);
        }
        else if (!isTrue && template.Arguments.Count > 2)
        {
            ExpandNode(template.Arguments[2].Value, context, sb);
        }
    }

    private void ExpandIfEqFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#ifeq: value1 | value2 | then | else}}
        if (template.Arguments.Count < 2)
        {
            return;
        }

        var value1 = GetExpandedArgumentValue(template.Arguments[0], context).Trim();
        var value2 = GetExpandedArgumentValue(template.Arguments[1], context).Trim();
        var isEqual = string.Equals(value1, value2, StringComparison.Ordinal);

        if (isEqual && template.Arguments.Count > 2)
        {
            ExpandNode(template.Arguments[2].Value, context, sb);
        }
        else if (!isEqual && template.Arguments.Count > 3)
        {
            ExpandNode(template.Arguments[3].Value, context, sb);
        }
    }

    private void ExpandSwitchFunction(Template template, TemplateExpansionContext context, StringBuilder sb)
    {
        // {{#switch: value | case1=result1 | case2=result2 | #default=default}}
        if (template.Arguments.Count == 0)
        {
            return;
        }

        var switchValue = GetExpandedArgumentValue(template.Arguments[0], context).Trim();
        string? defaultValue = null;
        string? result = null;

        for (int i = 1; i < template.Arguments.Count; i++)
        {
            var arg = template.Arguments[i];
            var argName = arg.Name?.ToString().Trim();

            if (argName is not null)
            {
                if (argName == "#default")
                {
                    defaultValue = GetExpandedArgumentValue(arg, context);
                }
                else if (string.Equals(argName, switchValue, StringComparison.Ordinal))
                {
                    result = GetExpandedArgumentValue(arg, context);
                    break;
                }
            }
        }

        sb.Append(result ?? defaultValue ?? string.Empty);
    }

    private string GetExpandedArgumentValue(TemplateArgument arg, TemplateExpansionContext context)
    {
        var valueSb = new StringBuilder();
        ExpandNode(arg.Value, context, valueSb);
        return valueSb.ToString();
    }

    private void ExpandArgumentReference(ArgumentReference argRef, TemplateExpansionContext context, StringBuilder sb)
    {
        var argName = argRef.Name.ToString().Trim();

        // Try to find the argument value
        if (context.Arguments.TryGetValue(argName, out var value))
        {
            sb.Append(value);
        }
        else if (argRef.DefaultValue is not null)
        {
            // Use default value, which may itself contain templates
            ExpandNode(argRef.DefaultValue, context, sb);
        }
        else
        {
            // No value and no default - output the reference as-is
            sb.Append("{{{");
            sb.Append(argName);
            sb.Append("}}}");
        }
    }

    private Dictionary<string, string> BuildArgumentDictionary(Template template)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        var positionalIndex = 1;

        foreach (var arg in template.Arguments)
        {
            var value = arg.Value.ToString();

            if (arg.Name is not null)
            {
                // Named argument
                var name = arg.Name.ToString().Trim();
                arguments[name] = value;
            }
            else
            {
                // Positional argument
                arguments[positionalIndex.ToString()] = value;
                positionalIndex++;
            }
        }

        return arguments;
    }

    private async Task ExpandNodeAsync(
        WikiNode node,
        TemplateExpansionContext context,
        StringBuilder sb,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (node)
        {
            case WikitextDocument doc:
                foreach (var line in doc.Lines)
                {
                    await ExpandNodeAsync(line, context, sb, cancellationToken).ConfigureAwait(false);
                }
                break;

            case Template template:
                await ExpandTemplateAsync(template, context, sb, cancellationToken).ConfigureAwait(false);
                break;

            case ArgumentReference argRef:
                ExpandArgumentReference(argRef, context, sb);
                break;

            case Paragraph para:
                foreach (var inline in para.Inlines)
                {
                    await ExpandNodeAsync(inline, context, sb, cancellationToken).ConfigureAwait(false);
                }
                if (!para.Compact)
                {
                    sb.AppendLine();
                }
                break;

            default:
                // For most nodes, use synchronous expansion
                ExpandNode(node, context, sb);
                break;
        }
    }

    private async Task ExpandTemplateAsync(
        Template template,
        TemplateExpansionContext context,
        StringBuilder sb,
        CancellationToken cancellationToken)
    {
        // Check recursion limit
        if (context.Depth >= _options.MaxRecursionDepth)
        {
            sb.Append(_options.RecursionLimitMessage);
            return;
        }

        // Get template name
        var templateName = template.Name?.ToString().Trim();
        if (string.IsNullOrEmpty(templateName))
        {
            sb.Append(template.ToString());
            return;
        }

        // Check for circular reference
        if (context.ExpandingTemplates.Contains(templateName, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(_options.CircularReferenceMessage);
            return;
        }

        // Handle magic words / parser functions
        if (template.IsMagicWord || templateName.StartsWith('#'))
        {
            ExpandMagicWord(template, templateName, context, sb);
            return;
        }

        // Get template content asynchronously
        var content = await _contentProvider.GetContentAsync(templateName, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            if (_options.PreserveUnknownTemplates)
            {
                sb.Append(template.ToString());
            }
            else
            {
                sb.Append("{{");
                sb.Append(templateName);
                sb.Append("}}");
            }
            return;
        }

        // Build argument dictionary
        var arguments = BuildArgumentDictionary(template);

        // Create child context
        var childContext = context.CreateChildContext(templateName, arguments);

        // Parse and expand template content
        var templateDoc = _parser.Parse(content);
        await ExpandNodeAsync(templateDoc, childContext, sb, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Options for template expansion.
/// </summary>
public class TemplateExpanderOptions
{
    /// <summary>
    /// Gets or sets the maximum recursion depth for template expansion.
    /// Default is 40 (same as MediaWiki).
    /// </summary>
    public int MaxRecursionDepth { get; set; } = 40;

    /// <summary>
    /// Gets or sets the message to output when recursion limit is exceeded.
    /// </summary>
    public string RecursionLimitMessage { get; set; } = "[[Template recursion limit exceeded]]";

    /// <summary>
    /// Gets or sets the message to output when a circular reference is detected.
    /// </summary>
    public string CircularReferenceMessage { get; set; } = "[[Template loop detected]]";

    /// <summary>
    /// Gets or sets whether to preserve unknown templates in the output.
    /// If true, unknown templates are output as-is ({{TemplateName}}).
    /// If false, only the template name is preserved.
    /// </summary>
    public bool PreserveUnknownTemplates { get; set; } = true;
}

/// <summary>
/// Context for template expansion, tracking recursion and argument values.
/// </summary>
public class TemplateExpansionContext
{
    /// <summary>
    /// Gets the current recursion depth.
    /// </summary>
    public int Depth { get; private init; }

    /// <summary>
    /// Gets the set of templates currently being expanded (for circular reference detection).
    /// </summary>
    public IReadOnlySet<string> ExpandingTemplates { get; private init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the current argument values from the parent template invocation.
    /// </summary>
    public IReadOnlyDictionary<string, string> Arguments { get; private init; } = new Dictionary<string, string>();

    /// <summary>
    /// Creates a child context for nested template expansion.
    /// </summary>
    /// <param name="templateName">The name of the template being expanded.</param>
    /// <param name="arguments">The arguments passed to the template.</param>
    /// <returns>A new context for the child template.</returns>
    public TemplateExpansionContext CreateChildContext(string templateName, Dictionary<string, string> arguments)
    {
        var expandingTemplates = new HashSet<string>(ExpandingTemplates, StringComparer.OrdinalIgnoreCase)
        {
            templateName
        };

        return new TemplateExpansionContext
        {
            Depth = Depth + 1,
            ExpandingTemplates = expandingTemplates,
            Arguments = arguments
        };
    }
}
