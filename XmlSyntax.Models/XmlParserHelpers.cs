using Microsoft.Language.Xml;
using System.Linq;

namespace XmlSyntax.Models;

public static class XmlParserHelpers
{
    /// <summary>
    /// This function parses the given text but returns a document that only contains valid Xml nodes. One should be able to call <see cref="SyntaxNode.ToFullString"/> to get the valid version of the XML.
    /// </summary>
    public static XmlDocumentSyntax GetValidXmlTree(string text)
    {
        // Re-parse to get copy of tree // TODO: Have a version that just prunes an existing tree?
        var tree = Parser.ParseText(text);

        // TODO: Validate root?

        // Walk and dump skipped tokens and missing tokens...
        tree = GetValidXmlTreeHelper(tree, tree);

        // Need to get rid of up to the Attribute Node or Syntax Node?

        return tree;
    }

    private static XmlDocumentSyntax GetValidXmlTreeHelper(XmlDocumentSyntax root, SyntaxNode node)
    {
        foreach (var child in node.ChildNodes.ToArray())
        {
            if (child.DescendantContainsDiagnostics() && 
                child.GetType() == typeof(XmlAttributeSyntax))
            {
                root = root.RemoveNode(child, SyntaxRemoveOptions.KeepEndOfLine);
            }
            else
            {
                root = GetValidXmlTreeHelper(root, child);
            }

            // Check element after we've removed bad attributes.
            if (child.DescendantContainsDiagnostics() &&
                child.GetType() == typeof(XmlElementSyntax))
            {
                root = root.RemoveNode(child, SyntaxRemoveOptions.KeepEndOfLine);
            }
        }

        return root;
    }

    private static bool DescendantContainsDiagnostics(this SyntaxNode node)
    {
        // Base Case
        if (node.ContainsDiagnostics)
        {
            return true;
        }

        // Recurse
        return node.ChildNodes.Any(child => child.DescendantContainsDiagnostics());
    }
}
