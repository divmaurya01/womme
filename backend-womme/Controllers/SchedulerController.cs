using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Data.SqlTypes; 
using Microsoft.Data.SqlClient; 
using WommeAPI.Services;
using WommeAPI.Data;
using WommeAPI.Models;

namespace WommeAPI.Controllers;

[ApiController]
[Route("api/scheduler")]
public class SchedulerController : ControllerBase
{
    private readonly DataSyncScheduler _scheduler;
    private readonly SyncService _syncService; // Add SyncService

    private readonly AppDbContext _localContext;
   private readonly SytelineDbContext _sourceContext;


    public SchedulerController(DataSyncScheduler scheduler, SyncService syncService,
                            AppDbContext localContext, SytelineDbContext sourceContext)
    {
        _scheduler = scheduler;
        _syncService = syncService;
        _localContext = localContext;
        _sourceContext = sourceContext;
    }


    // [HttpPost("StartScheduler")]
    // public IActionResult StartScheduler()
    // {
    //     _scheduler.Start();
    //     return Ok("Scheduler started manually.");
    // }

     //  Sync only Job_mstSyn
    [HttpPost("SyncJobMst")]
    public async Task<IActionResult> SyncJobMst()
    {
        try
        {
            var (insertedRecords, updatedRecords) = await _syncService.SyncJobMstAsync();

            return Ok(new
            {
                message = "Job_mst sync completed successfully.",
                insertedCount = insertedRecords.Count,
                updatedCount = updatedRecords.Count,
                insertedJobs = insertedRecords.Select(j => j.job).ToList(),
                updatedJobs = updatedRecords.Select(j => j.job).ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error syncing Job_mst.",
                error = ex.Message
            });
        }
    }


    // Sync only Item_mst
    [HttpPost("SyncItemMst")]
    public async Task<IActionResult> SyncItemMst()
    {
        try
        {
            await _syncService.SyncItemMstAsync();
            return Ok("Item_mst sync completed successfully.");
        }
        catch (SqlNullValueException ex)
        {
            return StatusCode(500, $"Error syncing Item_mst: Null value in column. Details: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error syncing Item_mst: {ex.Message}");
        }
    }


     // Sync only Employee_mst
    [HttpPost("SyncEmployeeMst")]
    public async Task<IActionResult> SyncEmployeeMst()
    {
        try
        {
            var (insertedRecords, updatedRecords) = await _syncService.SyncEmployeeMstAsync();

            return Ok(new
            {
                message = "Employee_mst sync completed successfully.",
                insertedCount = insertedRecords.Count,
                updatedCount = updatedRecords.Count,
                insertedEmployees = insertedRecords.Select(e => e.emp_num).ToList(),
                updatedEmployees = updatedRecords.Select(e => e.emp_num).ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error syncing Employee_mst",
                error = ex.Message
            });
        }
    }


    // Sync only Jobmatl_mst
    // [HttpPost("SyncJobMatlMst")]
    // public async Task<IActionResult> SyncJobMatlMst()
    // {
    //     try
    //     {
    //         await _syncService.SyncJobMatlMstAsync(); // call only jobmatl_mst sync
    //         return Ok("Jobmatl_mst sync completed successfully.");
    //     }
    //     catch (Exception ex)
    //     {
    //         return StatusCode(500, $"Error syncing Jobmatl_mst: {ex.Message}");
    //     }
    // }


     [HttpPost("SyncWcMst")]
    public async Task<IActionResult> SyncWcMst()
    {
        try
        {
            var (inserted, updated) = await _syncService.SyncWcMstAsync();

            return Ok(new
            {
                message = "Wc_mst sync completed successfully.",
                insertedCount = inserted.Count,
                updatedCount = updated.Count,
                insertedWCs = inserted.Select(w => w.wc).ToList(),
                updatedWCs = updated.Select(w => w.wc).ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error syncing Wc_mst.",
                error = ex.Message
            });
        }
    }
    
    // [HttpPost("SyncJobTranMst")]
    // public async Task<IActionResult> SyncJobTranMst()
    // {
    //     try
    //     {
    //         await _syncService.SyncJobTranMstAsync(); // call only jobtran_mst sync
    //         return Ok("Jobtran_mst sync completed successfully.");
    //     }
    //     catch (Exception ex)
    //     {
    //         return StatusCode(500, $"Error syncing Jobtran_mst: {ex.Message}");
    //     }
    // }


