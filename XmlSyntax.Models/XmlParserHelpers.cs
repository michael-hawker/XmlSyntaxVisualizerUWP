// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Language.Xml;

namespace XmlSyntax.Models;

public static class XmlParserHelpers
{
    private const int MaxIterations = 20;

    /// <summary>
    /// Parses <paramref name="text"/> and, if the result has parser diagnostics, iteratively
    /// removes the smallest enclosing structural nodes whose tokens carry diagnostics until
    /// the document re-parses cleanly (or no further progress can be made).
    /// </summary>
    /// <remarks>
    /// Uses a delete-only repair strategy: only characters from the source are removed, never
    /// added or rewritten. Returns the parsed (cleaned) <see cref="XmlDocumentSyntax"/>; call
    /// <see cref="SyntaxNode.ToFullString"/> to obtain the cleaned XML text.
    /// </remarks>
    public static XmlDocumentSyntax GetValidXmlTree(string text)
    {
        text ??= string.Empty;

        var tree = Parser.ParseText(text);

        for (var i = 0; i < MaxIterations; i++)
        {
            var ranges = tree.GetErrorRanges();
            if (ranges.Count == 0)
            {
                break;
            }

            var cleaned = text.RemoveRanges(ranges);
            if (cleaned.Length == text.Length)
            {
                // No characters actually removed — guard against an infinite loop on
                // pathological zero-width-only diagnostics.
                break;
            }

            text = cleaned;
            tree = Parser.ParseText(text);
        }

        return tree;
    }
}
