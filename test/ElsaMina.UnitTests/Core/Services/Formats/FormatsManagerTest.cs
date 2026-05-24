using ElsaMina.Core.Services.Formats;

namespace ElsaMina.UnitTests.Core.Services.Formats;

public class FormatsManagerTest
{
    private FormatsManager _formatsManager;

    [SetUp]
    public void SetUp()
    {
        _formatsManager = new FormatsManager();
    }

    [Test]
    public void Test_ParseFormatsFromReceivedLine_ShouldInsertFormats()
    {
        // Arrange
        const string message = "|formats|,1|S/V Singles|[Gen 9] Random Battle,f|[Gen 9] Unrated Random Battle,b";

        // Act
        _formatsManager.ParseFormats(message.Split("|"));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_formatsManager.Formats, Is.Not.Empty);
            Assert.That(_formatsManager.Formats.Count(), Is.EqualTo(2));
        }
    }

    [Test]
    public void Test_GetCleanFormat_ShouldGetFormattedTier_WhenTierExists()
    {
        // Arrange
        const string message = "|formats|,1|S/V Singles|[Gen 9] Random Battle,f|[Gen 9] Unrated Random Battle,b";
        _formatsManager.ParseFormats(message.Split("|"));

        // Act
        var tier = _formatsManager.GetCleanFormat("gen9randombattle");

        // Assert
        Assert.That(tier, Is.EqualTo("[Gen 9] Random Battle"));
    }

    [Test]
    public void Test_GetCleanFormat_ShouldGetInputTier_WhenTierDoesntExist()
    {
        // Arrange
        const string message = "|formats|,1|S/V Singles|[Gen 9] Random Battle,f|[Gen 9] Unrated Random Battle,b";
        _formatsManager.ParseFormats(message.Split("|"));

        // Act
        var tier = _formatsManager.GetCleanFormat("gen7littlecup");

        // Assert
        Assert.That(tier, Is.EqualTo("gen7littlecup"));
    }
}