    [HttpPost("SyncWomWcEmployee")]
    public async Task<IActionResult> SyncWomWcEmployee()
    {
        try
        {
            await _syncService.SyncWomWcEmployeeAsync(); // call only WomWcEmployee sync
            return Ok("WomWcEmployee sync completed successfully.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error syncing WomWcEmployee: {ex.Message}");
        }
    }


    [HttpPost("SyncJobRouteMst")]
    public async Task<IActionResult> SyncJobRouteMst()
    {
        try
        {
            await _syncService.SyncJobRouteAsync(); // call only jobroute_mst sync
            return Ok("JobRouteMst sync completed successfully.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error syncing JobRouteMst: {ex.Message}");
        }
    }


    // [HttpPost("SyncJobSchMst")]
    // public async Task<IActionResult> SyncJobSchMst()
    // {
    //     try
    //     {
    //         await _syncService.SyncJobSchMstAsync(); // call only jobsch_mst sync
    //         return Ok("JobSchMst sync completed successfully.");
    //     }
    //     catch (Exception ex)
    //     {
    //         return StatusCode(500, $"Error syncing JobSchMst: {ex.Message}");
    //     }
    // }


    //  New Combined Sync API


    // [HttpPost("SyncAllTablesAfterSept2025")]
    // public async Task<IActionResult> SyncAllTablesAfterSept2025()
    // {
    //     var syncResults = new List<string>();
    //     var cutoffDate = new DateTime(2025, 11, 1);

    //     try
    //     {
    //         using var transaction = await _localContext.Database.BeginTransactionAsync();

    //         // ------------------- 1️⃣ Job_mst -------------------
    //        // 1️⃣ Job_mst
    //         var jobMstData = await _sourceContext.JobMst
    //             .AsNoTracking()
    //             .Where(x => x.RecordDate >= cutoffDate)
    //             .ToListAsync();

    //         foreach (var src in jobMstData)
    //         {
    //             // Check if local context is already tracking this entity
    //             var tracked = _localContext.ChangeTracker
    //                 .Entries<JobMst>()
    //                 .FirstOrDefault(e => e.Entity.job == src.job);

    //             if (tracked != null)
    //             {
    //                 // Detach it first to avoid duplicate tracking
    //                 _localContext.Entry(tracked.Entity).State = EntityState.Detached;
    //             }

    //             // Try to find existing in DB
    //             var existing = await _localContext.JobMst
    //                 .FirstOrDefaultAsync(j => j.job == src.job);

    //             if (existing != null)
    //             {
    //                 existing.RecordDate = GetSafeSqlDateTime(src.RecordDate);                    
    //                 _localContext.JobMst.Update(existing);
    //             }
    //             else
    //             {
    //                 await _localContext.JobMst.AddAsync(new JobMst
    //                 {
    //                     job = src.job,
    //                     RecordDate = src.RecordDate
    //                     // map other properties
    //                 });
    //             }
    //         }

    //         foreach (var entry in _localContext.ChangeTracker.Entries())
    //         {
    //             foreach (var prop in entry.Properties)
    //             {
    //                 if (prop.Metadata.ClrType == typeof(DateTime))
    //                 {
    //                     var dt = (DateTime)prop.CurrentValue;
    //                     if (dt < new DateTime(1753, 1, 1))
    //                         Console.WriteLine($"⚠️ Out-of-range datetime in {entry.Entity.GetType().Name}.{prop.Metadata.Name}: {dt}");
    //                 }
    //             }
    //         }
    //         await _localContext.SaveChangesAsync();

    //         syncResults.Add($"Job_mst synced ({jobMstData.Count} records).");


    //         // ------------------- 2️⃣ JobRouteMst (4-part composite key) -------------------
    //         var jobRouteData = await _sourceContext.JobRouteMst
    //             .AsNoTracking()
    //             .Where(x => x.RecordDate >= cutoffDate)
    //             .ToListAsync();

    //         foreach (var src in jobRouteData)
    //         {
    //             var existing = await _localContext.JobRouteMst
    //                 .FirstOrDefaultAsync(jr =>
    //                     jr.Job == src.Job &&
    //                     jr.Suffix == src.Suffix &&
    //                     jr.OperNum == src.OperNum &&
    //                     jr.SiteRef == src.SiteRef
    //                 );

