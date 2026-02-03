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
            var jobLastSync = await _syncService.GetLastSyncDate("JobMst");
            var routeLastSync = await _syncService.GetLastSyncDate("JobRouteMst");
            var empLastSync = await _syncService.GetLastSyncDate("EmployeeMst");
            var itemLastSync = await _syncService.GetLastSyncDate("ItemMst");
            var wcLastSync = await _syncService.GetLastSyncDate("WcMst");
            var womLastSync = await _syncService.GetLastSyncDate("WomWcEmployee");

            var results = new List<string>();

            _localContext.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                // ===================== 1️⃣ JOB_MST =====================
                var srcJobs = await _sourceContext.JobMst
                    .AsNoTracking()
                    .Where(x => x.RecordDate != null && x.RecordDate > jobLastSync)
                    .ToListAsync();

                var existingJobsSet = (await _localContext.JobMst
                    .AsNoTracking()
                    .Select(x => x.job)
                    .ToListAsync()).ToHashSet();

                int jobInserted = 0;
                foreach (var src in srcJobs)
                {
                    if (existingJobsSet.Contains(src.job)) continue;

                    _localContext.JobMst.Add(new JobMst
                    {
                        job = src.job,
                        RecordDate = GetSafeSqlDateTime(src.RecordDate)
                    });
                    jobInserted++;
                }

                await _localContext.SaveChangesAsync();
                results.Add($"Job_mst inserted: {jobInserted}");

                if (srcJobs.Any())
                    await _syncService.UpdateLastSyncDate("JobMst",
                        srcJobs.Max(x => x.RecordDate));

                // ===================== 2️⃣ JOB_ROUTE_MST =====================
                var srcRoutes = await _sourceContext.JobRouteMst
                    .AsNoTracking()
                    .Where(x => x.RecordDate != null && x.RecordDate > routeLastSync)
                    .ToListAsync();

                var existingRouteKeys = (await _localContext.JobRouteMst
                    .AsNoTracking()
                    .Select(x => $"{x.Job}|{x.Suffix}|{x.OperNum}|{x.SiteRef}")
                    .ToListAsync()).ToHashSet();

                int routeInserted = 0;
                foreach (var src in srcRoutes)
                {
                    var key = $"{src.Job}|{src.Suffix}|{src.OperNum}|{src.SiteRef}";
                    if (existingRouteKeys.Contains(key)) continue;

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

                if (srcRoutes.Any())
                    await _syncService.UpdateLastSyncDate("JobRouteMst",
                        srcRoutes.Max(x => x.RecordDate));

                // ===================== 3️⃣ EMPLOYEE_MST =====================
                var srcEmployees = await _sourceContext.EmployeeMstSource
                    .AsNoTracking()
                    .Where(x => x.RecordDate != null && x.RecordDate > empLastSync)
                    .ToListAsync();

                var existingEmployees = (await _localContext.EmployeeMst
                    .AsNoTracking()
                    .Select(x => x.emp_num)
                    .ToListAsync()).ToHashSet();

                int empInserted = 0;
                foreach (var src in srcEmployees)
                {
                    if (existingEmployees.Contains(src.emp_num)) continue;

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

                if (srcEmployees.Any())
                    await _syncService.UpdateLastSyncDate("EmployeeMst",
                        srcEmployees.Max(x => x.RecordDate));

                // ===================== 4️⃣ ITEM_MST =====================
                var srcItems = await _sourceContext.ItemMst
                    .AsNoTracking()
                    .Where(x => x.RecordDate != null && x.RecordDate > itemLastSync)
                    .ToListAsync();

                var existingItems = (await _localContext.ItemMst
                    .AsNoTracking()
                    .Select(x => x.item)
                    .ToListAsync()).ToHashSet();

                int itemInserted = 0;
                foreach (var src in srcItems)
                {
                    if (existingItems.Contains(src.item)) continue;

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

                if (srcItems.Any())
                    await _syncService.UpdateLastSyncDate("ItemMst",
                        srcItems.Max(x => x.RecordDate ?? DateTime.Now));

                // ===================== 5️⃣ WOM_WC_EMPLOYEE =====================
                var srcWom = await _sourceContext.WomWcEmployee
                    .AsNoTracking()
                    .Where(x => x.RecordDate != null && x.RecordDate > womLastSync)
                    .ToListAsync();

                var existingWom = (await _localContext.WomWcEmployee
                    .AsNoTracking()
                    .Select(x => x.EmpNum)
                    .ToListAsync()).ToHashSet();

                int womInserted = 0;
                foreach (var src in srcWom)
                {
                    if (existingWom.Contains(src.EmpNum)) continue;

                    _localContext.WomWcEmployee.Add(new WomWcEmployee
                    {
                        EmpNum = src.EmpNum,
                        RecordDate = GetSafeSqlDateTime(src.RecordDate)
                    });
                    womInserted++;
                }

                await _localContext.SaveChangesAsync();
                results.Add($"WomWcEmployee inserted: {womInserted}");

                if (srcWom.Any())
                    await _syncService.UpdateLastSyncDate("WomWcEmployee",
                        srcWom.Max(x => x.RecordDate));

                // ===================== 6️⃣ WC_MST =====================
                var srcWcs = await _sourceContext.WcMst
                    .AsNoTracking()
                    .Where(x => x.RecordDate != null && x.RecordDate > wcLastSync)
                    .ToListAsync();

                var existingWcs = (await _localContext.WcMst
                    .AsNoTracking()
                    .Select(x => x.wc)
                    .ToListAsync()).ToHashSet();

                int wcInserted = 0;
                foreach (var src in srcWcs)
                {
                    if (existingWcs.Contains(src.wc)) continue;

                    _localContext.WcMst.Add(new WcMst
                    {
                        wc = src.wc,
                        RecordDate = GetSafeSqlDateTime(src.RecordDate)
                    });
                    wcInserted++;
                }

                await _localContext.SaveChangesAsync();
                results.Add($"Wc_mst inserted: {wcInserted}");

                if (srcWcs.Any())
                    await _syncService.UpdateLastSyncDate("WcMst",
                        srcWcs.Max(x => x.RecordDate));

                return Ok(new
                {
                    Status = "Success",
                    Message = "Incremental sync completed successfully.",
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

 
 