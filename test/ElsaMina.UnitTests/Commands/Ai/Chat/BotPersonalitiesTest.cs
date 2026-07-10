using ElsaMina.Commands.Ai.Chat;

namespace ElsaMina.UnitTests.Commands.Ai.Chat;

public class BotPersonalitiesTest
{
    [TestCase("silly", BotPersonality.Silly)]
    [TestCase("default", BotPersonality.Silly)]
    [TestCase("helpful", BotPersonality.Helpful)]
    [TestCase("assistant", BotPersonality.Helpful)]
    [TestCase("detective", BotPersonality.Detective)]
    [TestCase("gumshoe", BotPersonality.Detective)]
    [TestCase("philo", BotPersonality.Philosopher)]
    [TestCase("crypto", BotPersonality.CryptoBro)]
    [TestCase("gym", BotPersonality.GymBro)]
    public void Test_TryParse_ShouldResolveAlias_WhenAliasIsKnown(string value, BotPersonality expected)
    {
        // Act
        var success = BotPersonalities.TryParse(value, out var personality);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(personality, Is.EqualTo(expected));
        }
    }

    [Test]
    public void Test_TryParse_ShouldBeCaseInsensitiveAndTrimmed()
    {
        // Act
        var success = BotPersonalities.TryParse("  DeTeCtIvE  ", out var personality);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(personality, Is.EqualTo(BotPersonality.Detective));
        }
    }

    [Test]
    public void Test_TryParse_ShouldReturnFalseAndDefault_WhenValueIsUnknown()
    {
        // Act
        var success = BotPersonalities.TryParse("not-a-personality", out var personality);

        // Assert
        Assert.That(success, Is.False);
        // personality is the enum default when lookup fails
        Assert.That(personality, Is.EqualTo(default(BotPersonality)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Test_TryParse_ShouldReturnFalseAndDefault_WhenValueIsBlank(string value)
    {
        // Act
        var success = BotPersonalities.TryParse(value, out var personality);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(personality, Is.EqualTo(BotPersonalities.DEFAULT));
        }
    }

    [Test]
    public void Test_GetPromptKey_ShouldReturnResourceKey_ForEveryPersonality()
    {
        foreach (var personality in Enum.GetValues<BotPersonality>())
        {
            var key = BotPersonalities.GetPromptKey(personality);
            Assert.That(key, Is.Not.Null.And.Not.Empty,
                $"Missing prompt key for {personality}");
        }
    }

    [Test]
    public void Test_GetLabel_ShouldReturnLowercasedName()
    {
        Assert.That(BotPersonalities.GetLabel(BotPersonality.LinkedInGuru), Is.EqualTo("linkedinguru"));
    }

    [Test]
    public void Test_AvailableNames_ShouldListPrimaryAliases()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BotPersonalities.AvailableNames, Does.Contain("silly"));
            Assert.That(BotPersonalities.AvailableNames, Does.Contain("cryptobro"));
        }
    }
}
