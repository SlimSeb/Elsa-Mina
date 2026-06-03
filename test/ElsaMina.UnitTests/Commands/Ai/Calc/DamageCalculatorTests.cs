using ElsaMina.Commands.Ai.Calc;

namespace ElsaMina.UnitTests.Commands.Ai.Calc;

public class DamageCalculatorTests
{
    private DamageCalculator _damageCalculator;

    [SetUp]
    public void SetUp()
    {
        _damageCalculator = new DamageCalculator();
    }

    [Test]
    public void Test_Calculate_ShouldReturnSmogonDescription_WhenGivenBoostedSpecialAttacker()
    {
        // Arrange
        var request = new CalcRequestDto
        {
            Gen = 9,
            Move = "Sludge Bomb",
            Attacker = new CalcPokemonDto
            {
                Name = "Gengar",
                Nature = "Modest",
                Item = "Life Orb",
                Evs = new CalcStatsDto { Spa = 252 },
                Boosts = new CalcStatsDto { Spa = 3 }
            },
            Defender = new CalcPokemonDto
            {
                Name = "Chansey",
                Item = "Eviolite",
                Evs = new CalcStatsDto { Hp = 100, Spd = 100 },
                Boosts = new CalcStatsDto { Spd = 1 }
            }
        };

        // Act
        var description = _damageCalculator.Calculate(request);

        // Assert
        Assert.That(description,
            Is.EqualTo("+3 252+ SpA Life Orb Gengar Sludge Bomb vs. +1 100 HP / 100 SpD Eviolite Chansey: 204-242 (30.6 - 36.3%) -- 52.9% chance to 3HKO"));
    }

    [Test]
    public void Test_Calculate_ShouldThrow_WhenSpeciesIsUnknown()
    {
        // Arrange
        var request = new CalcRequestDto
        {
            Gen = 9,
            Move = "Tackle",
            Attacker = new CalcPokemonDto { Name = "NotARealPokemon" },
            Defender = new CalcPokemonDto { Name = "Chansey" }
        };

        // Act / Assert
        Assert.That(() => _damageCalculator.Calculate(request), Throws.Exception);
    }

    [Test]
    public void Test_Calculate_ShouldThrow_WhenMoveIsMissing()
    {
        // Arrange
        var request = new CalcRequestDto
        {
            Gen = 9,
            Move = null,
            Attacker = new CalcPokemonDto { Name = "Gengar" },
            Defender = new CalcPokemonDto { Name = "Chansey" }
        };

        // Act / Assert
        Assert.That(() => _damageCalculator.Calculate(request), Throws.ArgumentException);
    }
}
