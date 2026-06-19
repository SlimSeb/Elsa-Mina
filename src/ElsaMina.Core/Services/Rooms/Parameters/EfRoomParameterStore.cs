using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Core.Services.Rooms.Parameters;

public class EfRoomParameterStore : IRoomParameterStore
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IReadOnlyDictionary<Parameter, IParameterDefinition> _parameterDefinitions;
    private readonly Lock _parameterValuesLock = new();
    private SavedRoom _dbSavedRoom;

    public EfRoomParameterStore(IBotDbContextFactory dbContextFactory, IParametersDefinitionFactory definitionFactory)
    {
        _dbContextFactory = dbContextFactory;
        _parameterDefinitions = definitionFactory.GetParametersDefinitions();
    }

    public IRoom Room { get; set; }

    public void InitializeFromRoomEntity(SavedRoom savedRoomEntity)
    {
        _dbSavedRoom = savedRoomEntity;
    }

    public Task<string> GetValueAsync(Parameter parameter, CancellationToken cancellationToken = default)
        => cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<string>(cancellationToken)
            : Task.FromResult(GetCachedValue(parameter));

    private string GetCachedValue(Parameter parameter)
    {
        EnsureInitialized();
        var parameterDefinition = _parameterDefinitions[parameter];
        lock (_parameterValuesLock)
        {
            return _dbSavedRoom
                       .ParameterValues
                       .FirstOrDefault(parameterValue => parameterValue.ParameterId == parameterDefinition.Identifier)
                       ?.Value
                   ?? parameterDefinition.DefaultValue;
        }
    }

    public async Task<bool> SetValueAsync(Parameter parameter, string value,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var parameterDefinition = _parameterDefinitions[parameter];

        if (!IsValueValid(parameterDefinition, value))
        {
            Log.Warning(
                "Rejected invalid value '{Value}' for parameter {Parameter} in room {RoomId}",
                value, parameter, _dbSavedRoom.Id);
            return false;
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            lock (_parameterValuesLock)
            {
                var existing = _dbSavedRoom.ParameterValues
                    .FirstOrDefault(parameterValue => parameterValue.ParameterId == parameterDefinition.Identifier);

                if (existing == null)
                {
                    existing = new RoomBotParameterValue
                    {
                        RoomId = _dbSavedRoom.Id,
                        ParameterId = parameterDefinition.Identifier,
                        Value = value
                    };

                    _dbSavedRoom.ParameterValues.Add(existing);
                    dbContext.RoomBotParameterValues.Add(existing);
                }
                else
                {
                    existing.Value = value;
                    // The cached instance is detached, so the fresh context is not tracking it yet.
                    dbContext.RoomBotParameterValues.Update(existing);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // The value has already been validated, so the side effect cannot throw on a legal value.
            // Room may not be wired yet during initialization, in which case there is nothing to apply to.
            if (Room != null)
            {
                parameterDefinition.OnUpdateAction?.Invoke(Room, value);
            }

            return true;
        }
        catch (DbUpdateException ex)
        {
            Log.Error(ex,
                "Failed to set room parameter value for RoomId={RoomId}, Parameter={Parameter}",
                _dbSavedRoom.Id, parameter);

            return false;
        }
    }

    private void EnsureInitialized()
    {
        if (_dbSavedRoom == null)
        {
            throw new InvalidOperationException(
                $"{nameof(EfRoomParameterStore)} was used before {nameof(InitializeFromRoomEntity)} was called.");
        }
    }

    private static bool IsValueValid(IParameterDefinition parameterDefinition, string value)
    {
        switch (parameterDefinition.Type)
        {
            case RoomBotConfigurationType.Boolean:
                return bool.TryParse(value, out _);
            case RoomBotConfigurationType.Enumeration:
                return parameterDefinition.PossibleValues != null
                       && parameterDefinition.PossibleValues.Any(possible => possible.InternalValue == value);
            case RoomBotConfigurationType.String:
            default:
                return true;
        }
    }
}