    //             if (existing != null)
    //             {
    //                 existing.RecordDate = GetSafeSqlDateTime(src.RecordDate);
    //                 existing.CreateDate = GetSafeSqlDateTime(src.CreateDate);
    //                 existing.CreatedBy = string.IsNullOrEmpty(src.CreatedBy) ? "SYSTEM" : src.CreatedBy;
    //                 existing.UpdatedBy = string.IsNullOrEmpty(src.UpdatedBy) ? "SYSTEM" : src.UpdatedBy;
    //                 existing.Wc = string.IsNullOrEmpty(src.Wc) ? "UNKNOWN" : src.Wc;
    //                 _localContext.JobRouteMst.Update(existing);
    //             }

    //             else
    //             {
    //                 await _localContext.JobRouteMst.AddAsync(new JobRouteMst
    //                 {
    //                     Job = src.Job,
    //                     Suffix = src.Suffix,
    //                     OperNum = src.OperNum,
    //                     SiteRef = src.SiteRef,
    //                     RecordDate = GetSafeSqlDateTime(src.RecordDate),
    //                     CreateDate = GetSafeSqlDateTime(src.CreateDate),
    //                    CreatedBy = string.IsNullOrEmpty(src.CreatedBy) ? "SYSTEM" : src.CreatedBy,
    //                    UpdatedBy = string.IsNullOrEmpty(src.UpdatedBy) ? "SYSTEM" : src.UpdatedBy,
    //                    Wc = string.IsNullOrEmpty(src.Wc) ? "UNKNOWN" : src.Wc,
    //                     // map other properties
    //                 });
    //             }
    //         }
    //         foreach (var entry in _localContext.ChangeTracker.Entries())
    //         {
    //             foreach (var prop in entry.Properties)
    //             {
    //                 if (prop.Metadata.ClrType == typeof(DateTime))
    //                 {
    //                     var dt = (DateTime)prop.CurrentValue;
    //                     if (dt < new DateTime(1753, 1, 1))
    //                         Console.WriteLine($"⚠️ Out-of-range datetime in {entry.Entity.GetType().Name}.{prop.Metadata.Name}: {dt}");
    //                 }
    //             }
    //         }
    //         await _localContext.SaveChangesAsync();
    //         syncResults.Add($"Job_route synced ({jobRouteData.Count} records).");

    //         // ------------------- 3️⃣ Employee_mst -------------------
    //         var empData = await _sourceContext.EmployeeMstSource
    //             .AsNoTracking()
    //             .Where(x => x.RecordDate >= cutoffDate)
    //             .ToListAsync();

    //         foreach (var src in empData)
    //         {
    //             var existing = await _localContext.EmployeeMst
    //                 .FirstOrDefaultAsync(e => e.emp_num == src.emp_num);

    //             if (existing != null)
    //             {
    //                 existing.name = src.name;
    //                 existing.RecordDate = GetSafeSqlDateTime(src.RecordDate);
    //                 _localContext.EmployeeMst.Update(existing);
    //             }
    //             else
    //             {
    //                 await _localContext.EmployeeMst.AddAsync(new EmployeeMst
    //                 {
    //                     emp_num = src.emp_num,
    //                     name = src.name,
    //                     RecordDate = src.RecordDate
    //                     // map other fields
    //                 });
    //             }
    //         }
    //         foreach (var entry in _localContext.ChangeTracker.Entries())
    //         {
    //             foreach (var prop in entry.Properties)
    //             {
    //                 if (prop.Metadata.ClrType == typeof(DateTime))
    //                 {
    //                     var dt = (DateTime)prop.CurrentValue;
    //                     if (dt < new DateTime(1753, 1, 1))
    //                         Console.WriteLine($"⚠️ Out-of-range datetime in {entry.Entity.GetType().Name}.{prop.Metadata.Name}: {dt}");
    //                 }
    //             }
    //         }
    //         await _localContext.SaveChangesAsync();
    //         syncResults.Add($"Employee_mst synced ({empData.Count} records).");

    //         // ------------------- 4️⃣ Item_mst -------------------
    //             // ------------------- 4️⃣ Item_mst -------------------
    //         var itemData = await _sourceContext.ItemMst
    //             .AsNoTracking()
    //             .Where(x => x.RecordDate >= cutoffDate)
    //             .ToListAsync();

