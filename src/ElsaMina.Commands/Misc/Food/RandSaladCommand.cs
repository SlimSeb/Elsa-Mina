using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("randsalad")]
public class RandSaladCommand : RandomRecipeCommand
{
    public RandSaladCommand(ISpoonacularService spoonacularService) : base(spoonacularService) { }

    public override string HelpMessageKey => "randsalad_help";
    protected override string Tag => "salad";
}
