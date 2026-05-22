using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("randpasta")]
public class RandPastaCommand : RandomRecipeCommand
{
    public RandPastaCommand(ISpoonacularService spoonacularService) : base(spoonacularService) { }

    public override string HelpMessageKey => "randpasta_help";
    protected override string Tag => "pasta";
}
