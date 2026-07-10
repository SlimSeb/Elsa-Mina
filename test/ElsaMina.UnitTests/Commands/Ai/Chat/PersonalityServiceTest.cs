using ElsaMina.Commands.Ai.Chat;

namespace ElsaMina.UnitTests.Commands.Ai.Chat;

public class PersonalityServiceTest
{
    private PersonalityService _service;

    [SetUp]
    public void SetUp()
    {
        _service = new PersonalityService();
    }

    [Test]
    public void Test_GetPersonality_ShouldReturnDefault_WhenRoomHasNoExplicitChoice()
    {
        Assert.That(_service.GetPersonality("someroom"), Is.EqualTo(BotPersonalities.DEFAULT));
    }

    [Test]
    public void Test_GetPersonality_ShouldReturnStoredPersonality_WhenSet()
    {
        // Arrange
        _service.SetPersonality("someroom", BotPersonality.Detective);

        // Act & Assert
        Assert.That(_service.GetPersonality("someroom"), Is.EqualTo(BotPersonality.Detective));
    }

    [Test]
    public void Test_SetPersonality_ShouldOverwritePreviousChoice()
    {
        // Arrange
        _service.SetPersonality("someroom", BotPersonality.Detective);
        _service.SetPersonality("someroom", BotPersonality.Boomer);

        // Act & Assert
        Assert.That(_service.GetPersonality("someroom"), Is.EqualTo(BotPersonality.Boomer));
    }

    [Test]
    public void Test_Personality_ShouldBeIsolatedPerRoom()
    {
        // Arrange
        _service.SetPersonality("room1", BotPersonality.Detective);

        // Act & Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_service.GetPersonality("room1"), Is.EqualTo(BotPersonality.Detective));
            Assert.That(_service.GetPersonality("room2"), Is.EqualTo(BotPersonalities.DEFAULT));
        }
    }

    [Test]
    public void Test_GetPersonality_ShouldNotThrow_WhenRoomIdIsNull()
    {
        Assert.That(_service.GetPersonality(null), Is.EqualTo(BotPersonalities.DEFAULT));
    }
}