    //         int syncedCount = 0;

    //         foreach (var src in itemData)
    //         {
    //             // Skip if PK is null
    //             if (string.IsNullOrEmpty(src.item))
    //                 continue;

    //             // Try to find existing item by PK
    //             var existing = await _localContext.ItemMst
    //                 .FirstOrDefaultAsync(i => i.item == src.item);

    //             if (existing != null)
    //             {
    //                 existing.description = src.description;
    //                 existing.RecordDate = GetSafeSqlDateTime(src.RecordDate);
    //                 existing.Auto_Job = string.IsNullOrEmpty(src.Auto_Job) ? "N" : src.Auto_Job; // ✅ Default to "N"
    //                 existing.Auto_Post = src.Auto_Post ?? "N";
    //                 _localContext.ItemMst.Update(existing);
    //             }
    //             else
    //             {
    //                 await _localContext.ItemMst.AddAsync(new ItemMst
    //                 {
    //                     item = src.item,
    //                     description = src.description,
    //                     RecordDate = src.RecordDate,
    //                     Auto_Job = string.IsNullOrEmpty(src.Auto_Job) ? "N" : src.Auto_Job, // ✅ ensure non-null
    //                     Auto_Post = src.Auto_Post ?? "N"
    //                 });
    //             }

    //             syncedCount++;
    //         }


    //         foreach (var entry in _localContext.ChangeTracker.Entries())
    //         {
    //             foreach (var prop in entry.Properties)
    //             {
    //                 if (prop.Metadata.ClrType == typeof(DateTime))
    //                 {
    //                     var dt = (DateTime)prop.CurrentValue;
    //                     if (dt < new DateTime(1753, 1, 1))
    //                         Console.WriteLine($"⚠️ Out-of-range datetime in {entry.Entity.GetType().Name}.{prop.Metadata.Name}: {dt}");
    //                 }
    //             }
    //         }
    //         await _localContext.SaveChangesAsync();
    //         syncResults.Add($"Item_mst synced ({syncedCount} records).");



    //         // ------------------- 5️⃣ WomWcEmployee -------------------
    //         var womWcData = await _sourceContext.WomWcEmployee
    //             .AsNoTracking()
    //             .Where(x => x.RecordDate >= cutoffDate)
    //             .ToListAsync();

    //         foreach (var src in womWcData)
    //         {
    //             var existing = await _localContext.WomWcEmployee
    //                 .FirstOrDefaultAsync(w => w.EmpNum == src.EmpNum);

    //             if (existing != null)
    //             {
    //                 existing.RecordDate = GetSafeSqlDateTime(src.RecordDate);
    //                 _localContext.WomWcEmployee.Update(existing);
    //             }
    //             else
    //             {
    //                 await _localContext.WomWcEmployee.AddAsync(new WomWcEmployee
    //                 {
    //                     EmpNum = src.EmpNum,
    //                     RecordDate = src.RecordDate
    //                 });
    //             }
    //         }
    //         foreach (var entry in _localContext.ChangeTracker.Entries())
    //         {
    //             foreach (var prop in entry.Properties)
    //             {
    //                 if (prop.Metadata.ClrType == typeof(DateTime))
    //                 {
    //                     var dt = (DateTime)prop.CurrentValue;
    //                     if (dt < new DateTime(1753, 1, 1))
    //                         Console.WriteLine($"⚠️ Out-of-range datetime in {entry.Entity.GetType().Name}.{prop.Metadata.Name}: {dt}");
    //                 }
    //             }
    //         }
    //         await _localContext.SaveChangesAsync();
    //         syncResults.Add($"WomWcEmployee synced ({womWcData.Count} records).");

    //         // ------------------- 6️⃣ Wc_mst -------------------
    //         var wcData = await _sourceContext.WcMst
    //             .AsNoTracking()
    //             .Where(x => x.RecordDate >= cutoffDate)
    //             .ToListAsync();

    //         foreach (var src in wcData)
    //         {
    //             var existing = await _localContext.WcMst
    //                 .FirstOrDefaultAsync(w => w.wc == src.wc);

