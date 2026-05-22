using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("randsoup")]
public class RandSoupCommand : RandomRecipeCommand
{
    public RandSoupCommand(ISpoonacularService spoonacularService) : base(spoonacularService) { }

    public override string HelpMessageKey => "randsoup_help";
    protected override string Tag => "soup";
}
