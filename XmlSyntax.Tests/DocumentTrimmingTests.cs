using XmlSyntax.Models;

namespace XmlSyntax.Tests;

[TestClass]
public class DocumentTrimmingTests
{
    [TestMethod]
    public void ExtraneousUnclosedTag()
    {
        string input =
            """
            <A>
            <B>
            </A>
            """;

        string expected_output =
            """
            <A>
            </A>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }
}