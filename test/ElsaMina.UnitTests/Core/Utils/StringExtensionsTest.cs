using ElsaMina.Core.Utils;

namespace ElsaMina.UnitTests.Core.Utils;

public class StringExtensionsTest
{
    [Test]
    [TestCase(null, ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase("e", ExpectedResult = false)]
    [TestCase("https://youtube.com", ExpectedResult = false)]
    [TestCase("https://example/image.png", ExpectedResult = true)]
    [TestCase("https://example/image.gif", ExpectedResult = true)]
    [TestCase("https://example/image.jpg", ExpectedResult = true)]
    public bool Test_IsValidImageLink_ShouldReturnTrue_WhenLinkIsAnImage(string link)
    {
        // Act & Assert
        return link.IsValidImageLink();
    }

    [Test]
    [TestCase("Test! 123", ExpectedResult = "test123")]
    [TestCase("This is a test.", ExpectedResult = "thisisatest")]
    [TestCase("Hello, World!", ExpectedResult = "helloworld")]
    public string Test_ToLowerAlphaNum_ShouldReformatString(string input)
    {
        // Act
        var result = input.ToLowerAlphaNum();

        // Assert
        Assert.That(result, Is.Not.Null);
        return result;
    }

    [Test]
    [TestCase("n_n\n", ExpectedResult = "n_n")]
    public string Test_RemoveNewLines_ShouldRemoveNewLines(string input)
    {
        // Act
        var result = input.RemoveNewlines();

        // Assert
        Assert.That(result, Is.Not.Null);
        return result;
    }

    [Test]
    [TestCase("<a> <b>    </b></a>", ExpectedResult = "<a><b></b></a>")]
    [TestCase(" <test>     <a>   </a>   <myTag> lol </myTag> </test>", ExpectedResult = "<test><a></a><myTag>lol</myTag></test>")]
    public string Test_RemoveWhitespacesBetweenTags_ShouldRemoveWhitespaces(string input)
    {
        // Act
        var result = input.RemoveWhitespacesBetweenTags();

        // Assert
        Assert.That(result, Is.Not.Null);
        return result;
    }

    [Test]
    [TestCase("", ExpectedResult = "")]
    [TestCase("lol", ExpectedResult = "Lol")]
    [TestCase("test lol", ExpectedResult = "Test lol")]
    public string Test_Capitalize_ShouldCapitalizeFirstWord(string input)
    {
        // Act
        var result = input.Capitalize();

        // Assert
        Assert.That(result, Is.Not.Null);
        return result;
    }

    [Test]
    [TestCase("", "", ExpectedResult = 0)]
    [TestCase("a", "a", ExpectedResult = 0)]
    [TestCase("aa", "ab", ExpectedResult = 1)]
    [TestCase("xDlol", "xMlal", ExpectedResult = 2)]
    [TestCase("test string lol", "tst strng loool", ExpectedResult = 4)]
    public int Test_LevenshteinDistance_ShouldCalculateDistance(string s1, string s2)
    {
        // Act
        var result = s1.LevenshteinDistance(s2);

        // Assert
        return result;
    }

    [Test]
    [TestCase("lol mdrrrrrr", 5, ExpectedResult = "lol...")]
    [TestCase("lol mdr xD", 8, ExpectedResult = "lol mdr...")]
    [TestCase("lol mdr xD", 10, ExpectedResult = "lol mdr xD")]
    public string Test_Shorten_ShouldShortenWithoutCuttingWords(string text, int maxLength)
    {
        // Act
        var result = text.Shorten(maxLength);

        // Assert
        return result;
    }

    [Test]
    [TestCase(
        "<span                        class=\"username\"                        data-name=\"hc\">",
        ExpectedResult = "<span class=\"username\" data-name=\"hc\">")]
    [TestCase(
        "<div  class=\"x\">text   between   tags</div>",
        ExpectedResult = "<div class=\"x\">text   between   tags</div>")]
    [TestCase(
        "<a href=\"url\">link</a>",
        ExpectedResult = "<a href=\"url\">link</a>")]
    [TestCase(
        "<img\n    src=\"img.png\"\n    alt=\"test\">",
        ExpectedResult = "<img src=\"img.png\" alt=\"test\">")]
    public string Test_CollapseAttributeWhitespace_ShouldCollapseWhitespaceInsideTags(string input)
    {
        // Act
        var result = input.CollapseAttributeWhitespace();
        
        // Assert
        return result;
    }

    [Test]
    public void Test_RemoveExtension_ShouldRemoveExtension()
    {
        // Arrange
        const string fileName = "Stuff.png";

        // Act
        var result = fileName.RemoveExtension();

        // Assert
        Assert.That(result, Is.EqualTo("Stuff"));
    }

    [Test]
    [TestCase("true", ExpectedResult = true)]
    [TestCase("  TRUE  ", ExpectedResult = true)]
    [TestCase("y", ExpectedResult = true)]
    [TestCase("t", ExpectedResult = true)]
    [TestCase("1", ExpectedResult = true)]
    [TestCase("on", ExpectedResult = true)]
    [TestCase("false", ExpectedResult = false)]
    [TestCase("0", ExpectedResult = false)]
    [TestCase("nope", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    public bool Test_ToBoolean_ShouldParseTruthyValues(string input)
    {
        // Act & Assert
        return input.ToBoolean();
    }

    [Test]
    public void Test_ToMd5Digest_ShouldReturnKnownHash()
    {
        // Act
        var result = "abc".ToMd5Digest();

        // Assert
        // Reference MD5("abc") lowercase hex digest.
        Assert.That(result, Is.EqualTo("900150983cd24fb0d6963f7d28e17f72"));
    }

    [Test]
    public void Test_ToMd5Digest_ShouldBeDeterministicAnd32CharsLong()
    {
        // Act
        var first = "elsamina".ToMd5Digest();
        var second = "elsamina".ToMd5Digest();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Has.Length.EqualTo(32));
        }
    }

    [Test]
    [TestCase("<a>   <b>", ExpectedResult = "<a> <b>")]
    [TestCase("text   <span>", ExpectedResult = "text <span>")]
    [TestCase("</span>   text", ExpectedResult = "</span> text")]
    [TestCase("<a> <b>", ExpectedResult = "<a> <b>")]
    public string Test_CollapseWhitespacesBetweenTags_ShouldCollapseRuns(string input)
    {
        // Act & Assert
        return input.CollapseWhitespacesBetweenTags();
    }

    [Test]
    [TestCase("😀", ExpectedResult = true)]
    [TestCase("👍", ExpectedResult = true)]
    [TestCase("🇫🇷", ExpectedResult = true)] // regional-indicator flag
    [TestCase("a", ExpectedResult = false)]
    [TestCase("😀😀", ExpectedResult = false)] // more than one grapheme
    [TestCase("😀a", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool Test_IsSingleEmoji_ShouldDetectExactlyOneEmoji(string input)
    {
        // Act & Assert
        return input.IsSingleEmoji();
    }

    [Test]
    [TestCase("😀👍🔥", ExpectedResult = true)]
    [TestCase("😀", ExpectedResult = true)]
    [TestCase("😀 👍", ExpectedResult = false)] // space is not emoji
    [TestCase("hello", ExpectedResult = false)]
    [TestCase("😀hi", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool Test_IsAllEmoji_ShouldRequireEveryGraphemeToBeEmoji(string input)
    {
        // Act & Assert
        return input.IsAllEmoji();
    }

    [Test]
    [TestCase("hello 😀 world", ExpectedResult = true)]
    [TestCase("😀", ExpectedResult = true)]
    [TestCase("no emoji here", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool Test_ContainsEmoji_ShouldDetectAnyEmoji(string input)
    {
        // Act & Assert
        return input.ContainsEmoji();
    }
}