using ElsaMina.Battles;
using ElsaMina.Battles.Strategies.MiniPoke;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

/// <summary>
/// The guard's rules on battle lines (tools/minipoke/tests/test_analyze_replays.py checks the same rules in
/// analyze_replays.guard_vetoes). We are p1: Sandaconda against Eelektross.
/// </summary>
public class MiniPokeActionGuardTest
{
    private const int EARTHQUAKE = 6;
    private const int STEALTH_ROCK = 7;
    private const int RECOVER = 8;
    private const int SLEEP_TALK = 9;
    private const int TERA_OFFSET = 4;
    private static readonly string[] MOVES = ["earthquake", "stealthrock", "recover", "sleeptalk"];
    private static readonly string[] SPIKES_MOVES = ["earthquake", "spikes"];

    private static readonly string[] IMMUNE =
    [
        "|turn|2", "|move|p1a: Sandaconda|Earthquake|p2a: Eelektross",
        "|-immune|p2a: Eelektross|[from] ability: Levitate", "|turn|3"
    ];

    private MiniPokeActionGuard _guard;

    [SetUp]
    public void SetUp()
    {
        _guard = new MiniPokeActionGuard(TestBattleDex.Instance);
    }

    private static BattleContext CreateContext(IEnumerable<string> lines, string condition = "258/258",
        bool forceSwitch = false)
    {
        var context = new BattleContext("battle-gen9randombattle-1") { SideId = "p1" };
        context.Lines.AddRange([
            "|switch|p1a: Sandaconda|Sandaconda, L84|258/258", "|switch|p2a: Eelektross|Eelektross, L87|100/100",
            ..lines
        ]);
        var hp = condition.Split(' ')[0].Split('/');
        context.SidePokemon =
        [
            new BattlePokemonState
            {
                Ident = "p1: Sandaconda", IsActive = true, Condition = condition, CurrentHp = int.Parse(hp[0]),
                MaxHp = int.Parse(hp[1]), Ability = "shedskin"
            }
        ];
        context.ForceSwitchSlots = [forceSwitch];
        return context;
    }

    private static BattleObservation CreateObservation(string[] moves = null)
    {
        var observation = new BattleObservation(84, 39, 81);
        (moves ?? MOVES).CopyTo(observation.MoveSlotIds, 0);
        return observation;
    }

    private IReadOnlyDictionary<int, string> GetVetoes(IEnumerable<string> lines, string condition = "258/258",
        bool forceSwitch = false, string[] moves = null) =>
        _guard.GetVetoes(CreateContext(lines, condition, forceSwitch), CreateObservation(moves));

    [Test]
    public void Test_GetVetoes_ShouldVetoMoveAndItsTera_WhenItHadNoEffectOnTheSameTarget()
    {
        // Act
        var vetoes = GetVetoes(IMMUNE);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(vetoes[EARTHQUAKE], Is.EqualTo("no effect on Eelektross before"));
            Assert.That(vetoes[EARTHQUAKE + TERA_OFFSET], Is.EqualTo(vetoes[EARTHQUAKE]));
        }
    }

    [Test]
    public void Test_GetVetoes_ShouldNotVetoMove_WhenTargetTerastallizedSince()
    {
        // Act
        var vetoes = GetVetoes([..IMMUNE, "|-terastallize|p2a: Eelektross|Ground"]);

        // Assert
        Assert.That(vetoes, Does.Not.ContainKey(EARTHQUAKE));
    }

    [Test]
    public void Test_GetVetoes_ShouldOnlyVetoMove_WhenTheSameTargetIsActive()
    {
        // Arrange
        string[] switchedOut = [..IMMUNE, "|switch|p2a: Rotom|Rotom, L88|100/100"];

        // Act
        var otherTarget = GetVetoes(switchedOut);
        var backIn = GetVetoes([..switchedOut, "|switch|p2a: Eelektross|Eelektross, L87|100/100"]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(otherTarget, Does.Not.ContainKey(EARTHQUAKE));
            Assert.That(backIn, Does.ContainKey(EARTHQUAKE));
        }
    }

    [Test]
    public void Test_GetVetoes_ShouldVetoHealingAndSleepTalk_WhenAwakeAtFullHp()
    {
        // Act
        var vetoes = GetVetoes([]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(vetoes[RECOVER], Is.EqualTo("heals at full HP"));
            Assert.That(vetoes[SLEEP_TALK], Is.EqualTo("only works asleep"));
            Assert.That(vetoes, Does.Not.ContainKey(EARTHQUAKE));
        }
    }

    [Test]
    public void Test_GetVetoes_ShouldVetoNothing_WhenHurtAndAsleep()
    {
        // Act
        var vetoes = GetVetoes([], "100/258 slp");

        // Assert
        Assert.That(vetoes, Is.Empty);
    }

    [Test]
    public void Test_GetVetoes_ShouldVetoHazard_WhenOpponentSideHasEveryLayer()
    {
        // Arrange
        string[] rocks = ["|-sidestart|p2: Them|move: Stealth Rock"];

        // Act
        var set = GetVetoes(rocks);
        var defogged = GetVetoes([..rocks, "|-sideend|p2: Them|Stealth Rock|[from] move: Defog"]);
        var swapped = GetVetoes([..rocks, "|-swapsideconditions|"]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(set[STEALTH_ROCK], Is.EqualTo("hazard already set"));
            Assert.That(defogged, Does.Not.ContainKey(STEALTH_ROCK));
            Assert.That(swapped, Does.Not.ContainKey(STEALTH_ROCK));
        }
    }

    [Test]
    public void Test_GetVetoes_ShouldCountSpikesLayers()
    {
        // Arrange
        var twoLayers = Enumerable.Repeat("|-sidestart|p2: Them|Spikes", 2).ToList();

        // Act
        var two = GetVetoes(twoLayers, moves: SPIKES_MOVES);
        var three = GetVetoes([..twoLayers, "|-sidestart|p2: Them|Spikes"], moves: SPIKES_MOVES);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(two, Does.Not.ContainKey(STEALTH_ROCK));
            Assert.That(three, Does.ContainKey(STEALTH_ROCK));
        }
    }

    [Test]
    public void Test_GetVetoes_ShouldVetoEverySwitch_AfterTrappedErrorUntilAnActiveChanges()
    {
        // Arrange
        string[] error = [$"|error|{MiniPokeActionGuard.TRAPPED_ERROR}"];

        // Act
        var trapped = GetVetoes(error);
        var opponentSwitched = GetVetoes([..error, "|switch|p2a: Rotom|Rotom, L88|100/100"]);
        var forced = GetVetoes(error, forceSwitch: true);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Enumerable.Range(0, 6).All(slot => trapped[slot] == "trapped"), Is.True);
            Assert.That(opponentSwitched, Does.Not.ContainKey(0));
            Assert.That(forced, Does.Not.ContainKey(0));
        }
    }
}
