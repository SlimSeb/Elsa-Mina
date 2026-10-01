using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Rooms.Parameters;

public class RoomParameterStoreTest
{
    private const string ROOM_ID = "room";

    private static readonly Parameter BOOLEAN_PARAMETER = new("bool");
    private static readonly Parameter ENUMERATION_PARAMETER = new("enum");

    private IRoomParameterRepository _repository;
    private Action<IRoom, string> _onUpdateAction;
    private RoomParameterStore _store;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IRoomParameterRepository>();
        _repository.TrySaveParameterValueAsync(default, default, default).ReturnsForAnyArgs(true);
        _onUpdateAction = Substitute.For<Action<IRoom, string>>();

        var definitionFactory = Substitute.For<IParametersDefinitionFactory>();
        definitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            [BOOLEAN_PARAMETER] = new ParameterDefinition
            {
                Identifier = BOOLEAN_PARAMETER.Identifier,
                NameKey = "name",
                DescriptionKey = "description",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = "True",
                OnUpdateAction = _onUpdateAction
            },
            [ENUMERATION_PARAMETER] = new ParameterDefinition
            {
                Identifier = ENUMERATION_PARAMETER.Identifier,
                NameKey = "name",
                DescriptionKey = "description",
                Type = RoomBotConfigurationType.Enumeration,
                DefaultValue = "a",
                PossibleValues = [new EnumerationValue { InternalValue = "a" }, new EnumerationValue { InternalValue = "b" }]
            }
        });

        _store = new RoomParameterStore(_repository, definitionFactory);
    }

    [Test]
    public async Task Test_GetValueAsync_ShouldReturnStoredValue_WhenOneWasLoaded()
    {
        // Arrange
        _store.Initialize(ROOM_ID, new Dictionary<string, string> { ["bool"] = "False" });

        // Act
        var value = await _store.GetValueAsync(BOOLEAN_PARAMETER);

        // Assert
        Assert.That(value, Is.EqualTo("False"));
    }

    [Test]
    public async Task Test_GetValueAsync_ShouldReturnDefault_WhenNothingWasStored()
    {
        // Arrange
        _store.Initialize(ROOM_ID, new Dictionary<string, string>());

        // Act
        var value = await _store.GetValueAsync(BOOLEAN_PARAMETER);

        // Assert
        Assert.That(value, Is.EqualTo("True"));
    }

    [Test]
    public void Test_GetValueAsync_ShouldThrow_WhenNotInitialized()
    {
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _store.GetValueAsync(BOOLEAN_PARAMETER));
    }

    [Test]
    public async Task Test_SetValueAsync_ShouldSaveCacheAndApply_WhenValueIsValid()
    {
        // Arrange
        var room = Substitute.For<IRoom>();
        _store.Initialize(ROOM_ID, new Dictionary<string, string>());
        _store.Room = room;

        // Act
        var result = await _store.SetValueAsync(BOOLEAN_PARAMETER, "False");

        // Assert
        Assert.That(result, Is.True);
        await _repository.Received(1).TrySaveParameterValueAsync(ROOM_ID, "bool", "False", Arg.Any<CancellationToken>());
        Assert.That(await _store.GetValueAsync(BOOLEAN_PARAMETER), Is.EqualTo("False"));
        _onUpdateAction.Received(1).Invoke(room, "False");
    }

    [Test]
    public async Task Test_SetValueAsync_ShouldRejectValue_WhenItIsNotAPossibleEnumerationValue()
    {
        // Arrange
        _store.Initialize(ROOM_ID, new Dictionary<string, string>());

        // Act
        var result = await _store.SetValueAsync(ENUMERATION_PARAMETER, "z");

        // Assert
        Assert.That(result, Is.False);
        await _repository.DidNotReceiveWithAnyArgs().TrySaveParameterValueAsync(default, default, default);
    }

    [Test]
    public async Task Test_SetValueAsync_ShouldKeepPreviousValue_WhenSaveFails()
    {
        // Arrange
        _repository.TrySaveParameterValueAsync(default, default, default).ReturnsForAnyArgs(false);
        _store.Initialize(ROOM_ID, new Dictionary<string, string> { ["enum"] = "a" });
        _store.Room = Substitute.For<IRoom>();

        // Act
        var result = await _store.SetValueAsync(ENUMERATION_PARAMETER, "b");

        // Assert
        Assert.That(result, Is.False);
        Assert.That(await _store.GetValueAsync(ENUMERATION_PARAMETER), Is.EqualTo("a"));
    }
}
