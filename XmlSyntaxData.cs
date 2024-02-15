// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Language.Xml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XmlSyntaxVisualizerUwp;

/// <summary>
/// Data class used to expose interesting Xml <see cref="SyntaxNode"/> properties to the UI.
/// </summary>
public class XmlSyntaxData
{
    public int HashId { get; set; }
    public string Type { get; set; }
    public string TypeClass { get; set; }
    public string Text { get; set; }
    public List<XmlSyntaxError> Errors { get; set; }
    public int SpanStart { get; set; }
    public int SpanEnd { get; set; }

    public int Length => SpanEnd - SpanStart;
    public bool IsError => Errors != null && Errors.Count() > 0;
    public string ErrorText => IsError ? string.Join(' ', Errors.Select(e => e.Id.ToString().Substring(4) + ": " + e.Description)) : string.Empty;

    // Hierarchical reference for TreeView templates
    public List<XmlSyntaxData> Children { get; set; }

    /// <summary>
    /// Reads in a <see cref="SyntaxNode"/> and returns a <see cref="XmlSyntaxData"/>
    /// </summary>
    /// <param name="node">Parsed Xml Node</param>
    /// <param name="withChildren">Include children in this new node.</param>
    /// <returns>UI ready data.</returns>
    public static XmlSyntaxData FromNode(SyntaxNode node, bool withChildren = true)
    {
        return new XmlSyntaxData()
        {
            HashId = node.GetHashCode(),
            Type = node.IsList ? "SyntaxList" : node.GetType().Name,
            TypeClass = node.IsList ? "list" : (node.IsToken ? "token" : "syntax"),
            Text = node.IsToken ? (node as SyntaxToken).Text : string.Empty,
            Errors = node.ContainsDiagnostics ?
                node.GetDiagnostics().Select(d => new XmlSyntaxError()
                {
                    Id = d.ErrorID,
                    Description = d.GetDescription()
                }).ToList()
                : Array.Empty<XmlSyntaxError>().ToList(),
            SpanStart = node.FullSpan.Start,
            SpanEnd = node.FullSpan.End,
            Children = withChildren ? node.ChildNodes.Select(child => XmlSyntaxData.FromNode(child)).ToList() : Array.Empty<XmlSyntaxData>().ToList()
        };
    }
}
