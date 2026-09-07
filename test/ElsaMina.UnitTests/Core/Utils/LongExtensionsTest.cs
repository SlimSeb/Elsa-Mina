using ElsaMina.Core.Utils;

namespace ElsaMina.UnitTests.Core.Utils;

[TestFixture]
public class LongExtensionsTest
{
    [Test]
    [TestCase(0L, ExpectedResult = "0 B")]
    [TestCase(-100L, ExpectedResult = "0 B")]
    [TestCase(500L, ExpectedResult = "500 B")]
    [TestCase(1024L, ExpectedResult = "1 KB")]
    [TestCase(1536L, ExpectedResult = "1.5 KB")]
    [TestCase(1048576L, ExpectedResult = "1 MB")]
    [TestCase(52428800L, ExpectedResult = "50 MB")]
    [TestCase(1073741824L, ExpectedResult = "1 GB")]
    [TestCase(1610612736L, ExpectedResult = "1.5 GB")]
    public string Test_ToReadableDataSize_ShouldFormatCorrectly(long bytes)
    {
        return bytes.ToReadableDataSize();
    }
}
