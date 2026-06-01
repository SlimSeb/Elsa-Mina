namespace ElsaMina.Commands.Games.Wordle;

public enum WordleGuessOutcome
{
    Accepted,
    NotOwner,
    RoundNotActive,
    InvalidLength,
    NotAlphabetic,
    NotInWordList,
    AlreadyGuessed
}
