using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.DataAccess;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }

    public DbSet<RoomUser> RoomUsers { get; set; }
    public DbSet<Badge> Badges { get; set; }
    public DbSet<AddedCommand> AddedCommands { get; set; }
    public DbSet<SavedRoom> RoomInfo { get; set; }
    public DbSet<BadgeHolding> BadgeHoldings { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<RoomTeam> RoomTeams { get; set; }
    public DbSet<ArcadeLevel> ArcadeLevels { get; set; }
    public DbSet<UserPoints> UserPoints { get; set; }
    public DbSet<SavedPoll> SavedPolls { get; set; }
    public DbSet<SavedTournament> SavedTournaments { get; set; }
    public DbSet<RoomBotParameterValue> RoomBotParameterValues { get; set; }
    public DbSet<TournamentRecord> TournamentRecords { get; set; }
    public DbSet<SavedUser> Users { get; set; }
    public DbSet<VoltorbFlipLevel> VoltorbFlipLevels { get; set; }
    public DbSet<ShopItem> ShopItems { get; set; }
    public DbSet<WatchlistEntry> WatchlistEntries { get; set; }
    public DbSet<StaffIntro> StaffIntros { get; set; }
    public DbSet<NameColor> NameColors { get; set; }
    public DbSet<TourConfig> TourConfigs { get; set; }
    public DbSet<EventRoleMapping> EventRoleMappings { get; set; }
    public DbSet<LightsOutScore> LightsOutScores { get; set; }
    public DbSet<PollSuggestion> PollSuggestions { get; set; }
    public DbSet<PollSuggestionBan> PollSuggestionBans { get; set; }
    public DbSet<FloodItScore> FloodItScores { get; set; }
    public DbSet<TwentyFortyEightScore> TwentyFortyEightScores { get; set; }
    public DbSet<ConnectFourRating> ConnectFourRatings { get; set; }
    public DbSet<LadderEloSnapshot> LadderEloSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}