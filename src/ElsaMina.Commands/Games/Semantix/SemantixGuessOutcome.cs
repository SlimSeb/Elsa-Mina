namespace ElsaMina.Commands.Games.Semantix;

public enum SemantixGuessOutcome
{
    Accepted,
    Won,
    NotOwner,
    RoundNotActive,
    EmptyWord,
    NotInWordList,
    AlreadyGuessed,
    OnCooldown,
    EmbeddingUnavailable
}