    //             if (existing != null)
    //             {
    //                 existing.RecordDate = GetSafeSqlDateTime(src.RecordDate);
    //                 _localContext.WcMst.Update(existing);
    //             }
    //             else
    //             {
    //                 await _localContext.WcMst.AddAsync(new WcMst
    //                 {
    //                     wc = src.wc,
    //                     RecordDate = src.RecordDate
    //                 });
    //             }
    //         }
    //         foreach (var entry in _localContext.ChangeTracker.Entries())
    //         {
    //             foreach (var prop in entry.Properties)
    //             {
    //                 if (prop.Metadata.ClrType == typeof(DateTime))
    //                 {
    //                     var dt = (DateTime)prop.CurrentValue;
    //                     if (dt < new DateTime(1753, 1, 1))
    //                         Console.WriteLine($"⚠️ Out-of-range datetime in {entry.Entity.GetType().Name}.{prop.Metadata.Name}: {dt}");
    //                 }
    //             }
    //         }
    //         await _localContext.SaveChangesAsync();

    //         syncResults.Add($"Wc_mst synced ({wcData.Count} records).");

    //         await transaction.CommitAsync();

    //         return Ok(new
    //         {
    //             Status = "Success",
    //             Message = "All tables synced successfully for records after September 2025.",
    //             Details = syncResults
    //         });
    //     }
    //     catch (Exception ex)
    //     {
    //         syncResults.Add($"❌ Error: {ex.Message}");
    //         return StatusCode(500, new
    //         {
    //             Status = "Failed",
    //             Message = "Error occurred during filtered sync.",
    //             Details = syncResults
    //         });
    //     }
    // }



