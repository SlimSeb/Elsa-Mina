namespace ElsaMina.Core.Services.Rooms.Parameters;

public class ParametersDefinitionFactory : IParametersDefinitionFactory
{
    private readonly Lazy<IReadOnlyDictionary<Parameter, IParameterDefinition>> _definitions;

    public ParametersDefinitionFactory(IEnumerable<IRoomParameterProvider> providers)
    {
        _definitions = new Lazy<IReadOnlyDictionary<Parameter, IParameterDefinition>>(() => BuildDefinitions(providers));
    }

    public IReadOnlyDictionary<Parameter, IParameterDefinition> GetParametersDefinitions() => _definitions.Value;

    private static IReadOnlyDictionary<Parameter, IParameterDefinition> BuildDefinitions(
        IEnumerable<IRoomParameterProvider> providers)
    {
        var definitions = new Dictionary<Parameter, IParameterDefinition>();
        foreach (var definition in providers.SelectMany(provider => provider.GetDefinitions()))
        {
            // Les valeurs sont stockées sous l'identifiant : deux paramètres avec le même => ils s'écrasent entre eux
            var parameter = new Parameter(definition.Identifier, definition.Name);
            if (definitions.ContainsKey(parameter))
            {
                throw new InvalidOperationException(
                    $"Room parameter identifier '{definition.Identifier}' is defined more than once");
            }

            definitions[parameter] = definition;
        }

        return definitions;
    }
}
