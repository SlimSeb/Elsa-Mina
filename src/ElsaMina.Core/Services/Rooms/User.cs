using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.Rooms;

public sealed class User : IUser, IEquatable<User>
{
    private static readonly Dictionary<char, Rank> RANK_MAPPING = new()
    {
        [' '] = Rank.Regular,
        ['+'] = Rank.Voiced,
        ['♡'] = Rank.FormerStaff,
        ['%'] = Rank.Driver,
        ['@'] = Rank.Mod,
        ['*'] = Rank.Bot,
        ['#'] = Rank.RoomOwner,
        ['&'] = Rank.Leader,
        ['~'] = Rank.Admin
    };
    
    public static Rank GetRankFromCharacter(char character)
    {
        return RANK_MAPPING.TryGetValue(character, out var rank) ? rank : Rank.Regular;
    }

    public static char GetCharacterFromRank(Rank targetRank)
    {
        return RANK_MAPPING.FirstOrDefault(kvp => kvp.Value == targetRank).Key;
    }
    
    public static IUser FromUsername(string username)
    {
        var rank = RANK_MAPPING.TryGetValue(username[0], out var rankValue) ? rankValue : Rank.Regular;
        return new User(username[1..], rank);
    }
    
    public User(string name, Rank rank)
    {
        UserId = name.ToLowerAlphaNum();
        IsIdle = name.Length >= 2 && name[^2..] == "@!";
        Name = IsIdle ? name[..^2] : name;
        Rank = rank;
    }
    
    public string UserId { get; }
    public string Name { get; }
    public bool IsIdle { get; }
    public Rank Rank { get; }

    public bool Equals(User other)
    {
        return UserId == other?.UserId;
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((User)obj);
    }

    public override int GetHashCode()
    {
        return UserId != null ? UserId.GetHashCode() : 0;
    }
}