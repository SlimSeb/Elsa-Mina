using System.Text.Json;
using ElsaMina.Battles;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// A battle tracked by ElsaMina's own parser: our Sandaconda (Earthquake, Rock Blast, Glare, Coil) and Clodsire
/// against Eelektross, on turn 1.
/// </summary>
public sealed class SearchTestBattle
{
    public static readonly string[] SANDACONDA_MOVES = ["earthquake", "rockblast", "glare", "coil"];
    private static readonly string[] CLODSIRE_MOVES = ["earthquake", "toxic", "recover", "haze"];

    public SearchTestBattle()
    {
        Parser = new BattleMessageParser(TestBattleDex.Instance);
        Context = new BattleContext("battle-gen9randombattle-1");
        Apply("|switch|p1a: Sandaconda|Sandaconda, L84, M|200/258");
        Apply("|switch|p2a: Eelektross|Eelektross, L87, F|100/100");
        Apply("|turn|1");
        ApplyRequest();
    }

    public BattleMessageParser Parser { get; }
    public BattleContext Context { get; }

    public void Apply(string line)
    {
        Context.Lines.Add(line);
        Parser.TryApplyMessage(line.Split('|'), Context.RoomId, Context, out _);
    }

    private void ApplyRequest()
    {
        var stats = new { atk = 250, def = 260, spa = 150, spd = 160, spe = 170 };
        var pokemon = new object[]
        {
            new
            {
                ident = "p1: Sandaconda", details = "Sandaconda, L84, M", condition = "200/258", active = true,
                stats, moves = SANDACONDA_MOVES, baseAbility = "shedskin", item = "leftovers",
                pokeball = "pokeball", ability = "shedskin", teraType = "Water", terastallized = ""
            },
            new
            {
                ident = "p1: Clodsire", details = "Clodsire, L82, F", condition = "300/300", active = false,
                stats, moves = CLODSIRE_MOVES, baseAbility = "waterabsorb", item = "leftovers",
                pokeball = "pokeball", ability = "waterabsorb", teraType = "Flying", terastallized = ""
            }
        };
        var request = new
        {
            active = new[]
            {
                new
                {
                    moves = SANDACONDA_MOVES.Select(move => new
                    {
                        move, id = move, pp = move == "earthquake" ? 12 : 16, maxpp = 16, target = "normal",
                        disabled = false
                    }),
                    canTerastallize = "Water"
                }
            },
            side = new { name = "Elsa", id = "p1", pokemon },
            rqid = 1
        };
        Apply($"|request|{JsonSerializer.Serialize(request)}");
    }
}
