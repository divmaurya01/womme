using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using WommeAPI.Models;

namespace WommeAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<AssignedJob> AssignedJob { get; set; }
        public DbSet<EmployeeLog> EmployeeLog { get; set; }
        //  public DbSet<ItemMaster> ItemMaster { get; set; }
        public DbSet<MachineMaster> MachineMaster { get; set; }
        //  public DbSet<OperationMaster> OperationMaster { get; set; }
        public DbSet<PageMaster> PageMaster { get; set; }
        public DbSet<RoleMaster> RoleMaster { get; set; }
        public DbSet<RolePageMapping> RolePageMapping { get; set; }
        public DbSet<StatusMaster> StatusMaster { get; set; }
        public DbSet<UserMaster> UserMaster { get; set; }
        public DbSet<UserToken> UserToken { get; set; }
        public DbSet<Job> Job { get; set; }
        public DbSet<PrintLog> PrintLog { get; set; }
        //  public DbSet<JobMaterial> JobMaterial { get; set; }
        public DbSet<JobMst> JobMst { get; set; }
        public DbSet<JobRouteMst> JobRouteMst { get; set; }
        public DbSet<JobmatlMst> JobMatlMst { get; set; }
        public DbSet<WcMst> WcMst { get; set; }
        public DbSet<EmployeeMst> EmployeeMst { get; set; }
        public DbSet<JobTranMst> JobTranMst { get; set; }
        public DbSet<JobSchMst> JobSchMst { get; set; }
        public DbSet<Calendar> Calendar { get; set; }
        public DbSet<ItemMst> ItemMst { get; set; }
        public DbSet<WomWcEmployee> WomWcEmployee { get; set; }
        public DbSet<WomMachineEmployee> WomMachineEmployee { get; set; }        
        public DbSet<JobPool> JobPool { get; set; }
        public DbSet<WomWcMachine> WomWcMachines { get; set; }
        public DbSet<Notification> Notification { get; set; }
        public DbSet<SyncLog> SyncLog { get; set; }


       


       
        
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AssignedJob>()
                .Property(a => a.AssignedHours)
                .HasPrecision(10, 2); // Allows values like 99999999.99

            modelBuilder.Entity<Job>()
                .Property(j => j.EstimatedHours)
                .HasPrecision(10, 2);

            modelBuilder.Entity<JobTranMst>()
              .Property(j => j.trans_num)
              .HasPrecision(11, 0);

            modelBuilder.Entity<JobTranMst>()
            .Property(j => j.qty_complete)
            .HasPrecision(19, 8);

            modelBuilder.Entity<JobTranMst>()
            .Property(j => j.qty_scrapped)
            .HasPrecision(19, 8);

            modelBuilder.Entity<JobTranMst>()
            .Property(j => j.a_hrs)
            .HasPrecision(19, 8);

            modelBuilder.Entity<JobTranMst>()
           .Property(j => j.a_dollar)
           .HasPrecision(20, 8);

            modelBuilder.Entity<JobTranMst>()
           .Property(j => j.qty_moved)
           .HasPrecision(19, 8);

            modelBuilder.Entity<JobTranMst>()
            .Property(j => j.pr_rate)
            .HasPrecision(9, 3);

            modelBuilder.Entity<JobTranMst>()
           .Property(j => j.job_rate)
           .HasPrecision(9, 3);

            modelBuilder.Entity<JobTranMst>()
           .Property(j => j.fixovhd)
           .HasPrecision(20, 8);

            modelBuilder.Entity<JobTranMst>()
           .Property(j => j.varovhd)
           .HasPrecision(20, 8);


            modelBuilder.Entity<JobSchMst>(entity =>
           {
               entity.Property(e => e.StartTick)
              .HasPrecision(10, 0);

               entity.Property(e => e.EndTick)
               .HasPrecision(10, 0);

           });


            modelBuilder.Entity<JobmatlMst>(entity =>
            {
                entity.Property(e => e.Cost)
               .HasPrecision(20, 8);

                entity.Property(e => e.QtyIssued)
              .HasPrecision(18, 8);

                entity.Property(e => e.ACost)
              .HasPrecision(20, 8);

                entity.Property(e => e.PoUnitCost)
              .HasPrecision(20, 8);

                entity.Property(e => e.QtyVar)
              .HasPrecision(18, 8);

                entity.Property(e => e.FixovhdT)
              .HasPrecision(20, 8);

                entity.Property(e => e.VarovhdT)
              .HasPrecision(20, 8);

                entity.Property(e => e.IncPrice)
              .HasPrecision(20, 8);

                entity.Property(e => e.MatlQtyConv)
              .HasPrecision(18, 8);

                entity.Property(e => e.IncPriceConv)
              .HasPrecision(18, 8);

                entity.Property(e => e.MatlCost)
              .HasPrecision(20, 8);

                entity.Property(e => e.LbrCost)
              .HasPrecision(20, 8);

                entity.Property(e => e.FovhdCost)
              .HasPrecision(20, 8);

                entity.Property(e => e.VovhdCost)
              .HasPrecision(20, 8);

                entity.Property(e => e.OutCost)
              .HasPrecision(20, 8);

                entity.Property(e => e.UfLastPoCost)
               .HasPrecision(19, 8);

                entity.Property(e => e.UfLastPoQtyOrd)
              .HasPrecision(19, 8);

                entity.Property(e => e.UfReservedJobMatl)
              .HasPrecision(19, 8);

            });


            modelBuilder.Entity<EmployeeMst>(entity =>
          {
              entity.Property(e => e.mfg_reg_rate)
             .HasPrecision(9, 3);

              entity.Property(e => e.mfg_ot_rate)
             .HasPrecision(9, 3);

              entity.Property(e => e.mfg_dt_rate)
             .HasPrecision(9, 3);

              entity.Property(e => e.salary)
             .HasPrecision(8, 2);

              entity.Property(e => e.reg_rate)
             .HasPrecision(9, 3);

              entity.Property(e => e.ot_rate)
             .HasPrecision(9, 3);

              entity.Property(e => e.dt_rate)
             .HasPrecision(9, 3);

              entity.Property(e => e.fwt_dol)
             .HasPrecision(8, 2);

              entity.Property(e => e.swt_dol)
             .HasPrecision(8, 2);

              entity.Property(e => e.Uf_Bonus)
             .HasPrecision(24, 8);

              entity.Property(e => e.ytd_tip_cr)
             .HasPrecision(10, 2);

              entity.Property(e => e.ytd_med)
             .HasPrecision(10, 2);

              entity.Property(e => e.ytd_swt)
             .HasPrecision(10, 2);

              entity.Property(e => e.ytd_fwt)
             .HasPrecision(10, 2);

              entity.Property(e => e.swt_dol)
             .HasPrecision(8, 2);

              entity.Property(e => e.fwt_dol)
             .HasPrecision(8, 2);

          });

            modelBuilder.Entity<JobRouteMst>(entity =>
           {
               entity.Property(e => e.WipAmt)
              .HasPrecision(20, 8);

               entity.Property(e => e.QtyScrapped)
              .HasPrecision(19, 8);

               entity.Property(e => e.QtyReceived)
              .HasPrecision(19, 8);

               entity.Property(e => e.QtyComplete)
              .HasPrecision(19, 8);

               entity.Property(e => e.FixovhdTLbr)
              .HasPrecision(20, 8);

               entity.Property(e => e.VarovhdTLbr)
              .HasPrecision(20, 8);

               entity.Property(e => e.VarovhdTMch)
              .HasPrecision(20, 8);

               entity.Property(e => e.RunHrsTLbr)
              .HasPrecision(20, 8);

               entity.Property(e => e.RunHrsVLbr)
              .HasPrecision(20, 8);

               entity.Property(e => e.RunHrsVMch)
              .HasPrecision(20, 8);

               entity.Property(e => e.RunCostTLbr)
              .HasPrecision(20, 8);

               entity.Property(e => e.SetupRate)
              .HasPrecision(10, 3);

               entity.Property(e => e.Efficiency)
              .HasPrecision(4, 1);

               entity.Property(e => e.VovhdRateMch)
              .HasPrecision(9, 3);

               entity.Property(e => e.RunRateLbr)
              .HasPrecision(10, 3);

               entity.Property(e => e.VarovhdRate)
              .HasPrecision(9, 3);

               entity.Property(e => e.FixovhdRate)
              .HasPrecision(9, 3);

               entity.Property(e => e.SetupHrsT)
              .HasPrecision(19, 8);

               entity.Property(e => e.SetupCostT)
              .HasPrecision(20, 8);

               entity.Property(e => e.SetupHrsV)
              .HasPrecision(20, 8);

           });


            modelBuilder.Entity<WcMst>(entity =>
          {
              entity.Property(e => e.setup_rate)
             .HasPrecision(10, 2);

              entity.Property(e => e.efficiency)
             .HasPrecision(4, 1);

              entity.Property(e => e.queue_hrs_a)
             .HasPrecision(19, 8);

              entity.Property(e => e.setup_rate_a)
             .HasPrecision(9, 3);

              entity.Property(e => e.setup_hrs_t)
             .HasPrecision(20, 8);

              entity.Property(e => e.decifld1)
             .HasPrecision(10, 2);

              entity.Property(e => e.decifld2)
             .HasPrecision(10, 2);

              entity.Property(e => e.decifld3)
             .HasPrecision(10, 2);

              entity.Property(e => e.queue_ticks)
             .HasPrecision(10, 0);

              entity.Property(e => e.run_hrs_t_mch)
             .HasPrecision(19, 8);

              entity.Property(e => e.fovhd_rate_mch)
             .HasPrecision(9, 3);

              entity.Property(e => e.vovhd_rate_mch)
             .HasPrecision(9, 3);

              entity.Property(e => e.run_hrs_t_lbr)
             .HasPrecision(19, 8);

              entity.Property(e => e.run_rate_lbr)
             .HasPrecision(10, 3);

              entity.Property(e => e.run_rate_a_lbr)
             .HasPrecision(9, 3);

              entity.Property(e => e.wip_matl_total)
             .HasPrecision(23, 8);

              entity.Property(e => e.wip_fovhd_total)
             .HasPrecision(23, 8);

              entity.Property(e => e.wip_vovhd_total)
             .HasPrecision(23, 8);

              entity.Property(e => e.wip_out_total)
             .HasPrecision(23, 8);

              entity.Property(e => e.queue_hrs)
             .HasPrecision(20, 8);

              entity.Property(e => e.queue_hrs_a)
             .HasPrecision(19, 8);

              entity.Property(e => e.finish_hrs)
              .HasPrecision(7, 2);

              entity.Property(e => e.wip_lbr_total)
                    .HasPrecision(23, 8);
          });


            modelBuilder.Entity<EmployeeMst>()
            .HasKey(e => new { e.emp_num, e.site_ref });

            modelBuilder.Entity<JobRouteMst>()
           .HasKey(j => new { j.Job, j.Suffix, j.OperNum, j.SiteRef });

            modelBuilder.Entity<JobTranMst>()
           .HasKey(j => new { j.site_ref, j.trans_num });

            modelBuilder.Entity<WcMst>()
           .HasKey(w => new { w.site_ref, w.wc });

            modelBuilder.Entity<JobmatlMst>()
            .HasKey(j => new { j.Job, j.Item });

            modelBuilder.Entity<WomWcEmployee>()
           .HasKey(e => new { e.Wc, e.EmpNum });

            modelBuilder.Entity<ItemMst>()
            .HasKey(i => new { i.item });

            modelBuilder.Entity<JobSchMst>()
          .HasKey(i => new { i.Job, i.Suffix });

            modelBuilder.Entity<WomMachineEmployee>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("wom_machine_employee");
            });
            

           

            base.OnModelCreating(modelBuilder);
        }

    }
    
}
