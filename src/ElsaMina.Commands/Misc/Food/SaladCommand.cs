using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("salad")]
public class SaladCommand : RandomFoodPickerCommand
{
    private static readonly string[] INGREDIENTS =
    [
        "Lettuce", "Tomatoes", "Cucumbers", "Carrots", "Bell peppers",
        "Red onions", "Broccoli", "Mushrooms", "Corn", "Avocado",
        "Black beans", "Chickpeas", "Kidney beans", "Quinoa", "Cauliflower",
        "Radishes", "Green beans", "Snap peas", "Beets", "Feta cheese",
        "Sunflower seeds", "Pumpkin seeds", "Cranberries", "Walnuts", "Almonds",
        "Pecans", "Cashews", "Pine nuts", "Sesame seeds", "Poppy seeds",
        "Raisins", "Apricots", "Apples", "Oranges", "Strawberries",
        "Blueberries", "Raspberries", "Goji berries", "Mandarin oranges", "Peaches",
        "Pears", "Pomegranate seeds", "Grapes", "Cheddar cheese", "Parmesan cheese",
        "Blue cheese", "Gorgonzola cheese", "Mozzarella cheese", "Goat cheese",
        "Balsamic vinaigrette", "Ranch dressing", "Caesar dressing", "Italian dressing", "Thousand Island dressing"
    ];

    public SaladCommand(IRandomService randomService) : base(randomService) { }

    public override string HelpMessageKey => "salad_help";
    protected override string[] Items => INGREDIENTS;
    protected override int DefaultMinCount => 3;
    protected override int DefaultMaxCount => 8;
    protected override string InvalidCountMessageKey => "salad_invalid_number";
    protected override string TooManyItemsMessageKey => "salad_too_many_ingredients";
}
