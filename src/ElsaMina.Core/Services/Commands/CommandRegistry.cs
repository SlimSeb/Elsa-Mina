using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Commands;

public class CommandRegistry : ICommandRegistry
{
    private readonly Lazy<(Dictionary<string, ICommand> ByName, IReadOnlyCollection<ICommand> Commands)> _index;

    // Lazy : les commandes dépendent de services qui dépendent de l'executor, donc construites seulement au premier usage
    public CommandRegistry(Lazy<IEnumerable<ICommand>> commands)
    {
        _index = new Lazy<(Dictionary<string, ICommand>, IReadOnlyCollection<ICommand>)>(() => BuildIndex(commands.Value));
    }

    public IReadOnlyCollection<ICommand> Commands => _index.Value.Commands;

    public ICommand Find(string nameOrAlias)
    {
        if (string.IsNullOrEmpty(nameOrAlias))
        {
            return null;
        }

        return _index.Value.ByName.GetValueOrDefault(nameOrAlias);
    }

    private static (Dictionary<string, ICommand> ByName, IReadOnlyCollection<ICommand> Commands) BuildIndex(IEnumerable<ICommand> commands)
    {
        var byName = new Dictionary<string, ICommand>();
        var all = new List<ICommand>();
        foreach (var command in commands)
        {
            all.Add(command);
            foreach (var name in (string[])[command.Name, ..command.Aliases ?? []])
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (byName.TryGetValue(name, out var existing) && existing != command)
                {
                    Log.Warning("Command name '{0}' is used by both {1} and {2}, keeping the latter",
                        name, existing.GetType().Name, command.GetType().Name);
                }

                byName[name] = command;
            }
        }

        // Une commande dont tous les noms ont été pris par une autre enregistrée après est plus atteignable
        var reachable = byName.Values.ToHashSet();
        return (byName, all.Where(reachable.Contains).Distinct().ToList());
    }
}