    private static DateTime GetSafeSqlDateTime(DateTime? value)
    {
        var minSqlDate = new DateTime(1753, 1, 1);

        if (!value.HasValue || value.Value < minSqlDate)
            return minSqlDate;

        return value.Value;
    }

[HttpPost("SyncAllTablesAfterSept2025")]
public async Task<IActionResult> SyncAllTablesAfterSept2025()
{
    var cutoffDate = new DateTime(2025, 11, 1);
    var results = new List<string>();

    // ⚡ Performance optimization
    _localContext.ChangeTracker.AutoDetectChangesEnabled = false;

    try
    {
        // ===================== 1️⃣ JOB_MST =====================
        var srcJobs = await _sourceContext.JobMst
            .AsNoTracking()
            .Where(x => x.RecordDate >= cutoffDate)
            .ToListAsync();

        var existingJobsList = await _localContext.JobMst
            .AsNoTracking()
            .Select(x => x.job)
            .ToListAsync();
        var existingJobsSet = existingJobsList.ToHashSet();

        int jobInserted = 0;
        foreach (var src in srcJobs)
        {
            if (existingJobsSet.Contains(src.job))
                continue;

            _localContext.JobMst.Add(new JobMst
            {
                job = src.job,
                RecordDate = GetSafeSqlDateTime(src.RecordDate)
            });
            jobInserted++;
        }
        await _localContext.SaveChangesAsync();
        results.Add($"Job_mst inserted: {jobInserted}");

        // ===================== 2️⃣ JOB_ROUTE_MST =====================
        var srcRoutes = await _sourceContext.JobRouteMst
            .AsNoTracking()
            .Where(x => x.RecordDate >= cutoffDate)
            .ToListAsync();

        var existingRouteKeysList = await _localContext.JobRouteMst
            .AsNoTracking()
            .Select(x => $"{x.Job}|{x.Suffix}|{x.OperNum}|{x.SiteRef}")
            .ToListAsync();
        var existingRouteKeysSet = existingRouteKeysList.ToHashSet();

        int routeInserted = 0;
        foreach (var src in srcRoutes)
        {
            var key = $"{src.Job}|{src.Suffix}|{src.OperNum}|{src.SiteRef}";
            if (existingRouteKeysSet.Contains(key))
                continue;

            _localContext.JobRouteMst.Add(new JobRouteMst
            {
                Job = src.Job,
                Suffix = src.Suffix,
                OperNum = src.OperNum,
                SiteRef = src.SiteRef,
                RecordDate = GetSafeSqlDateTime(src.RecordDate),
                CreateDate = GetSafeSqlDateTime(src.CreateDate),
                CreatedBy = src.CreatedBy ?? "SYSTEM",
                UpdatedBy = src.UpdatedBy ?? "SYSTEM",
                Wc = src.Wc ?? "UNKNOWN"
            });
            routeInserted++;
        }
        await _localContext.SaveChangesAsync();
        results.Add($"JobRoute_mst inserted: {routeInserted}");

        // ===================== 3️⃣ EMPLOYEE_MST =====================
        var srcEmployees = await _sourceContext.EmployeeMstSource
            .AsNoTracking()
            .Where(x => x.RecordDate >= cutoffDate)
            .ToListAsync();

        var existingEmployeesList = await _localContext.EmployeeMst
            .AsNoTracking()
            .Select(x => x.emp_num)
            .ToListAsync();
        var existingEmployeesSet = existingEmployeesList.ToHashSet();

        int empInserted = 0;
        foreach (var src in srcEmployees)
        {
            if (existingEmployeesSet.Contains(src.emp_num))
                continue;

            _localContext.EmployeeMst.Add(new EmployeeMst
            {
                emp_num = src.emp_num,
                name = src.name,
                RecordDate = GetSafeSqlDateTime(src.RecordDate)
            });
            empInserted++;
        }
        await _localContext.SaveChangesAsync();
        results.Add($"Employee_mst inserted: {empInserted}");

        // ===================== 4️⃣ ITEM_MST =====================
        var srcItems = await _sourceContext.ItemMst
            .AsNoTracking()
            .Where(x => x.RecordDate >= cutoffDate && !string.IsNullOrEmpty(x.item))
            .ToListAsync();

        var existingItemsList = await _localContext.ItemMst
            .AsNoTracking()
            .Select(x => x.item)
            .ToListAsync();
        var existingItemsSet = existingItemsList.ToHashSet();

        int itemInserted = 0;
        foreach (var src in srcItems)
        {
            if (existingItemsSet.Contains(src.item))
                continue;

            _localContext.ItemMst.Add(new ItemMst
            {
                item = src.item,
                description = src.description,
                RecordDate = GetSafeSqlDateTime(src.RecordDate),
                Auto_Job = src.Auto_Job ?? "N",
                Auto_Post = src.Auto_Post ?? "N"
            });
            itemInserted++;
        }
        await _localContext.SaveChangesAsync();
        results.Add($"Item_mst inserted: {itemInserted}");

        // ===================== 5️⃣ WOM_WC_EMPLOYEE =====================
        var srcWomWc = await _sourceContext.WomWcEmployee
            .AsNoTracking()
            .Where(x => x.RecordDate >= cutoffDate)
            .ToListAsync();

        var existingWomKeysList = await _localContext.WomWcEmployee
            .AsNoTracking()
            .Select(x => x.EmpNum)
            .ToListAsync();
        var existingWomKeysSet = existingWomKeysList.ToHashSet();

        int womInserted = 0;
        foreach (var src in srcWomWc)
        {
            if (existingWomKeysSet.Contains(src.EmpNum))
                continue;

            _localContext.WomWcEmployee.Add(new WomWcEmployee
            {
                EmpNum = src.EmpNum,
                RecordDate = GetSafeSqlDateTime(src.RecordDate)
            });
            womInserted++;
        }
        await _localContext.SaveChangesAsync();
        results.Add($"WomWcEmployee inserted: {womInserted}");

        // ===================== 6️⃣ WC_MST =====================
        var srcWcs = await _sourceContext.WcMst
            .AsNoTracking()
            .Where(x => x.RecordDate >= cutoffDate)
            .ToListAsync();

        var existingWcsList = await _localContext.WcMst
            .AsNoTracking()
            .Select(x => x.wc)
            .ToListAsync();
        var existingWcsSet = existingWcsList.ToHashSet();

        int wcInserted = 0;
        foreach (var src in srcWcs)
        {
            if (existingWcsSet.Contains(src.wc))
                continue;

            _localContext.WcMst.Add(new WcMst
            {
                wc = src.wc,
                RecordDate = GetSafeSqlDateTime(src.RecordDate)
            });
            wcInserted++;
        }
        await _localContext.SaveChangesAsync();
        results.Add($"Wc_mst inserted: {wcInserted}");

        return Ok(new
        {
            Status = "Success",
            Message = "Insert-only sync completed successfully.",
            Details = results
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            Status = "Failed",
            Message = ex.Message,
            Details = results
        });
    }
    finally
    {
        _localContext.ChangeTracker.AutoDetectChangesEnabled = true;
    }
}



    }

 
 