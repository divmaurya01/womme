using Microsoft.EntityFrameworkCore;
using WommeAPI.Models;

namespace WommeAPI.Data
{
    public class ManhourDbContext : DbContext
    {
        public ManhourDbContext(DbContextOptions<ManhourDbContext> options) : base(options) { }

        public DbSet<WomWcEmployee> WomWcEmployee { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WomWcEmployee>()
                .HasKey(e => new { e.Wc, e.EmpNum });

            base.OnModelCreating(modelBuilder);
        }
    }
}