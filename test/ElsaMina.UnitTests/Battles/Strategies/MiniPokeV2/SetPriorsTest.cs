using System.Security.Cryptography;
using ElsaMina.Battles.Strategies.MiniPokeV2;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV2;

/// <summary>
/// setpriors.py's rules, on two Palafin roles: a Choice-able "Wallbreaker" with four attacks and a "Bulky Setup"
/// with Bulk Up (a status move) and a fifth move.
/// </summary>
public class SetPriorsTest
{
    private const string SET_PRIORS_JSON = """
        {
         "source_sha256": "",
         "species": {
          "palafin": [
           {"abilities": ["zerotohero"], "items": [], "moves": ["closecombat", "flipturn", "jetpunch", "wavecrash"],
            "role": "Wallbreaker", "tera_types": ["fighting", "water"]},
           {"abilities": ["zerotohero"], "items": [], "moves": ["bulkup", "drainpunch", "icepunch", "jetpunch", "wavecrash"],
            "role": "Bulky Setup", "tera_types": ["steel"]}
          ]
         }
        }
        """;

    private static readonly string[] ALL_CANDIDATES =
        ["jetpunch", "wavecrash", "bulkup", "closecombat", "drainpunch", "flipturn", "icepunch"];
    private static readonly string[] BULKY_SETUP_CANDIDATES = ["drainpunch", "icepunch", "jetpunch", "wavecrash"];
    private static readonly string[] BULK_UP = ["bulkup"];
    private static readonly string[] BULK_UP_AND_CLOSE_COMBAT = ["bulkup", "closecombat"];
    private static readonly string[] OUTSIDE_THE_POOL = ["struggle", "tackle"];
    private static readonly string[] TRICK = ["trick"];

    private string _path;
    private SetPriors _priors;

    [SetUp]
    public void SetUp()
    {
        _path = Path.Join(Path.GetTempPath(), $"set_priors_{Guid.NewGuid():N}.json");
        File.WriteAllText(_path, SET_PRIORS_JSON);
        _priors = SetPriors.Load(_path, TestBattleDex.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(_path);
    }

    private MonSetPrior Infer(string[] moves = null, string ability = null, string item = null,
        string teraType = null, int candidateCount = 8)
    {
        return _priors.Infer("palafin", moves ?? [], ability, item, teraType, candidateCount);
    }

    [Test]
    public void Test_Load_ShouldHashFileBytes_WhenLoaded()
    {
        // Act
        var expected = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(_path)));

