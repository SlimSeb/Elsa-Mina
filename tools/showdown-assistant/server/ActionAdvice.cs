namespace ElsaMina.BattleAssistant;

/// <summary>
/// One legal choice and what the model thinks of it.
/// </summary>
/// <param name="Action">The model's action: 0-5 switch to a team slot, 6-9 a move, 10-13 terastallize and move.</param>
/// <param name="Kind">Whether it switches or uses a move.</param>
/// <param name="Label">The Pokémon switched in, or the move's name, as Showdown's buttons show them.</param>
/// <param name="Detail">Its HP (switch) or PP (move).</param>
/// <param name="Type">The move's type, null for a switch.</param>
/// <param name="Terastallize">Whether it terastallizes before moving.</param>
/// <param name="TeraType">The type it terastallizes into, null when it doesn't.</param>
/// <param name="Choice">The choice sent to Showdown, as in "/choose move 2 terastallize": the move's position in the
/// request, or the Pokémon's, from 1. The extension finds the button that makes it from there.</param>
/// <param name="Probability">The model's probability for it, among the legal choices.</param>
/// <param name="Veto">Why the guard rules it out (a move that already had no effect...), null when it doesn't.</param>
public sealed record ActionAdvice(
    int Action,
    ActionKind Kind,
    string Label,
    string Detail,
    string? Type,
    bool Terastallize,
    string? TeraType,
    string Choice,
    double Probability,
    string? Veto);
