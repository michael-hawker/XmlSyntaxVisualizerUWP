using XmlSyntax.Models;

namespace XmlSyntax.Tests;

[TestClass]
public class DocumentTrimmingTests
{
    [TestMethod]
    public void ExtraneousPartialAttribute()
    {
        string input =
            """
            <A One="Two" Hel/>
            """;

        string expected_output =
            """
            <A One="Two"/>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousPartialAttributeFullTag()
    {
        string input =
            """
            <A One="Two" Hel>
            </A>
            """;

        string expected_output =
            """
            <A One="Two">
            </A>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousPartialAttributeValue()
    {
        string input =
            """
            <A One="Two" Hello="World/>
            """;

        string expected_output =
            """
            <A One="Two"/>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousPartialAttributeValueFullTag()
    {
        string input =
            """
            <A One="Two" Hello="World>
            </A>
            """;

        string expected_output =
            """
            <A One="Two">
            </A>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousUnclosedClosingTag()
    {
        string input =
            """
            <A>
            </B>
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

    [TestMethod]
    public void ExtraneousUnclosedOpeningTag()
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

    [TestMethod]
    public void ExtraneousUnclosedFullPropertyTag()
    {
        string input =
            """
            <A B="value">
              <A.Something>
            </A>
            """;

        string expected_output =
            """
            <A B="value">
            </A>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }

    [TestMethod]
    public void ExtraneousUnclosedFullPropertyTagExtended()
    {
        string input =
            """
              <A B="value">
                <A.Something>
            	</A
              </A>
            """;

        string expected_output =
            """
              <A B="value">
              </A>
            """;

        var output = XmlParserHelpers.GetValidXmlTree(input);

        Assert.AreEqual(expected_output, output.ToFullString());
    }
}