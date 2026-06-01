
using Microsoft.EntityFrameworkCore;
using WommeAPI.Models;

namespace WommeAPI.Data
{
    public class SytelineDbContext : DbContext
    {
        public SytelineDbContext(DbContextOptions<SytelineDbContext> options)
            : base(options) { }

        public DbSet<JobMst> JobMst { get; set; }
        public DbSet<JobRouteMst> JobRouteMst { get; set; }
        public DbSet<JobmatlMst> JobMatlMst { get; set; }
        public DbSet<WcMst> WcMst { get; set; }
        public DbSet<JobTranMst> JobTranMst { get; set; }
        public DbSet<JobSchMst> JobSchMst { get; set; }
        public DbSet<WomWcEmployee> WomWcEmployee { get; set; }
        public DbSet<ItemMst> ItemMst { get; set; }
        public DbSet<EmployeeMstSource> EmployeeMstSource { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Composite keys
            modelBuilder.Entity<WomWcEmployee>()
                .HasKey(e => new { e.Wc, e.EmpNum });

            modelBuilder.Entity<WcMst>()
                .HasKey(w => new { w.site_ref, w.wc });

            modelBuilder.Entity<JobRouteMst>()
                .HasKey(j => new { j.Job, j.Suffix, j.OperNum, j.SiteRef });

            modelBuilder.Entity<JobTranMst>()
                .HasKey(j => new { j.site_ref, j.trans_num });

            modelBuilder.Entity<JobmatlMst>()
                .HasKey(j => new { j.Job, j.Item });

            modelBuilder.Entity<JobSchMst>()
                .HasKey(i => new { i.Job, i.Suffix });

            modelBuilder.Entity<EmployeeMstSource>()
                .HasKey(e => e.emp_num);

            base.OnModelCreating(modelBuilder);
        }
    }
    
}


