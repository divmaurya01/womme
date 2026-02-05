using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WommeAPI.Data;
using WommeAPI.Models;
using WommeAPI.Services; 

namespace WommeAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeleteController : ControllerBase
{

    private readonly AppDbContext _context;

  private readonly SytelineService _sytelineService;
 
    public DeleteController(AppDbContext context,SytelineService sytelineService)
    {
        _context = context;
        _sytelineService = sytelineService;
 
    } 


    [HttpDelete("DeleteMachineMaster/{entryNo}")]
    public async Task<IActionResult> DeleteMachineMaster(int entryNo)
    {
        var record = await _context.MachineMaster.FindAsync(entryNo);
        if (record == null) return NotFound("MachineMaster not found");
        _context.MachineMaster.Remove(record);
        await _context.SaveChangesAsync();
        return Ok(new { massage = "MachineMaster deleted successfully." });
    }


    [HttpDelete("DeleteRoleMaster/{entryNo}")]
    public async Task<IActionResult> DeleteRoleMaster(int entryNo)
    {
        var record = await _context.RoleMaster.FindAsync(entryNo);
        if (record == null) return NotFound("RoleMaster not found");
        _context.RoleMaster.Remove(record);
        await _context.SaveChangesAsync();
        return Ok(new { message = "RoleMaster deleted successfully." });
    }


    [HttpDelete("DeleteRolePageMapping/{entryNo}")]
    public async Task<IActionResult> DeleteRolePageMapping(int entryNo)
    {
        var record = await _context.RolePageMapping.FindAsync(entryNo);
        if (record == null) return NotFound("RolePageMapping not found");
        _context.RolePageMapping.Remove(record);
        await _context.SaveChangesAsync();
        return Ok(new { message = "RolePageMapping deleted successfully." });
    }


    [HttpDelete("DeleteMachineEmployee/{machineNum}/{empNum}")]
    public async Task<IActionResult> DeleteMachineEmployee(string machineNum, string empNum)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(machineNum) || string.IsNullOrWhiteSpace(empNum))
                return BadRequest(new { message = "Machine number and employee number are required." });

            var rows = await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM wom_machine_employee WHERE MachineNumber = {0} AND emp_num = {1}",
                machineNum, empNum);

            if (rows == 0)
                return NotFound(new { message = "Mapping not found." });

            return Ok(new { message = "Machine-Employee mapping deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting.", error = ex.Message });
        }
    }

    [HttpDelete("DeleteMachineWC/{machineNum}/{wcCode}")]
    public async Task<IActionResult> DeleteMachineWC(string machineNum, string wcCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(machineNum) || string.IsNullOrWhiteSpace(wcCode))
                return BadRequest(new { message = "Machine number and WC code are required." });

            var rows = await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM wom_wc_machine WHERE machine_id = {0} AND wc = {1}",
                machineNum, wcCode);

            if (rows == 0)
                return NotFound(new { message = "Machine-WC mapping not found." });

            return Ok(new { message = "Machine-WC mapping deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting.", error = ex.Message });
        }
    }

    [HttpDelete("DeleteJobTransaction/{jobNumber}/{serialNo}/{operNum}")]
        public async Task<IActionResult> DeleteJobTransaction(
            string jobNumber,
            string serialNo,
            int operNum)
        {
            if (string.IsNullOrEmpty(jobNumber) ||
                string.IsNullOrEmpty(serialNo) ||
                operNum <= 0)
            {
                return BadRequest(new { message = "Missing parameters" });
            }

            var records = _context.JobTranMst
                .Where(j =>
                    j.job == jobNumber &&
                    j.SerialNo == serialNo &&
                    j.oper_num == operNum);

            if (!records.Any())
                return NotFound(new { message = "No Serial number found" });

            _context.JobTranMst.RemoveRange(records);
            await _context.SaveChangesAsync();

            bool deletedInSyteline = false;
            try
            {
                deletedInSyteline =
                    await _sytelineService.DeleteJobFromSytelineAsync(
                        jobNumber, serialNo, operNum);
            }
            catch { }

            return Ok(new
            {
                message = "Job deleted successfully",
                deletedInLocal = true,
                deletedInSyteline
            });
        }

 

  
    [HttpDelete("DeleteEmployeeWC/{empNum}/{wcCode}")]
    public async Task<IActionResult> DeleteEmployeeWC(string empNum, string wcCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(empNum) || string.IsNullOrWhiteSpace(wcCode))
                return BadRequest(new { message = "Employee number and WC code are required." });

            // Delete row from wom_wc_employee table
            var rows = await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM wom_wc_employee WHERE emp_num = {0} AND wc = {1}",
                empNum, wcCode);

            if (rows == 0)
                return NotFound(new { message = "Employee-WC mapping not found." });

            return Ok(new { message = "Employee-WC mapping deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting.", error = ex.Message });
        }
    }


}


