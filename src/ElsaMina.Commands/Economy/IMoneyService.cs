namespace ElsaMina.Commands.Economy;

public interface IMoneyService
{
    /// <summary>
    /// The amount of bucks a user starts with before earning or spending anything.
    /// </summary>
    const long DEFAULT_BALANCE = 100;

    /// <summary>
    /// Returns the user's bucks balance in the given room, or <see cref="DEFAULT_BALANCE"/> if they
    /// have no data yet.
    /// </summary>
    Task<long> GetBalanceAsync(string roomId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds (or, with a negative amount, removes) bucks from the user's balance in the given room,
    /// creating their room data first if needed. Returns the new balance.
    /// </summary>
    Task<long> AddAsync(string roomId, string userId, long amount, CancellationToken cancellationToken = default);
}
