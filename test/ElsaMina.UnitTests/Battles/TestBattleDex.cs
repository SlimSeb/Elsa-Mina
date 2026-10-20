using ElsaMina.Battles.Tracking;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Http;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles;

/// <summary>
/// The real Showdown data (pokedex.json, moves.json from the test output), loaded once for the battle tests.
/// </summary>
public static class TestBattleDex
{
    private static readonly Lazy<BattleDex> DEX = new(Load);

    public static BattleDex Instance => DEX.Value;

    private static BattleDex Load()
    {
        var dexManager = new DexManager(Substitute.For<IHttpService>());
        dexManager.LoadDexAsync().GetAwaiter().GetResult();
        return new BattleDex(dexManager);
    }
}
