using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("randcheese")]
public class RandCheeseCommand : RandomRecipeCommand
{
    public RandCheeseCommand(ISpoonacularService spoonacularService) : base(spoonacularService) { }

    public override string HelpMessageKey => "randcheese_help";
    protected override string Tag => "cheese";
}
