using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Strategies;

// The random and type matchup strategies share the same decision flow
[TestFixture(typeof(RandomBattleDecisionService))]
[TestFixture(typeof(TypeMatchupBattleDecisionService))]
public class SimpleBattleDecisionServiceTest<TService> where TService : IBattleDecisionService
{
    private IRandomService _randomService;
    private IBattleDecisionService _service;
    private BattleContext _context;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        _randomService.RandomElement(Arg.Any<IEnumerable<int>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<int>>().First());
        _service = (IBattleDecisionService)Activator.CreateInstance(typeof(TService), _randomService);
        _context = new BattleContext("battle-gen9ou-1") { SideId = "p1" };
    }

    private static BattlePokemonState CreatePokemon(bool isActive = false, bool isFainted = false)
    {
        return new BattlePokemonState { IsActive = isActive, IsFainted = isFainted };
    }

    private static BattleActiveSlot CreateSlot(params bool[] disabledMoves)
    {
        return new BattleActiveSlot
        {
            Moves = disabledMoves.Select(isDisabled => new BattleMoveState { IsDisabled = isDisabled }).ToList()
        };
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenBattleIsOver()
    {
        // Arrange
        _context.IsBattleOver = true;
        _context.ActiveSlots = [CreateSlot(false)];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickRandomLead_WhenTeamPreview()
    {
        // Arrange
        _context.TeamPreview = true;
        _context.SidePokemon = [CreatePokemon(), CreatePokemon(), CreatePokemon()];
        _randomService.NextInt(1, 4).Returns(2);

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.TeamPreview));
            Assert.That(decision.Choices, Is.EqualTo(new[] { 2 }));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldSwitchToHealthyBenchedPokemon_WhenForcedToSwitch()
    {
        // Arrange
        _context.ForceSwitchSlots = [true];
        _context.SidePokemon = [CreatePokemon(isActive: true), CreatePokemon(isFainted: true), CreatePokemon()];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Switch));
            Assert.That(decision.Choices, Is.EqualTo(new[] { 3 }));
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickDistinctPokemon_WhenSeveralSlotsMustSwitch()
    {
        // Arrange
        _context.ForceSwitchSlots = [true, false, true];
        _context.SidePokemon = [CreatePokemon(), CreatePokemon()];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        Assert.That(decision.Choices, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenNotEnoughPokemonToSwitchIn()
    {
        // Arrange
        _context.ForceSwitchSlots = [true, true];
        _context.SidePokemon = [CreatePokemon(), CreatePokemon(isFainted: true)];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenNoPokemonCanSwitchIn()
    {
        // Arrange
        _context.ForceSwitchSlots = [true];
        _context.SidePokemon = [CreatePokemon(isActive: true), CreatePokemon(isFainted: true)];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickEnabledMove_WhenActiveSlotHasMoves()
    {
        // Arrange
        _context.ActiveSlots = [CreateSlot(true, false, false)];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision.Type, Is.EqualTo(BattleDecisionType.Move));
            Assert.That(decision.Choices, Is.EqualTo(new[] { 2 }));
            Assert.That(decision.UseTerastallize, Is.False);
        }
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldPickOneMovePerSlot_WhenSeveralActiveSlots()
    {
        // Arrange
        _context.ActiveSlots = [CreateSlot(false), CreateSlot(true, true, false)];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        Assert.That(decision.Choices, Is.EqualTo(new[] { 1, 3 }));
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenEveryMoveIsDisabled()
    {
        // Arrange
        _context.ActiveSlots = [CreateSlot(true, true)];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenActiveSlotHasNoMoves()
    {
        // Arrange
        _context.ActiveSlots = [CreateSlot()];

        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        Assert.That(decision, Is.Null);
    }

    [Test]
    public async Task Test_GetDecisionAsync_ShouldReturnNull_WhenNothingToDecide()
    {
        // Act
        var decision = await _service.GetDecisionAsync(_context);

        // Assert
        Assert.That(decision, Is.Null);
    }
}
