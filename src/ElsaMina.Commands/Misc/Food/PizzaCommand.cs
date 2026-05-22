using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("pizza")]
public class PizzaCommand : RandomFoodPickerCommand
{
    private static readonly string[] TOPPINGS =
    [
        "Pepperoni", "Mushrooms", "Onions", "Sausage", "Bacon",
        "Extra cheese", "Black olives", "Green peppers", "Pineapple", "Spinach",
        "Artichoke hearts", "Anchovies", "Chicken", "Ham", "Salami",
        "Tomatoes", "Basil", "Garlic", "Red peppers", "Feta cheese",
        "Jalapenos", "Sun-dried tomatoes", "Pesto", "Provolone cheese", "Ricotta cheese",
        "Goat cheese", "Parmesan cheese", "Gorgonzola cheese", "Buffalo mozzarella", "Fontina cheese",
        "Blue cheese", "Barbecue sauce", "Alfredo sauce", "Olives", "Pimentos",
        "Canadian bacon", "Cornmeal", "Oregano", "Marinara sauce", "White onions",
        "Red onions", "Poblano peppers", "Cheddar cheese", "Garlic butter", "Shrimp",
        "Pesto sauce", "Tofu", "Cherry tomatoes", "Fresh basil", "Capers",
        "Pine nuts", "Olive oil", "Rosemary", "Cilantro", "Jalapeno peppers"
    ];

    public PizzaCommand(IRandomService randomService) : base(randomService) { }

    public override string HelpMessageKey => "pizza_help";
    protected override string[] Items => TOPPINGS;
    protected override int DefaultMinCount => 1;
    protected override int DefaultMaxCount => 6;
    protected override string InvalidCountMessageKey => "pizza_invalid_number";
    protected override string TooManyItemsMessageKey => "pizza_too_many_toppings";
}
