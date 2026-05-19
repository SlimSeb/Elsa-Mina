using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("salad")]
public class SaladCommand : Command
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

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "salad_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        int count;
        if (!string.IsNullOrWhiteSpace(context.Target))
        {
            if (!int.TryParse(context.Target.Trim(), out count) || count <= 0)
            {
                context.ReplyLocalizedMessage("salad_invalid_number");
                return Task.CompletedTask;
            }

            if (count > INGREDIENTS.Length)
            {
                context.ReplyLocalizedMessage("salad_too_many_ingredients");
                return Task.CompletedTask;
            }
        }
        else
        {
            count = Random.Shared.Next(3, 8);
        }

        var selected = INGREDIENTS.OrderBy(_ => Random.Shared.Next()).Take(count);
        context.Reply(string.Join(", ", selected));
        return Task.CompletedTask;
    }
}
