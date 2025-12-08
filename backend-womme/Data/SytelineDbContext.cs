
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
        // public DbSet<EmployeeMst> EmployeeMst { get; set; }
        public DbSet<JobTranMst> JobTranMst { get; set; }
        public DbSet<JobSchMst> JobSchMst { get; set; }
        public DbSet<WomWcEmployee> WomWcEmployee { get; set; } 
        public DbSet<ItemMst> ItemMst { get; set; }  
         public DbSet<EmployeeMstSource> EmployeeMstSource { get; set; }
       
    }
    
}


