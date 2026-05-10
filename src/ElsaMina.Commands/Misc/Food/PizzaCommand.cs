using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("pizza")]
public class PizzaCommand : Command
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

    private readonly IRandomService _randomService;

    public PizzaCommand(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        int count;
        if (!string.IsNullOrWhiteSpace(context.Target))
        {
            if (!int.TryParse(context.Target.Trim(), out count) || count <= 0)
            {
                context.ReplyLocalizedMessage("pizza_invalid_number");
                return Task.CompletedTask;
            }

            if (count > TOPPINGS.Length)
            {
                context.ReplyLocalizedMessage("pizza_too_many_toppings");
                return Task.CompletedTask;
            }
        }
        else
        {
            count = _randomService.NextInt(1, 6);
        }

        var selected = TOPPINGS.OrderBy(_ => _randomService.NextDouble()).Take(count);
        context.Reply(string.Join(", ", selected));
        return Task.CompletedTask;
    }
}
