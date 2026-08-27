using ElsaMina.Battles;
using ElsaMina.Battles.Strategies.Simulation;

namespace ElsaMina.UnitTests.Battles.Strategies.Simulation;

[TestFixture]
public class CalcPokemonFactoryTest
{
    [Test]
    public void Test_TryBuildOpponentPokemon_ShouldReturnFalse_WhenStateIsNull()
    {
        // Act
        var result = CalcPokemonFactory.TryBuildOpponentPokemon(null, out var pokemon);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(pokemon, Is.Null);
    }

    [Test]
    public void Test_TryBuildOpponentPokemon_ShouldReturnFalse_WhenSpeciesIsEmpty()
    {
        // Arrange
        var state = new OpponentPokemonState { Species = "" };

        // Act
        var result = CalcPokemonFactory.TryBuildOpponentPokemon(state, out var pokemon);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(pokemon, Is.Null);
    }

    [Test]
    public void Test_TryBuildOurPokemon_ShouldReturnFalse_WhenStateIsNull()
    {
        // Act
        var result = CalcPokemonFactory.TryBuildOurPokemon(null, out var pokemon);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(pokemon, Is.Null);
    }

    [Test]
    public void Test_TryBuildOurPokemon_ShouldReturnFalse_WhenDetailsIsEmpty()
    {
        // Arrange
        var state = new BattlePokemonState { Details = "" };

        // Act
        var result = CalcPokemonFactory.TryBuildOurPokemon(state, out var pokemon);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(pokemon, Is.Null);
    }

    [Test]
    public void Test_TryBuildOurPokemon_ShouldSucceed_WhenValidState()
    {
        // Arrange
        var state = new BattlePokemonState
        {
            Details = "Garchomp, L80, M",
            CurrentHp = 300,
            MaxHp = 300,
            Stats = new BattlePokemonStats(240, 190, 160, 170, 210)
        };

        // Act
        var result = CalcPokemonFactory.TryBuildOurPokemon(state, out var pokemon);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(pokemon, Is.Not.Null);
        Assert.That(pokemon.Name, Is.EqualTo("Garchomp"));
    }
}
