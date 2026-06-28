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


    


    // FIND entire SyncWcMst action and REPLACE WITH:
    [HttpPost("SyncWcMst")]
    public async Task<IActionResult> SyncWcMst()
    {
        try
        {
            // 1. Sync WcMst to local WOMME_App (existing)
            var (inserted, updated) = await _syncService.SyncWcMstAsync();

            // 2. Sync WomWcEmployee: SyteLine (15) → ManhourApplication (27)
            var womInserted = await _syncService.SyncWomWcEmployeeAsync();

            return Ok(new
            {
                message = "Wc_mst and WomWcEmployee sync completed successfully.",
                wcInsertedCount = inserted.Count,
                wcUpdatedCount = updated.Count,
                womInsertedCount = womInserted.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error syncing Wc_mst or WomWcEmployee.",
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

        private static DateTime GetSafeSqlDateTime(DateTime value)
        {
            var minSqlDate = new DateTime(1753, 1, 1);
            return value < minSqlDate ? minSqlDate : value;
        }

        

        [HttpPost("SyncRestTables")]
        public async Task<IActionResult> SyncRestTables()
        {
            try
            {

                var connStr = _localContext.Database.GetConnectionString();

                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("EXEC SyncRestTablesFromSyteline", conn);
                cmd.CommandTimeout = 300; // 5 minutes — SP can take time on large data

                await cmd.ExecuteNonQueryAsync();
                

                return Ok(new
                {
                    Status  = "Success",
                    Message = "SyncRestTablesFromSyteline SP executed successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status  = "Failed",
                    Message = ex.Message
                });
            }
        }



       [HttpPost("SyncAllTablesFromSyteline")]
        public async Task<IActionResult> SyncAllTablesFromSyteline()
        {
            try
            {
                var connStr = _localContext.Database.GetConnectionString();

                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("EXEC SyncJobTablesFromSyteline", conn);
                cmd.CommandTimeout = 300; // 5 minutes — SP can take time on large data

                await cmd.ExecuteNonQueryAsync();

                return Ok(new
                {
                    Status  = "Success",
                    Message = "SyncJobTablesFromSyteline SP executed successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status  = "Failed",
                    Message = ex.Message
                });
            }
        }



    }

 
 