        // Assert
        Assert.That(_priors.Sha256, Is.EqualTo(expected));
    }

    [Test]
    public void Test_Infer_ShouldRankCandidatesByFractionThenId_WhenNothingIsRevealed()
    {
        // Act
        var prior = Infer();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(prior.Found, Is.True);
            Assert.That(prior.RoleCount, Is.EqualTo(2));
            Assert.That(prior.ConsistentRoleCount, Is.EqualTo(2));
            Assert.That(prior.Fallback, Is.False);
            Assert.That(prior.Candidates.Select(candidate => candidate.Name), Is.EqualTo(ALL_CANDIDATES));
            Assert.That(prior.Candidates[0].Weight, Is.EqualTo(1.0));
            Assert.That(prior.Candidates[2].Weight, Is.EqualTo(0.5));
        }
    }

    [Test]
    public void Test_Infer_ShouldKeepFirstCandidates_WhenKIsSmaller()
    {
        // Act
        var prior = Infer(candidateCount: 2);

        // Assert
        Assert.That(prior.Candidates.Select(candidate => candidate.Name), Is.EqualTo(ALL_CANDIDATES.Take(2)));
    }

    [Test]
    public void Test_Infer_ShouldKeepConsistentRoles_WhenMoveIsRevealed()
    {
        // Act
        var prior = Infer(BULK_UP);

        // Assert: Bulk Up is no candidate any more, and a status move rules a Choice item out
        using (Assert.EnterMultipleScope())
        {
            Assert.That(prior.ConsistentRoleCount, Is.EqualTo(1));
            Assert.That(prior.Candidates.Select(candidate => candidate.Name), Is.EqualTo(BULKY_SETUP_CANDIDATES));
            Assert.That(prior.Candidates.Select(candidate => candidate.Weight), Is.All.EqualTo(1.0));
            Assert.That(prior.TeraTypes.Single(), Is.EqualTo(new SetPriorWeight("steel", 1.0)));
            Assert.That(prior.ChoicePossible, Is.False);
        }
    }

    [Test]
    public void Test_Infer_ShouldFallBackOnEveryRole_WhenNoRoleHasAllRevealedMoves()
    {
        // Act
        var prior = Infer(BULK_UP_AND_CLOSE_COMBAT);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(prior.ConsistentRoleCount, Is.Zero);
            Assert.That(prior.Fallback, Is.True);
            Assert.That(prior.Candidates, Has.Count.EqualTo(5));
        }
    }

    [Test]
    public void Test_Infer_ShouldIgnoreMoves_WhenOutsideTheSpeciesPool()
    {
        // Act
        var prior = Infer(OUTSIDE_THE_POOL);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(prior.ConsistentRoleCount, Is.EqualTo(2));
            Assert.That(prior.Candidates.Select(candidate => candidate.Name), Is.EqualTo(ALL_CANDIDATES));
        }
    }

    [Test]
    public void Test_Infer_ShouldAverageTeraTypesOverRoles_WhenNoneIsRevealed()
    {
        // Act
        var prior = Infer();

        // Assert: 1/2 of the Wallbreaker for fighting and water, all of the Bulky Setup for steel, each role 1/2
        Assert.That(prior.TeraTypes, Is.EqualTo(new[]
        {
            new SetPriorWeight("steel", 0.5), new SetPriorWeight("fighting", 0.25), new SetPriorWeight("water", 0.25)
        }));
    }

    [Test]
    public void Test_Infer_ShouldWeighRevealedOnly_WhenAbilityOrTeraTypeIsRevealed()
    {
        // Act
        var prior = Infer(ability: "Zero to Hero", teraType: "water");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(prior.Abilities.Single(), Is.EqualTo(new SetPriorWeight("zerotohero", 1.0)));
            Assert.That(prior.TeraTypes.Single(), Is.EqualTo(new SetPriorWeight("water", 1.0)));
            Assert.That(prior.ConsistentRoleCount, Is.EqualTo(1));
        }
    }

    [TestCase(null, true)]
    [TestCase("choicescarf", true)]
    [TestCase("Choice Band", true)]
    [TestCase("leftovers", false)]
    [TestCase("", false)]
    public void Test_Infer_ShouldTellWhetherChoiceItemIsPossible_WhenItemIs(string item, bool expected)
    {
        // Act: the Wallbreaker's four attacks could be dealt a Choice item
        var prior = Infer(item: item);

        // Assert
        Assert.That(prior.ChoicePossible, Is.EqualTo(expected));
    }

    [Test]
    public void Test_Infer_ShouldKeepChoicePossible_WhenRevealedStatusMoveIsTrick()
    {
        // Act
        var prior = Infer(TRICK);

        // Assert
        Assert.That(prior.ChoicePossible, Is.True);
    }

    [Test]
    public void Test_Infer_ShouldUseFormeItComesFrom_WhenSpeciesIsBattleOnly()
    {
        // Act
        var prior = _priors.Infer("Palafin-Hero", [], null, null, null, 8);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_priors.KeyFor("palafinhero"), Is.EqualTo("palafin"));
            Assert.That(prior.Found, Is.True);
        }
    }

    [Test]
    public void Test_Infer_ShouldFindNothing_WhenSpeciesHasNoSets()
    {
        // Act
        var prior = _priors.Infer("pikachu", [], null, null, null, 8);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(prior.Found, Is.False);
            Assert.That(prior.Candidates, Is.Empty);
        }
    }
}
