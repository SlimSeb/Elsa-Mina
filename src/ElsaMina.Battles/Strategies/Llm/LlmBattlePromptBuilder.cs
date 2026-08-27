using System.Text;
using ElsaMina.Battles.Data;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Simulation;
using ElsaMina.Logging;
using Lusamine.DamageCalc;
using Lusamine.DamageCalc.Data;

namespace ElsaMina.Battles.Strategies.Llm;

public class LlmBattlePromptBuilder : ILlmBattlePromptBuilder
{
    private static readonly double[] SpikesChipByLayers = [0.0, 12.5, 16.67, 25.0];

    public string BuildSystemPrompt()
    {
        return """
               You are an elite competitive Pokémon Showdown AI battle engine.
               Your goal is to make optimal, game-winning decisions in Pokémon battles.

               Key tactical principles to follow:
               1. Type matchups & Speed tiers: evaluate who moves first and type advantages.
               2. Damage calculations: know your damage output, KO thresholds (OHKO, 2HKO), and the opponent's kill range on you.
               3. Win conditions: preserve your key win condition Pokémon, eliminate opponent counters/checks.
               4. Hazard awareness: consider entry hazards chip damage and when to preserve or switch Pokémon.
               5. Setup & Status: exploit setup opportunities when the opponent is forced out or passive.
               6. Terastallization: use Terastallize strategically to secure crucial KOs or flip defensive weaknesses.
               7. Prediction: anticipate opponent switches or attacks based on their likely Smogon sets.

               RESPONSE FORMAT:
               You MUST respond with a valid JSON object matching this schema:
               {
                 "reasoning": "brief tactical justification for your choice",
                 "decision": "move" | "switch" | "teampreview",
                 "index": <1-based number>,
                 "terastallize": <true | false>
               }

               Rules for index:
               - When decision is "move": index is the move slot (1, 2, 3, or 4). Set "terastallize" to true ONLY if Terastallization is available and desired.
               - When decision is "switch": index is the team slot of the bench Pokémon to send in (1 to 6).
               - When decision is "teampreview": index is the team slot of the Pokémon to lead with (1 to 6).

               Respond ONLY with the JSON object.
               """;
    }

    public string BuildTeamPreviewPrompt(BattleContext context, OpponentPrediction prediction)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("=== POKÉMON SHOWDOWN: TEAM PREVIEW ===");
        stringBuilder.AppendLine($"Format: {context.Format}");
        stringBuilder.AppendLine();

        stringBuilder.AppendLine("--- OUR TEAM ---");
        for (var i = 0; i < context.SidePokemon.Count; i++)
        {
            var pokemon = context.SidePokemon[i];
            var species = CalcPokemonFactory.ExtractSpeciesFromDetails(pokemon.Details);
            var types = GetOurPokemonTypes(pokemon);
            var typesStr = types.Count > 0 ? string.Join("/", types) : "Unknown";

            stringBuilder.AppendLine($"Slot {i + 1}: {species} [{typesStr}]");
            stringBuilder.AppendLine($"  Ability: {pokemon.Ability} | Item: {pokemon.Item} | Tera Type: {pokemon.TeraType}");
            stringBuilder.AppendLine($"  Stats: HP:{pokemon.MaxHp} Atk:{pokemon.Stats.Atk} Def:{pokemon.Stats.Def} SpA:{pokemon.Stats.SpA} SpD:{pokemon.Stats.SpD} Spe:{pokemon.Stats.Spe}");
            stringBuilder.AppendLine($"  Moves: {string.Join(", ", pokemon.Moves)}");
        }
        stringBuilder.AppendLine();

        stringBuilder.AppendLine("--- OPPONENT TEAM ---");
        if (context.OpponentPokemon.Count == 0)
        {
            stringBuilder.AppendLine("  (No opponent Pokémon revealed yet)");
        }
        else
        {
            for (var i = 0; i < context.OpponentPokemon.Count; i++)
            {
                var opponent = context.OpponentPokemon[i];
                var oppTypes = GetOpponentTypes(opponent);
                var oppTypesStr = oppTypes.Count > 0 ? string.Join("/", oppTypes) : "Unknown";
                stringBuilder.AppendLine($"  {i + 1}. {opponent.Species} (Lvl {opponent.Level}) [{oppTypesStr}]");
            }
        }
        stringBuilder.AppendLine();

