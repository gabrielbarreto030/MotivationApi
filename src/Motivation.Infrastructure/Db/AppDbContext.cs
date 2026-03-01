using Microsoft.EntityFrameworkCore;
using Motivation.Domain.Entities;

namespace Motivation.Infrastructure.Db
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<Step> Steps { get; set; }
        public DbSet<Motivation.Domain.Entities.Motivation> Motivations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.Property(u => u.Email).IsRequired();
                b.Property(u => u.PasswordHash).IsRequired();
            });

            modelBuilder.Entity<Goal>(b =>
            {
                b.HasKey(g => g.Id);
                b.Property(g => g.Title).IsRequired();
                b.Property(g => g.Description).IsRequired();
                b.HasMany<Step>().WithOne().HasForeignKey(s => s.GoalId);
            });

            modelBuilder.Entity<Step>(b =>
            {
                b.HasKey(s => s.Id);
                b.Property(s => s.Title).IsRequired();
            });

            modelBuilder.Entity<Motivation.Domain.Entities.Motivation>(b =>
            {
                b.HasKey(m => m.Id);
                b.Property(m => m.Text).IsRequired();
            });
        }
    }
}
