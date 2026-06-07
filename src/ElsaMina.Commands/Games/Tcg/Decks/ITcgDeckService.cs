using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Tcg.Decks;

public interface ITcgDeckService
{
    Task<IReadOnlyList<TcgDeck>> GetUserDecksAsync(string ownerId, CancellationToken cancellationToken = default);

    Task<TcgDeck> GetDeckAsync(string ownerId, string name, CancellationToken cancellationToken = default);

    Task<TcgDeckOperationResult> CreateAsync(string ownerId, string name,
        CancellationToken cancellationToken = default);

    Task<TcgDeckOperationResult> AddCardAsync(string ownerId, string name, string cardId,
        CancellationToken cancellationToken = default);

    Task<TcgDeckOperationResult> RemoveCardAsync(string ownerId, string name, string cardId,
        CancellationToken cancellationToken = default);

    Task<TcgDeckOperationResult> SetEnergyTypesAsync(string ownerId, string name,
        IReadOnlyList<TcgType> energyTypes, CancellationToken cancellationToken = default);

    Task<TcgDeckOperationResult> DeleteAsync(string ownerId, string name,
        CancellationToken cancellationToken = default);
}
