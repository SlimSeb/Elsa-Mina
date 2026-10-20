namespace ElsaMina.UnitTests.Battles.Strategies;

/// <summary>
/// Les choix attendus par les tests de stratégies, ici et pas dans les classes de test génériques
/// (un champ static d'un type générique existe une fois par type construit)
/// </summary>
internal static class ExpectedChoices
{
    internal static readonly IReadOnlyList<int> ONE = [1];
    internal static readonly IReadOnlyList<int> TWO = [2];
    internal static readonly IReadOnlyList<int> THREE = [3];
    internal static readonly IReadOnlyList<int> ONE_AND_ONE = [1, 1];
    internal static readonly IReadOnlyList<int> ONE_AND_TWO = [1, 2];
    internal static readonly IReadOnlyList<int> ONE_AND_THREE = [1, 3];
    internal static readonly IReadOnlyList<int> TWO_AND_TWO = [2, 2];
    internal static readonly IReadOnlyList<int> TWO_AND_THREE = [2, 3];
}
