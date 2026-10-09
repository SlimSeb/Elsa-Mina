using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Rooms.Parameters;

public class RoomParameterStore : IRoomParameterStore
{
    private readonly IRoomParameterRepository _repository;
    private readonly IReadOnlyDictionary<Parameter, IParameterDefinition> _parameterDefinitions;
    private readonly Lock _valuesLock = new();
    private Dictionary<string, string> _values;
    private string _roomId;

    public RoomParameterStore(IRoomParameterRepository repository, IParametersDefinitionFactory definitionFactory)
    {
        _repository = repository;
        _parameterDefinitions = definitionFactory.GetParametersDefinitions();
    }

    public IRoom Room { get; set; }

    public void Initialize(string roomId, IReadOnlyDictionary<string, string> storedValues)
    {
        lock (_valuesLock)
        {
            _roomId = roomId;
            _values = new Dictionary<string, string>(storedValues);
        }
    }

    public Task<string> GetValueAsync(Parameter parameter, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<string>(cancellationToken);
        }

        EnsureInitialized();
        var parameterDefinition = _parameterDefinitions[parameter];
        lock (_valuesLock)
        {
            return Task.FromResult(_values.GetValueOrDefault(parameterDefinition.Identifier)
                                   ?? parameterDefinition.DefaultValue);
        }
    }

    public async Task<bool> SetValueAsync(Parameter parameter, string value,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var parameterDefinition = _parameterDefinitions[parameter];

        if (!IsValueValid(parameterDefinition, value))
        {
            Log.Warning("Rejected invalid value '{Value}' for parameter {Parameter} in room {RoomId}",
                value, parameter, _roomId);
            return false;
        }

        if (!await _repository.TrySaveParameterValueAsync(_roomId, parameterDefinition.Identifier, value,
                cancellationToken))
        {
            return false;
        }

        lock (_valuesLock)
        {
            _values[parameterDefinition.Identifier] = value;
        }

        // La valeur est déjà validée, donc l'effet de bord peut pas throw sur une valeur légale
        // La room est peut-être pas encore branchée pendant l'init, dans ce cas y a rien à appliquer
        if (Room != null)
        {
            parameterDefinition.OnUpdateAction?.Invoke(Room, value);
        }

        return true;
    }

    private void EnsureInitialized()
    {
        if (_values == null)
        {
            throw new InvalidOperationException(
                $"{nameof(RoomParameterStore)} was used before {nameof(Initialize)} was called.");
        }
    }

    private static bool IsValueValid(IParameterDefinition parameterDefinition, string value)
    {
        return parameterDefinition.Type switch
        {
            RoomBotConfigurationType.Enumeration => parameterDefinition.PossibleValues != null
                                                    && parameterDefinition.PossibleValues.Any(possible =>
                                                        possible.InternalValue == value),
            _ => true
        };
    }
}
