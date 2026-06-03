namespace ElsaMina.Commands.Ai.Calc;

public interface IDamageCalculator
{
    /// <summary>
    /// Runs the damage calculation described by <paramref name="request"/> and returns the
    /// Smogon-style description string (e.g. <c>"252+ SpA Life Orb Gengar Sludge Bomb vs. …: 204-242 (30.6 - 36.3%) -- 52.9% chance to 3HKO"</c>).
    /// </summary>
    /// <exception cref="System.Exception">Thrown when a species, move or other input is invalid.</exception>
    string Calculate(CalcRequestDto request);
}
