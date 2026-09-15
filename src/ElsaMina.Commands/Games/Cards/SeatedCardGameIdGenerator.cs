using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Hands out the incrementing game ids the seated card games build their panel ids from.
/// </summary>
/// <remarks>
/// One counter per seat type, so tarot, belote, président and poker each number their panels from 1
/// independently. Do not collapse this into a single shared counter: every panel id would silently shift.
/// </remarks>
public static class SeatedCardGameIdGenerator
{
    private static readonly ConcurrentDictionary<Type, StrongBox<int>> COUNTERS = new();

    /// <summary>
    /// The next id for the given seat type, starting at 1.
    /// </summary>
    public static int NextId(Type seatType)
    {
        var counter = COUNTERS.GetOrAdd(seatType, _ => new StrongBox<int>());
        return Interlocked.Increment(ref counter.Value);
    }
}
