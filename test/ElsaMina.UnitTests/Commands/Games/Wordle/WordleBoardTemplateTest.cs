using System.Globalization;
using ElsaMina.Commands.Games.Wordle;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using RazorLight;

namespace ElsaMina.UnitTests.Commands.Games.Wordle;

/// <summary>
/// Renders the real Wordle board template. The main thing under test is the anti spoiler contract:
/// the guess input must whisper the word to the bot, never send it to the room.
/// </summary>
[TestFixture]
public class WordleBoardTemplateTest
{
    private RazorLightEngine _engine;
    private IWordleGame _game;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var resourcesService = Substitute.For<IResourcesService>();
        resourcesService.GetString(Arg.Any<string>(), Arg.Any<CultureInfo>())
            .Returns(callInfo => callInfo.ArgAt<string>(0));
        var containerService = Substitute.For<IDependencyContainerService>();
        containerService.Resolve<IResourcesService>().Returns(resourcesService);
        DependencyContainerService.Current = containerService;

        _engine = new RazorLightEngineBuilder()
            .UseFileSystemProject(Path.Join(Environment.CurrentDirectory, "Templates"))
            .SetOperatingAssembly(typeof(WordleModel).Assembly)
            .UseMemoryCachingProvider()
            .Build();
    }

    [SetUp]
    public void SetUp()
    {
        var owner = Substitute.For<IUser>();
        owner.UserId.Returns("player");
        owner.Name.Returns("Player");

        _game = Substitute.For<IWordleGame>();
        _game.Owner.Returns(owner);
        _game.Guesses.Returns([]);
        _game.KeyboardStates.Returns(new Dictionary<char, WordleLetterState>());
        _game.MaxGuesses.Returns(6);
        _game.WordLength.Returns(5);
        _game.CurrentInput.Returns(string.Empty);
        _game.IsRoundActive.Returns(true);
    }

    private Task<string> RenderAsync(bool isPrivateMode) => _engine.CompileRenderAsync(
        "Games/Wordle/WordleBoard.cshtml",
        new WordleModel
        {
            Culture = new CultureInfo("en-US"),
            CurrentGame = _game,
            BotName = "Bot",
            Trigger = "-",
            RoomId = "myroom",
            IsPrivateMode = isPrivateMode
        });

    [TestCase(true)]
    [TestCase(false)]
    public async Task Test_Render_ShouldWhisperTheGuess_WhateverTheMode(bool isPrivateMode)
    {
        // Act
        var html = await RenderAsync(isPrivateMode);

        // Assert: the guess is whispered to the bot, so spectators never see the word
        Assert.That(html, Does.Contain("data-submitsend=\"/w Bot,-wlg myroom, {word}\""));
        Assert.That(html, Does.Contain("name=\"word\""));
    }

    [Test]
    public async Task Test_Render_ShouldNotExposeTheAnswer_WhileRoundIsActive()
    {
        // Arrange
        _game.Answer.Returns("CRANE");
        _game.RevealedAnswer.Returns((string)null);

        // Act
        var html = await RenderAsync(isPrivateMode: false);

        // Assert
        Assert.That(html, Does.Not.Contain("CRANE"));
    }

    [Test]
    public async Task Test_Render_ShouldHideTheInput_WhenRoundIsOver()
    {
        // Arrange
        _game.IsRoundActive.Returns(false);
        _game.RevealedAnswer.Returns("CRANE");

        // Act
        var html = await RenderAsync(isPrivateMode: false);

        // Assert
        Assert.That(html, Does.Not.Contain("data-submitsend"));
    }
}
