using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.BattleAssistant;
using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeSearch;
using ElsaMina.Battles.Strategies.MiniPokeV2;
using ElsaMina.Battles.Tracking;
using ElsaMina.Logging;

// The local server behind the Firefox extension: it follows the player's battles with the bot's own tracker and
// scores each decision with a minipoke model (see ../README.md)
const string extensionOriginPrefix = "moz-extension://";

var builder = WebApplication.CreateBuilder(args);
var options = builder.Configuration.Get<AssistantOptions>() ?? new AssistantOptions();
if (options.Model.IsSearch() || options.Model == BattleDecisionStrategy.DamageCalc)
{
    throw new InvalidOperationException(
        $"--Model must be a minipoke model (MiniPokeV3_22M), not {options.Model}");
}

Log.Configuration = new AssistantLoggingConfiguration();
var dexManager = new ShowdownDataDexManager();
await dexManager.LoadDexAsync();
var dex = new BattleDex(dexManager);
var modelOptions = new MiniPokeModelOptions(options.Model,
    Path.Join(AppContext.BaseDirectory, "Models", options.Model.ToString()));

builder.Services.AddSingleton<IBattleDex>(dex);
builder.Services.AddSingleton(modelOptions);
builder.Services.AddSingleton<IBattlesConfiguration>(options);
builder.Services.AddSingleton<IBattleMessageParser>(_ => new BattleMessageParser(dex));
builder.Services.AddSingleton<IMiniPokeModel>(_ => new MiniPokeOnnxModel(modelOptions));
builder.Services.AddSingleton<IMiniPokeEncoder>(_ => new MiniPokeV2Encoder(modelOptions, dex));
builder.Services.AddSingleton(_ => new MiniPokeActionGuard(dex));
builder.Services.AddSingleton<IShowdownSimulator>(_ => new ShowdownSimulator(options));
builder.Services.AddSingleton(services => new MiniPokeTurnSearch(services.GetRequiredService<IShowdownSimulator>(),
    services.GetRequiredService<IBattleMessageParser>()));
builder.Services.AddSingleton<BattleAdvisor>();
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
// The extension's background page calls from its moz-extension:// origin
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(origin => origin.StartsWith(extensionOriginPrefix, StringComparison.Ordinal))
    .WithMethods("GET", "POST", "DELETE")
    .WithHeaders("Content-Type")));

var app = builder.Build();
app.UseCors();

app.MapGet("/status", (BattleAdvisor advisor) => advisor.GetStatus());

app.MapPost("/battles/{roomId}/frames", (string roomId, FramesRequest request, BattleAdvisor advisor) =>
    BattleAdvisor.IsBattleRoom(roomId)
        ? Results.Ok(advisor.ApplyFrames(roomId, request.Frames ?? [], request.Reset))
        : Results.BadRequest("Not a battle room"));

app.MapPost("/battles/{roomId}/search",
    async (string roomId, SearchRequest request, BattleAdvisor advisor, CancellationToken cancellationToken) =>
        BattleAdvisor.IsBattleRoom(roomId)
            ? Results.Ok(await advisor.SearchAsync(roomId, request.Rqid, cancellationToken))
            : Results.BadRequest("Not a battle room"));

app.MapDelete("/battles/{roomId}", (string roomId, BattleAdvisor advisor) =>
{
    advisor.Forget(roomId);
    return Results.NoContent();
});

// Load the model now rather than on the first battle
var status = app.Services.GetRequiredService<BattleAdvisor>().GetStatus();
app.Services.GetRequiredService<IMiniPokeEncoder>();
Log.Information("{Model} {State}; one-turn search {Search}", status.Model,
    status.Loaded ? "loaded" : "NOT loaded", status.CanSearch ? "on" : "off (no --ShowdownDirectory)");

await app.RunAsync();
