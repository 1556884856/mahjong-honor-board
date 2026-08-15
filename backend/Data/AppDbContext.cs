using Microsoft.EntityFrameworkCore;
using MahjongApi.Models;

namespace MahjongApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(e =>
        {
            e.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<GamePlayer>(e =>
        {
            e.HasIndex(gp => new { gp.GameId, gp.PlayerId }).IsUnique();

            e.HasOne(gp => gp.Game)
                .WithMany(g => g.GamePlayers)
                .HasForeignKey(gp => gp.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(gp => gp.Player)
                .WithMany()
                .HasForeignKey(gp => gp.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
