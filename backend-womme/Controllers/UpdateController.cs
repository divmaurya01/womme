using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WommeAPI.Data;
using WommeAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Components.Routing;

namespace WommeAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UpdateController : ControllerBase
{

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public UpdateController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPut("UpdateMachineMaster/{entryNo}")]
    public async Task<IActionResult> UpdateMachineMaster(int entryNo, [FromBody] MachineMaster updatedMachine)
    {
        var existingMachine = await _context.MachineMaster.FindAsync(entryNo);
        if (existingMachine == null)
        {
            return NotFound(new { message = $"MachineMaster with EntryNo {entryNo} not found." });
        }

        existingMachine.MachineNumber = updatedMachine.MachineNumber;
        existingMachine.MachineName = updatedMachine.MachineName;
        existingMachine.MachineDescription = updatedMachine.MachineDescription;
        //  existingMachine.CreatedAt = updatedMachine.CreatedAt;
        existingMachine.UpdatedAt = updatedMachine.UpdatedAt;

        await _context.SaveChangesAsync();

        return Ok(new { message = "MachineMaster updated successfully." });
    }


    [HttpPut("UpdateUserProfileImages")]
    public async Task<IActionResult> UpdateUserProfileImages([FromForm] string emp_num, [FromForm] IFormFile profileImage)
    {
        if (string.IsNullOrWhiteSpace(emp_num))
            return BadRequest(new { message = "Employee number is required." });

        // Fix: compare against the passed emp_num, not itself
        var user = await _context.EmployeeMst.FirstOrDefaultAsync(u => u.emp_num == emp_num);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (profileImage != null && profileImage.Length > 0)
        {
            // Ensure directory exists
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ProfileImages");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Create unique file name
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(profileImage.FileName)}";
            var filePath = Path.Combine(folderPath, fileName);

            // Save the file to wwwroot/ProfileImages
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profileImage.CopyToAsync(stream);
            }

            // Save relative path to DB
            user.ProfileImage = $"ProfileImages/{fileName}";
            //  user.updatedAt = DateTime.Now;

            _context.EmployeeMst.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User profile image updated successfully.",
                imageUrl = $"{Request.Scheme}://{Request.Host}/ProfileImages/{fileName}"
            });
        }

        return BadRequest(new { message = "No profile image was provided." });
    }


    [HttpPut("UpdateEmployee/{empNum}")]
    public IActionResult UpdateEmployee(string empNum, [FromBody] EmployeeDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(empNum) || dto == null)
                return BadRequest(new { message = "Invalid request." });

            var employee = _context.EmployeeMst
                .FirstOrDefault(e => e.emp_num == empNum);

            if (employee == null)
                return NotFound(new { message = "Employee not found." });
             
            // UPDATE ONLY PASSWORD
            if (!string.IsNullOrWhiteSpace(dto.PasswordHash))
                employee.PasswordHash = dto.PasswordHash;

            // UPDATE ONLY ROLE
            if (dto.RoleID.HasValue)
                employee.RoleID = dto.RoleID.Value;

            if (dto.IsActive.HasValue)
                employee.IsActive = dto.IsActive.Value;

                   // UPDATE EMAIL
            if (!string.IsNullOrWhiteSpace(dto.Email))
                employee.email_addr = dto.Email;

            // Audit
            employee.RecordDate = DateTime.Now;
            employee.UpdatedBy = dto.CreatedBy ?? employee.UpdatedBy;

           

            _context.SaveChanges();

            return Ok(new { message = "Employee updated successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Server error",
                error = ex.Message
            });
        }
    }





    [HttpPut("EditMachineEmployee/{oldMachineNumber}")]
    public async Task<IActionResult> EditMachineEmployee(string oldMachineNumber, [FromBody] MachineEmployeeDto dto)
    {
        // Check if record exists
        var machine = await _context.WomMachineEmployee
            .FirstOrDefaultAsync(m => m.Machine_Num == oldMachineNumber);

        if (machine == null)
            return NotFound(new { message = "Machine not found." });

        string sql = @"
    UPDATE wom_machine_employee
    SET emp_num = {0},
        MachineNumber = {1},
        MachineDescription = {2},
        name = {3},
        updatedby = {4},
        recorddate = {5}
    WHERE MachineNumber = {6}";

        await _context.Database.ExecuteSqlRawAsync(sql,
            dto.Emp_Num!,
            dto.MachineNumber!,          // new machine number (assignment update)
            dto.MachineDescription!,
            dto.Name!,
            "system",
            DateTime.Now,
            oldMachineNumber);         // old machine number (lookup)

        return Ok(new { message = "Machine employee updated successfully." });
    }


    // [HttpPost("UpdateQCRemark")]
    // public async Task<IActionResult> UpdateQCRemark([FromBody] JobTran dto)
    // {
    //     try
    //     {
    //         string? connectionString = _configuration.GetConnectionString("DefaultConnection");

    //         if (string.IsNullOrEmpty(connectionString))
    //             return StatusCode(500, "Database connection string not configured.");

    //         using (var connection = new SqlConnection(connectionString))
    //         {
    //             string query = @"
    //             UPDATE jobtran_mst
    //             SET Remark = @Remark
    //             WHERE job = @Job
    //               AND trans_type = 'M'
    //               AND status = 1;";   // ✅ only when status = 1 (Running)

    //             using (var cmd = new SqlCommand(query, connection))
    //             {
    //                 cmd.Parameters.AddWithValue("@Remark", dto.Remark ?? (object)DBNull.Value);
    //                 cmd.Parameters.AddWithValue("@Job", dto.JobNumber ?? (object)DBNull.Value);

    //                 await connection.OpenAsync();
    //                 int rowsAffected = await cmd.ExecuteNonQueryAsync();

    //                 if (rowsAffected == 0)
    //                     return NotFound(new { message = "No running QC job found to update remark." });
    //             }
    //         }

    //         return Ok(new { message = "Remark updated successfully for running job." });
    //     }
    //     catch (Exception ex)
    //     {
    //         return StatusCode(500, new { message = "Error updating remark", error = ex.Message });
    //     }
    // }


    
}


    
       
 









         


    






