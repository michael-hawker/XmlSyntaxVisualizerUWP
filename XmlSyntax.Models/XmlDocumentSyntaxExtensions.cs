// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Language.Xml;

namespace XmlSyntax.Models;

public static class XmlDocumentSyntaxExtensions
{
    /// <summary>
    /// Walks the parsed tree and returns source character ranges that — when removed
    /// from the original text — should yield a re-parseable, well-formed XML document.
    /// </summary>
    /// <remarks>
    /// Strategy:
    /// <list type="bullet">
    ///   <item>For every node and token that carries diagnostics, find the smallest enclosing
    ///         <em>removable</em> structural node (element, start/end tag, attribute, text,
    ///         comment, CDATA, processing instruction, declaration) with non-zero width and
    ///         take its full span as a candidate removal range.</item>
    ///   <item>When two candidate ranges overlap, the smaller one wins — the larger range
    ///         is almost always a downstream symptom of the smaller, deeper error
    ///         (e.g. an unclosed attribute string makes its enclosing element appear
    ///         "missing an end tag"). Removing the deepest cause and re-parsing usually
    ///         resolves the symptoms automatically.</item>
    ///   <item>Each range is widened leftward to consume any whitespace that immediately
    ///         precedes it in the source (so removing a stray attribute doesn't leave a
    ///         double space behind).</item>
    ///   <item>If an unclosed XML attribute string consumed a tag-closing <c>&gt;</c> or
    ///         <c>/&gt;</c>, the trailing tag-boundary characters are trimmed back out of
    ///         the range so they survive removal. This trim only applies to attribute
    ///         removals — for an element removal the trailing <c>&gt;</c> belongs to the
    ///         element being removed and must be deleted with it.</item>
    /// </list>
    /// Note: diagnostics in this library are recorded only on the immediate offending node —
    /// they do <em>not</em> propagate up the tree — so we cannot short-circuit the walk
    /// based on <see cref="SyntaxNode.ContainsDiagnostics"/> at the document root.
    /// </remarks>
    public static IReadOnlyList<Range> GetErrorRanges(this XmlDocumentSyntax tree)
    {
        if (tree is null) throw new ArgumentNullException(nameof(tree));

        var sourceText = tree.ToFullString();
        var raw = new List<RemovalCandidate>();
        Collect(tree, raw);

        return PostProcess(raw, sourceText);
    }

    private static void Collect(SyntaxNode node, List<RemovalCandidate> ranges)
    {
        if (HasOwnDiagnostics(node))
        {
            var enclosing = FindEnclosingRemovable(node);
            if (enclosing != null)
            {
                ranges.Add(new RemovalCandidate(enclosing));
            }
        }

        foreach (var child in node.ChildNodes)
        {
            if (child is SyntaxToken token)
            {
                if (HasOwnDiagnostics(token))
                {
                    var enclosing = FindEnclosingRemovable(token);
                    if (enclosing != null)
                    {
                        ranges.Add(new RemovalCandidate(enclosing));
                    }
                }
            }
            else
            {
                Collect(child, ranges);
            }
        }
    }

    private static bool HasOwnDiagnostics(SyntaxNode node)
    {
        if (!node.ContainsDiagnostics) return false;

        var diags = node.GetDiagnostics();
        return diags != null && diags.Any();
    }

    private static SyntaxNode FindEnclosingRemovable(SyntaxNode node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current.FullWidth == 0)
            {
                continue;
            }

            switch (current)
            {
                case XmlElementSyntax _:
                case XmlEmptyElementSyntax _:
                case XmlElementStartTagSyntax _:
                case XmlElementEndTagSyntax _:
                case XmlAttributeSyntax _:
                case XmlCommentSyntax _:
                case XmlCDataSectionSyntax _:
                case XmlProcessingInstructionSyntax _:
                case XmlDeclarationSyntax _:
                case XmlTextSyntax _:
                    return current;
            }
        }

        return null;
    }

    private static IReadOnlyList<Range> PostProcess(List<RemovalCandidate> raw, string source)
    {
        if (raw.Count == 0) return Array.Empty<Range>();

        // 1. Drop ranges that overlap a smaller candidate (smallest cause wins).
        var ordered = raw
            .GroupBy(c => (c.Start, c.End))
            .Select(g => g.First())
            .OrderBy(c => c.End - c.Start)
            .ToList();

        var kept = new List<RemovalCandidate>();
        foreach (var candidate in ordered)
        {
            if (!kept.Any(k => Overlaps(k, candidate)))
            {
                kept.Add(candidate);
            }
        }

        // 2. Refine: trim swallowed tag-closers for attributes only, then extend left
        //    over preceding whitespace.
        var refined = new List<Range>(kept.Count);
        foreach (var c in kept)
        {
            var range = (c.Start, c.End);
            if (c.IsAttribute)
            {
                range = TrimTrailingTagBoundary(range, source);
            }
            range = ExtendLeftOverWhitespace(range, source);

            if (range.End > range.Start)
            {
                refined.Add(new Range(range.Start, range.End));
            }
        }

        return refined.OrderBy(r => r.Start.Value).ToList();
    }

    private static bool Overlaps(RemovalCandidate a, RemovalCandidate b) =>
        a.Start < b.End && b.Start < a.End;

    private static (int Start, int End) ExtendLeftOverWhitespace((int Start, int End) range, string source)
    {
        var start = range.Start;
        while (start > 0 && IsXmlWhitespace(source[start - 1]))
        {
            start--;
        }
        return (start, range.End);
    }

    private static (int Start, int End) TrimTrailingTagBoundary((int Start, int End) range, string source)
    {
        var end = Math.Min(range.End, source.Length);

        while (end > range.Start && IsXmlWhitespace(source[end - 1]))
        {
            end--;
        }

        if (end > range.Start && source[end - 1] == '>')
        {
            end--; // drop '>'
            if (end > range.Start && source[end - 1] == '/')
            {
                end--; // drop the '/' of a self-closing tag too
            }
        }

        return (range.Start, end);
    }

    private static bool IsXmlWhitespace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';

    private readonly struct RemovalCandidate
    {
        public RemovalCandidate(SyntaxNode node)
        {
            Start = node.FullSpan.Start;
            End = node.FullSpan.End;
            IsAttribute = node is XmlAttributeSyntax;
        }

        public int Start { get; }
        public int End { get; }
        public bool IsAttribute { get; }
    }
}
