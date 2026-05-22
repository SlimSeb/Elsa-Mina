using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("randrecipe")]
public class RandRecipeCommand : RandomRecipeCommand
{
    public RandRecipeCommand(ISpoonacularService spoonacularService) : base(spoonacularService) { }

    public override string HelpMessageKey => "randrecipe_help";
}