        stringBuilder.AppendLine("--- LEGAL CHOICES ---");
        for (var i = 0; i < context.SidePokemon.Count; i++)
        {
            var pokemon = context.SidePokemon[i];
            var species = CalcPokemonFactory.ExtractSpeciesFromDetails(pokemon.Details);
            stringBuilder.AppendLine($"- TEAM {i + 1}: Lead with {species}");
        }
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("Choose your lead Pokémon. Return JSON with decision \"teampreview\" and index (1-6).");

        return stringBuilder.ToString();
    }

    public string BuildForcedSwitchPrompt(BattleContext context, OpponentPrediction prediction,
        IReadOnlyList<int> candidateIndices)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("=== POKÉMON SHOWDOWN: FORCED SWITCH ===");
        stringBuilder.AppendLine($"Format: {context.Format}");
        stringBuilder.AppendLine();

        AppendFieldHazards(stringBuilder, context);

        var opponent = context.ActiveOpponent;
        if (opponent != null)
        {
            AppendOpponentActiveInfo(stringBuilder, opponent, prediction);
        }

        stringBuilder.AppendLine("--- AVAILABLE SWITCH CANDIDATES ---");
        CalcPokemonFactory.TryBuildOpponentPokemon(opponent, out var calcOpponent, prediction?.Spread);

        foreach (var slotIndex in candidateIndices)
        {
            if (slotIndex < 1 || slotIndex > context.SidePokemon.Count)
            {
                continue;
            }

            var pokemon = context.SidePokemon[slotIndex - 1];
            var species = CalcPokemonFactory.ExtractSpeciesFromDetails(pokemon.Details);
            var types = GetOurPokemonTypes(pokemon);
            var typesStr = types.Count > 0 ? string.Join("/", types) : "Unknown";
            var hpPercent = pokemon.MaxHp > 0 ? (double)pokemon.CurrentHp / pokemon.MaxHp * 100.0 : 0.0;

            stringBuilder.AppendLine($"Slot {slotIndex}: {species} [{typesStr}] - HP: {pokemon.CurrentHp}/{pokemon.MaxHp} ({hpPercent:F1}%)");
            stringBuilder.AppendLine($"  Ability: {pokemon.Ability} | Item: {pokemon.Item} | Spe: {pokemon.Stats.Spe}");
            stringBuilder.AppendLine($"  Moves: {string.Join(", ", pokemon.Moves)}");

            // Hazard chip
            var chip = ComputeSwitchInChipPercent(types, context.OwnSideStealthRock, context.OwnSideSpikesLayers);
            if (chip > 0)
            {
                stringBuilder.AppendLine($"  ⚠️ Entry hazard chip on switch-in: takes {chip:F1}% max HP damage");
            }

            // Damage from opponent active
            if (calcOpponent != null && CalcPokemonFactory.TryBuildOurPokemon(pokemon, out var calcMember) && prediction?.Moves != null)
            {
                var damageNotes = new List<string>();
                foreach (var predictedMove in prediction.Moves)
                {
                    try
                    {
                        var move = new Move(CalcPokemonFactory.Generation, predictedMove.Name);
                        if (move.Category == MoveCategories.Status) continue;

                        var result = Calc.Calculate(CalcPokemonFactory.Generation, calcOpponent, calcMember, move, null);
                        var (minDmg, maxDmg) = result.Range();
                        var maxHp = calcMember.MaxHP(false);
                        if (maxHp > 0)
                        {
                            var minPct = (double)minDmg / maxHp * 100.0;
                            var maxPct = (double)maxDmg / maxHp * 100.0;
                            damageNotes.Add($"{predictedMove.Name}: {minPct:F1}-{maxPct:F1}%");
                        }
                    }
                    catch
                    {
                        // Ignore calc errors
                    }
                }

                if (damageNotes.Count > 0)
                {
                    stringBuilder.AppendLine($"  🛡️ Expected damage taken from opponent: {string.Join(", ", damageNotes)}");
                }
            }
        }
        stringBuilder.AppendLine();

        stringBuilder.AppendLine("--- LEGAL CHOICES ---");
        foreach (var slotIndex in candidateIndices)
        {
            var pokemon = context.SidePokemon[slotIndex - 1];
            var species = CalcPokemonFactory.ExtractSpeciesFromDetails(pokemon.Details);
            stringBuilder.AppendLine($"- SWITCH {slotIndex}: Switch in {species}");
        }
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("Your active Pokémon fainted or was forced to switch. Choose which Pokémon to switch in.");

        return stringBuilder.ToString();
    }

    public string BuildTurnPrompt(BattleContext context, OpponentPrediction prediction)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("=== POKÉMON SHOWDOWN: BATTLE TURN ===");
        stringBuilder.AppendLine($"Format: {context.Format}");
        stringBuilder.AppendLine();

        AppendFieldHazards(stringBuilder, context);

        var activeSlot = context.ActiveSlots.Count > 0 ? context.ActiveSlots[0] : null;
        var ourActivePokemon = context.SidePokemon.FirstOrDefault(p => p.IsActive);
        var opponent = context.ActiveOpponent;

        // Our active details
        if (ourActivePokemon != null)
        {
            var species = CalcPokemonFactory.ExtractSpeciesFromDetails(ourActivePokemon.Details);
            var types = GetOurPokemonTypes(ourActivePokemon);
            var typesStr = types.Count > 0 ? string.Join("/", types) : "Unknown";
            var hpPercent = ourActivePokemon.MaxHp > 0 ? (double)ourActivePokemon.CurrentHp / ourActivePokemon.MaxHp * 100.0 : 0.0;
            var statusStr = string.IsNullOrEmpty(ourActivePokemon.Condition) ? "Healthy" : ourActivePokemon.Condition;
            var effectiveSpeed = ourActivePokemon.Stats != null ? ourActivePokemon.Stats.Spe : 100;
            if (CalcPokemonFactory.ExtractStatus(ourActivePokemon.Condition) == "par")
            {
                effectiveSpeed /= 2;
            }

            stringBuilder.AppendLine("--- OUR ACTIVE POKÉMON ---");
            stringBuilder.AppendLine($"Name: {species} [{typesStr}]");
            stringBuilder.AppendLine($"HP: {ourActivePokemon.CurrentHp}/{ourActivePokemon.MaxHp} ({hpPercent:F1}%) | Status: {statusStr}");
            if (ourActivePokemon.Stats != null)
            {
                stringBuilder.AppendLine($"Stats: HP:{ourActivePokemon.MaxHp} Atk:{ourActivePokemon.Stats.Atk} Def:{ourActivePokemon.Stats.Def} SpA:{ourActivePokemon.Stats.SpA} SpD:{ourActivePokemon.Stats.SpD} Spe:{ourActivePokemon.Stats.Spe} (Effective Spe: {effectiveSpeed})");
            }
            else
            {
                stringBuilder.AppendLine($"Stats: HP:{ourActivePokemon.MaxHp}");
            }
            stringBuilder.AppendLine($"Ability: {ourActivePokemon.Ability} | Item: {ourActivePokemon.Item} | Tera Type: {ourActivePokemon.TeraType}");
            if (!string.IsNullOrEmpty(ourActivePokemon.Terastallized))
            {
                stringBuilder.AppendLine($"Terastallized: {ourActivePokemon.Terastallized}");
            }
            if (activeSlot != null)
            {
                var canTeraStr = !string.IsNullOrEmpty(activeSlot.CanTerastallize)
                    ? $"YES (Can Terastallize into {activeSlot.CanTerastallize})"
                    : "NO";
                stringBuilder.AppendLine($"Can Terastallize this turn: {canTeraStr}");
                stringBuilder.AppendLine($"Trapped: {(activeSlot.Trapped ? "YES (Cannot switch)" : "NO")}");
            }
            stringBuilder.AppendLine();
        }

        // Opponent active details
        if (opponent != null)
        {
            AppendOpponentActiveInfo(stringBuilder, opponent, prediction);
        }
        else
        {
            stringBuilder.AppendLine("--- OPPONENT ACTIVE POKÉMON ---");
            stringBuilder.AppendLine("  (Opponent active Pokémon not yet revealed)");
            stringBuilder.AppendLine();
        }

        // Speed comparison
        if (ourActivePokemon?.Stats != null && opponent != null)
        {
            var ourSpeed = ourActivePokemon.Stats.Spe;
            if (CalcPokemonFactory.ExtractStatus(ourActivePokemon.Condition) == "par") ourSpeed /= 2;

            var oppSpeed = ComputeOpponentSpeedEstimate(opponent, prediction?.Spread);
            string speedComparison;
            if (ourSpeed > oppSpeed)
            {
                speedComparison = $"⚡ You are FASTER (Your Spe: {ourSpeed} vs Opponent est: {oppSpeed})";
            }
            else if (ourSpeed < oppSpeed)
            {
                speedComparison = $"⚡ Opponent is FASTER (Opponent est: {oppSpeed} vs Your Spe: {ourSpeed})";
            }
            else
            {
                speedComparison = $"⚡ Speed tie likely (Your Spe: {ourSpeed} vs Opponent est: {oppSpeed})";
            }
            stringBuilder.AppendLine(speedComparison);
            stringBuilder.AppendLine();
        }

        // In-depth Damage Calculations (Our Moves vs Opponent Active)
        if (activeSlot != null && ourActivePokemon != null && opponent != null)
        {
            AppendDamageCalculations(stringBuilder, activeSlot, ourActivePokemon, opponent, prediction);
        }

        // Opponent Attacks vs Our Active
        if (ourActivePokemon != null && opponent != null && prediction?.Moves != null)
        {
            AppendOpponentAttacksOnUs(stringBuilder, ourActivePokemon, opponent, prediction);
        }

        // Bench Teammates & Switch-in Analysis
        AppendBenchAnalysis(stringBuilder, context, opponent, prediction);

        // Opponent Bench
        AppendOpponentBench(stringBuilder, context);

        // Legal Choices
        AppendLegalChoices(stringBuilder, context, activeSlot);

        stringBuilder.AppendLine("Make your decision. Return JSON with decision (\"move\" or \"switch\"), index, and terastallize.");

        return stringBuilder.ToString();
    }

    private static void AppendFieldHazards(StringBuilder stringBuilder, BattleContext context)
    {
        stringBuilder.AppendLine("--- FIELD CONDITIONS & HAZARDS ---");
        var ownHazards = new List<string>();
        if (context.OwnSideStealthRock) ownHazards.Add("Stealth Rock");
        if (context.OwnSideSpikesLayers > 0) ownHazards.Add($"{context.OwnSideSpikesLayers} layer(s) Spikes");
        stringBuilder.AppendLine($"Our side: {(ownHazards.Count > 0 ? string.Join(", ", ownHazards) : "None")}");

        var oppHazards = new List<string>();
        if (context.OpponentSideStealthRock) oppHazards.Add("Stealth Rock");
        if (context.OpponentSideSpikesLayers > 0) oppHazards.Add($"{context.OpponentSideSpikesLayers} layer(s) Spikes");
        if (context.OpponentSideToxicSpikes) oppHazards.Add("Toxic Spikes");
        if (context.OpponentSideStickyWeb) oppHazards.Add("Sticky Web");
        stringBuilder.AppendLine($"Opponent side: {(oppHazards.Count > 0 ? string.Join(", ", oppHazards) : "None")}");

        if (context.OpponentActiveTaunted)
        {
            stringBuilder.AppendLine("Opponent status: TAUNTED (Cannot use status moves)");
        }
        stringBuilder.AppendLine();
    }

    private static void AppendOpponentActiveInfo(StringBuilder stringBuilder, OpponentPokemonState opponent,
        OpponentPrediction prediction)
    {
        var oppTypes = GetOpponentTypes(opponent);
        var oppTypesStr = oppTypes.Count > 0 ? string.Join("/", oppTypes) : "Unknown";
        var boostsList = opponent.Boosts.Where(b => b.Value != 0).Select(b => $"{b.Key}: {(b.Value > 0 ? "+" : "")}{b.Value}").ToList();
        var boostsStr = boostsList.Count > 0 ? string.Join(", ", boostsList) : "None";

        stringBuilder.AppendLine("--- OPPONENT ACTIVE POKÉMON ---");
        stringBuilder.AppendLine($"Species: {opponent.Species} (Lvl {opponent.Level}) [{oppTypesStr}]");
        stringBuilder.AppendLine($"HP: {opponent.HpPercent:F1}% | Status: {(string.IsNullOrEmpty(opponent.Status) ? "Healthy" : opponent.Status)}");
        stringBuilder.AppendLine($"Stat Boosts: {boostsStr}");
        if (!string.IsNullOrEmpty(opponent.LastUsedMove))
        {
            stringBuilder.AppendLine($"Last used move: {opponent.LastUsedMove}");
        }
        if (opponent.RevealedMoves.Count > 0)
        {
            stringBuilder.AppendLine($"Revealed moves: {string.Join(", ", opponent.RevealedMoves)}");
        }

        if (prediction != null)
        {
            if (prediction.Moves != null && prediction.Moves.Count > 0)
            {
                var predMovesStr = string.Join(", ", prediction.Moves.Select(m => $"{m.Name} ({m.Probability * 100.0:F0}%)"));
                stringBuilder.AppendLine($"Smogon predicted moves: {predMovesStr}");
            }
            if (prediction.Spread != null)
            {
                stringBuilder.AppendLine($"Smogon predicted set: Nature: {prediction.Spread.Nature} | EVs: HP:{prediction.Spread.HpEvs} Atk:{prediction.Spread.AtkEvs} Def:{prediction.Spread.DefEvs} SpA:{prediction.Spread.SpaEvs} SpD:{prediction.Spread.SpdEvs} Spe:{prediction.Spread.SpeEvs}");
            }
        }
        stringBuilder.AppendLine();
    }

    private static void AppendDamageCalculations(StringBuilder stringBuilder, BattleActiveSlot activeSlot,
        BattlePokemonState ourPokemon, OpponentPokemonState opponent, OpponentPrediction prediction)
    {
        stringBuilder.AppendLine("--- DAMAGE CALCULATIONS (OUR MOVES VS OPPONENT) ---");
        if (!CalcPokemonFactory.TryBuildOurPokemon(ourPokemon, out var attacker) ||
            !CalcPokemonFactory.TryBuildOpponentPokemon(opponent, out var defender, prediction?.Spread))
        {
            stringBuilder.AppendLine("  (Calculation unavailable)");
            stringBuilder.AppendLine();
            return;
        }

        Pokemon teraAttacker = null;
        if (!string.IsNullOrEmpty(activeSlot.CanTerastallize) &&
            CalcPokemonFactory.TryBuildOurPokemon(ourPokemon, out var teraCandidate))
        {
            teraCandidate.TeraType = activeSlot.CanTerastallize;
            teraAttacker = teraCandidate;
        }

        var defenderMaxHp = defender.MaxHP(false);

        for (var i = 0; i < activeSlot.Moves.Count; i++)
        {
            var moveState = activeSlot.Moves[i];
            var isDisabled = moveState.IsDisabled || (moveState.Pp == 0 && moveState.MaxPp > 0);
            var statusNote = isDisabled ? " [DISABLED/NO PP]" : "";

            try
            {
                var calcMove = new Move(CalcPokemonFactory.Generation, moveState.Name);
                var category = calcMove.Category.ToString();
                var moveType = calcMove.Type ?? "Normal";
                var typeMultiplier = TypeMatchupTable.GetMultiplier(moveType, defender.Types);
                var effectiveness = FormatEffectiveness(typeMultiplier);

                if (calcMove.Category == MoveCategories.Status)
                {
                    stringBuilder.AppendLine($"Move {i + 1}: {moveState.Name} (Type: {moveType}, Status){statusNote}");
                    stringBuilder.AppendLine($"  Effect: Status move");
                }
                else
                {
                    var result = Calc.Calculate(CalcPokemonFactory.Generation, attacker, defender, calcMove, null);
                    var (minDmg, maxDmg) = result.Range();
                    var (koChance, nHko, _) = result.Kochance(false);
                    var minPct = defenderMaxHp > 0 ? (double)minDmg / defenderMaxHp * 100.0 : 0.0;
                    var maxPct = defenderMaxHp > 0 ? (double)maxDmg / defenderMaxHp * 100.0 : 0.0;
                    var koText = FormatKoChance(koChance, nHko);

                    stringBuilder.AppendLine($"Move {i + 1}: {moveState.Name} (Type: {moveType}, {category}){statusNote}");
                    stringBuilder.AppendLine($"  💥 Damage: {minPct:F1}% - {maxPct:F1}% [{effectiveness}] -> {koText}");

                    if (teraAttacker != null)
                    {
                        var teraResult = Calc.Calculate(CalcPokemonFactory.Generation, teraAttacker, defender, calcMove, null);
                        var (teraMin, teraMax) = teraResult.Range();
                        var (teraKoChance, teraNHko, _) = teraResult.Kochance(false);
                        var teraMinPct = defenderMaxHp > 0 ? (double)teraMin / defenderMaxHp * 100.0 : 0.0;
                        var teraMaxPct = defenderMaxHp > 0 ? (double)teraMax / defenderMaxHp * 100.0 : 0.0;
                        var teraKoText = FormatKoChance(teraKoChance, teraNHko);

                        stringBuilder.AppendLine($"  ✨ With Tera ({activeSlot.CanTerastallize}): {teraMinPct:F1}% - {teraMaxPct:F1}% -> {teraKoText}");
                    }
                }
            }
            catch (Exception ex)
            {
                stringBuilder.AppendLine($"Move {i + 1}: {moveState.Name}{statusNote} (Calc unavailable: {ex.Message})");
            }
        }
        stringBuilder.AppendLine();
    }

    private static void AppendOpponentAttacksOnUs(StringBuilder stringBuilder, BattlePokemonState ourPokemon,
        OpponentPokemonState opponent, OpponentPrediction prediction)
    {
        if (!CalcPokemonFactory.TryBuildOurPokemon(ourPokemon, out var defender) ||
            !CalcPokemonFactory.TryBuildOpponentPokemon(opponent, out var attacker, prediction?.Spread))
        {
            return;
        }

        var ourMaxHp = defender.MaxHP(false);
        if (ourMaxHp <= 0) return;

        var attacks = new List<string>();
        foreach (var predictedMove in prediction.Moves)
        {
            try
            {
                var calcMove = new Move(CalcPokemonFactory.Generation, predictedMove.Name);
                if (calcMove.Category == MoveCategories.Status) continue;

                var result = Calc.Calculate(CalcPokemonFactory.Generation, attacker, defender, calcMove, null);
                var (minDmg, maxDmg) = result.Range();
                var (koChance, nHko, _) = result.Kochance(false);
                var minPct = (double)minDmg / ourMaxHp * 100.0;
                var maxPct = (double)maxDmg / ourMaxHp * 100.0;
                var koText = FormatKoChance(koChance, nHko);

                attacks.Add($"  {predictedMove.Name}: {minPct:F1}% - {maxPct:F1}% ({koText})");
            }
            catch
            {
                // Ignore
            }
        }

        if (attacks.Count > 0)
        {
            stringBuilder.AppendLine("--- OPPONENT EXPECTED DAMAGE ON OUR ACTIVE ---");
            foreach (var attack in attacks)
            {
                stringBuilder.AppendLine(attack);
            }
            stringBuilder.AppendLine();
        }
    }

    private static void AppendBenchAnalysis(StringBuilder stringBuilder, BattleContext context,
        OpponentPokemonState opponent, OpponentPrediction prediction)
    {
        var benchPokemon = new List<(int slotIndex, BattlePokemonState pokemon)>();
        for (var i = 0; i < context.SidePokemon.Count; i++)
        {
            var p = context.SidePokemon[i];
            if (!p.IsActive && !p.IsFainted && p.CurrentHp > 0)
            {
                benchPokemon.Add((i + 1, p));
            }
        }

        if (benchPokemon.Count == 0)
        {
            stringBuilder.AppendLine("--- OUR BENCH TEAMMATES ---");
            stringBuilder.AppendLine("  (No living bench Pokémon available)");
            stringBuilder.AppendLine();
            return;
        }

        stringBuilder.AppendLine("--- OUR BENCH TEAMMATES (SWITCH OPTIONS) ---");
        Pokemon calcOpponent = null;
        if (opponent != null)
        {
            CalcPokemonFactory.TryBuildOpponentPokemon(opponent, out calcOpponent, prediction?.Spread);
        }

        foreach (var (slotIndex, member) in benchPokemon)
        {
            var species = CalcPokemonFactory.ExtractSpeciesFromDetails(member.Details);
            var types = GetOurPokemonTypes(member);
            var typesStr = types.Count > 0 ? string.Join("/", types) : "Unknown";
            var hpPercent = member.MaxHp > 0 ? (double)member.CurrentHp / member.MaxHp * 100.0 : 0.0;
            var speStr = member.Stats != null ? $" | Spe: {member.Stats.Spe}" : "";

            stringBuilder.AppendLine($"Slot {slotIndex}: {species} [{typesStr}] - HP: {member.CurrentHp}/{member.MaxHp} ({hpPercent:F1}%)");
            stringBuilder.AppendLine($"  Ability: {member.Ability} | Item: {member.Item}{speStr}");
            stringBuilder.AppendLine($"  Moves: {string.Join(", ", member.Moves)}");

            var chip = ComputeSwitchInChipPercent(types, context.OwnSideStealthRock, context.OwnSideSpikesLayers);
            if (chip > 0)
            {
                stringBuilder.AppendLine($"  ⚠️ Hazard chip: takes {chip:F1}% max HP on switch-in");
            }

            if (calcOpponent != null && CalcPokemonFactory.TryBuildOurPokemon(member, out var calcMember) && prediction?.Moves != null)
            {
                var hits = new List<string>();
                foreach (var predMove in prediction.Moves)
                {
                    try
                    {
                        var move = new Move(CalcPokemonFactory.Generation, predMove.Name);
                        if (move.Category == MoveCategories.Status) continue;

                        var result = Calc.Calculate(CalcPokemonFactory.Generation, calcOpponent, calcMember, move, null);
                        var (minDmg, maxDmg) = result.Range();
                        var maxHp = calcMember.MaxHP(false);
                        if (maxHp > 0)
                        {
                            var minPct = (double)minDmg / maxHp * 100.0;
                            var maxPct = (double)maxDmg / maxHp * 100.0;
                            hits.Add($"{predMove.Name}: {minPct:F1}-{maxPct:F1}%");
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }

                if (hits.Count > 0)
                {
                    stringBuilder.AppendLine($"  🛡️ Dmg from opponent: {string.Join(", ", hits)}");
                }
            }
        }
        stringBuilder.AppendLine();
    }

    private static void AppendOpponentBench(StringBuilder stringBuilder, BattleContext context)
    {
        var opponentBench = context.OpponentPokemon.Where(p => !p.IsActive).ToList();
        if (opponentBench.Count > 0)
        {
            stringBuilder.AppendLine("--- OPPONENT BENCH ---");
            foreach (var p in opponentBench)
            {
                var status = p.IsFainted ? "FAINTED" : $"{p.HpPercent:F0}% HP" + (string.IsNullOrEmpty(p.Status) ? "" : $" ({p.Status})");
                var types = GetOpponentTypes(p);
                var typesStr = types.Count > 0 ? string.Join("/", types) : "Unknown";
                stringBuilder.AppendLine($"  {p.Species} [{typesStr}] - {status}");
            }
            stringBuilder.AppendLine();
        }
    }

    private static void AppendLegalChoices(StringBuilder stringBuilder, BattleContext context,
        BattleActiveSlot activeSlot)
    {
        stringBuilder.AppendLine("--- LEGAL ACTIONS ---");
        if (activeSlot != null)
        {
            for (var i = 0; i < activeSlot.Moves.Count; i++)
            {
                var move = activeSlot.Moves[i];
                var isUsable = move.Name == "Recharge" || move.MaxPp == 0 || (!move.IsDisabled && move.Pp > 0);
                if (isUsable)
                {
                    stringBuilder.AppendLine($"- MOVE {i + 1}: Use {move.Name}");
                    if (!string.IsNullOrEmpty(activeSlot.CanTerastallize))
                    {
                        stringBuilder.AppendLine($"- MOVE {i + 1} TERA: Use {move.Name} (with Terastallize into {activeSlot.CanTerastallize})");
                    }
                }
            }

            if (!activeSlot.Trapped)
            {
                for (var i = 0; i < context.SidePokemon.Count; i++)
                {
                    var pokemon = context.SidePokemon[i];
                    if (!pokemon.IsActive && !pokemon.IsFainted && pokemon.CurrentHp > 0)
                    {
                        var species = CalcPokemonFactory.ExtractSpeciesFromDetails(pokemon.Details);
                        stringBuilder.AppendLine($"- SWITCH {i + 1}: Switch to {species}");
                    }
                }
            }
            else
            {
                stringBuilder.AppendLine("(Cannot switch: Pokémon is trapped)");
            }
        }
        stringBuilder.AppendLine();
    }

    private static IReadOnlyList<string> GetOurPokemonTypes(BattlePokemonState state)
    {
        if (CalcPokemonFactory.TryBuildOurPokemon(state, out var pokemon))
        {
            return pokemon.Types;
        }

        return [];
    }

    private static IReadOnlyList<string> GetOpponentTypes(OpponentPokemonState state)
    {
        if (CalcPokemonFactory.TryBuildOpponentPokemon(state, out var pokemon))
        {
            return pokemon.Types;
        }

        return [];
    }

    private static int ComputeOpponentSpeedEstimate(OpponentPokemonState state, PredictedSpread spread)
    {
        if (!CalcPokemonFactory.TryBuildOpponentPokemon(state, out var opponentPokemon, spread))
        {
            return 200;
        }

        var speed = (double)opponentPokemon.RawStats.Spe;
        if (state.Boosts.TryGetValue("spe", out var boost) && boost != 0)
        {
            speed *= boost > 0 ? (2.0 + boost) / 2.0 : 2.0 / (2.0 - boost);
        }

        if (state.Status == "par")
        {
            speed /= 2;
        }

        return (int)speed;
    }

    private static double ComputeSwitchInChipPercent(IReadOnlyList<string> types, bool stealthRock, int spikesLayers)
    {
        var chip = 0.0;
        if (stealthRock)
        {
            chip += 12.5 * TypeMatchupTable.GetMultiplier("Rock", types);
        }

        if (spikesLayers > 0 && IsGrounded(types))
        {
            chip += SpikesChipByLayers[Math.Clamp(spikesLayers, 0, 3)];
        }

        return chip;
    }

    private static bool IsGrounded(IReadOnlyList<string> types)
    {
        return types == null || !types.Any(t => t.Equals("Flying", StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatEffectiveness(double multiplier)
    {
        return multiplier switch
        {
            >= 4.0 => "4x Ultra Effective",
            >= 2.0 => "2x Super Effective",
            <= 0.0 => "0x Immune",
            <= 0.25 => "0.25x Not Very Effective",
            <= 0.5 => "0.5x Not Very Effective",
            _ => "1.0x Neutral"
        };
    }

    private static string FormatKoChance(double koChance, int nHko)
    {
        if (nHko == 1)
        {
            return koChance >= 1.0 ? "Guaranteed OHKO" : $"{koChance * 100.0:F1}% chance to OHKO";
        }
        if (nHko == 2)
        {
            return koChance >= 1.0 ? "Guaranteed 2HKO" : $"{koChance * 100.0:F1}% chance to 2HKO";
        }
        if (nHko > 2)
        {
            return $"{nHko}HKO";
        }

        return "Low damage";
    }
}
