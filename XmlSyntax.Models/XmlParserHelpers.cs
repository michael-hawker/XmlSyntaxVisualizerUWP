// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Language.Xml;

namespace XmlSyntax.Models;

public static class XmlParserHelpers
{
    private const int MaxIterations = 20;

    /// <summary>
    /// Parses <paramref name="text"/> and iteratively repairs it via a delete-only strategy.
    /// </summary>
    /// <remarks>
    /// Convenience wrapper over <see cref="GetValidXml(string, XmlDocumentSyntax)"/> that
    /// returns just the resulting tree (the original public surface). Prefer
    /// <see cref="GetValidXml(string, XmlDocumentSyntax)"/> when you also need the cleaned
    /// source text or already have a parsed tree.
    /// </remarks>
    public static XmlDocumentSyntax GetValidXmlTree(string text)
        => GetValidXml(text, existingTree: null).Tree;

    /// <summary>
    /// Iteratively removes the smallest enclosing structural node bearing parser
    /// diagnostics until the document parses cleanly (or no further progress can be made).
    /// </summary>
    /// <param name="text">Source text to repair.</param>
    /// <param name="existingTree">
    /// Optional pre-parsed tree of <paramref name="text"/>. When non-null, skips the
    /// initial parse on the hot path. <strong>Must</strong> be the result of parsing
    /// the exact same text — passing a tree from a different source corrupts the result.
    /// </param>
    /// <returns>The cleaned text and its parsed tree (always consistent with each other).</returns>
    /// <remarks>
    /// Only the single smallest candidate range is applied per iteration so cascading
    /// "symptom" diagnostics (e.g. a real closing tag the parser misinterpreted because
    /// of an earlier missing tag) get a chance to re-evaluate against the cleaned text.
    /// </remarks>
    public static (string Text, XmlDocumentSyntax Tree) GetValidXml(string text, XmlDocumentSyntax existingTree = null)
    {
        text ??= string.Empty;
        var tree = existingTree ?? Parser.ParseText(text);

        for (var i = 0; i < MaxIterations; i++)
        {
            if (!XmlDocumentSyntaxExtensions.TryGetSmallestRepairRange(tree, text, out var range))
            {
                break;
            }

            var start = range.Start.Value;
            var end = range.End.Value;
            if (end <= start) break;

            text = text.Remove(start, end - start);
            tree = Parser.ParseText(text);
        }

        return (text, tree);
    }
}
