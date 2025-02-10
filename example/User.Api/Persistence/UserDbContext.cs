using Microsoft.EntityFrameworkCore;

namespace User.Api.Persistence
{
    public class UserDbContext : DbContext
    {
        private static bool _created = false;

        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
        {
            if (!_created)
            {
                _created = true;
                Database.EnsureDeleted();
                Database.EnsureCreated();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Users>()
                .HasIndex(b => b.Email)
                .IsUnique();
        }
        
        public DbSet<Users> Users { get; set; }
    }
}