using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.GuessingGame.PokeCries;

public class PokeCriesGame : GuessingGame
{
    private const int MAX_MON_ID = 807;
    private const double EASTER_EGG_PROBABILITY = 0.025;

    private static readonly List<EasterEgg> EASTER_EGGS =
    [
        new()
        {
            Message =
                "<audio controls src=\"https://www.myinstants.com/media/sounds/euh-nique-ta-mere-marine-le-pen.mp3\"></audio>",
            Answers = ["Marine le Pen", "Le Pen"]
        },
        new()
        {
            Message =
                "<audio controls src=\"https://www.myinstants.com/media/sounds/nous-sommes-en-guerre_-macron-contre-le-coronavirus-2.mp3\"></audio>",
            Answers = ["Macron, Emmanuel Macron"]
        },
        new()
        {
            Message =
                "<audio controls src=\"https://www.myinstants.com/media/sounds/parce-que-cest-notre-projet_KwtXTe2.mp3\"></audio>",
            Answers = ["Macron", "Emmanuel Macron"]
        },
        new()
        {
            Message =
                "<audio controls src=\"https://www.myinstants.com/media/sounds/sardoche-mais-voila-mais-cetait-sur-en-fait_s9qhFsx.mp3\"></audio>",
            Answers = ["Sardoche"]
        }
    ];

    private readonly IRandomService _randomService;
    private readonly IDexManager _dexManager;

    public PokeCriesGame(ITemplatesManager templatesManager,
        IConfiguration configuration,
        IRandomService randomService,
        IDexManager dexManager,
        IClockService clockService) : base(templatesManager, configuration, clockService)
    {
        _randomService = randomService;
        _dexManager = dexManager;
    }

    public override string Identifier => nameof(PokeCriesGame);

    protected override void OnGameStart()
    {
        Context.ReplyLocalizedMessage("pokecries_started");
    }

    protected override Task OnTurnStart()
    {
        if (_randomService.NextDouble() < EASTER_EGG_PROBABILITY)
        {
            var chosenEasterEgg = _randomService.RandomElement(EASTER_EGGS);
            CurrentValidAnswers = chosenEasterEgg.Answers;
            Context.ReplyHtml(chosenEasterEgg.Message);

            return Task.CompletedTask;
        }

        var monId = _randomService.NextInt(1, MAX_MON_ID + 1);
        var message = $"<audio controls src=\"https://media.pokemoncentral.it/wiki/versi/{monId:D3}.mp3\"></audio>";
        if (monId < 0 || monId > _dexManager.Pokedex.Length)
        {
            return Task.CompletedTask; // Prevent crash but it shouldn't be possible
        }
        var pokemon = _dexManager.Pokedex[monId];
        CurrentValidAnswers = [pokemon.Name.French, pokemon.Name.English, pokemon.Name.Japanese];
        Context.ReplyHtml(message);

        return Task.CompletedTask;
    }
}