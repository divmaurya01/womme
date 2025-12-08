using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using WommeAPI.Data;
using WommeAPI.Models;

namespace WommeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CalendarController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CalendarController(AppDbContext context)
        {
            _context = context;
        }

        //  ADD: POST /api/calendar 
        [HttpPost("AddCalendar")]
        public async Task<IActionResult> AddCalendar([FromBody] Calendar calendar)
        {
            if (await _context.Calendar.AnyAsync(c => c.date == calendar.date))
                return BadRequest("A record with this date already exists.");

            _context.Calendar.Add(calendar);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Calendar entry added successfully." });
        }

        //  EDIT: PUT /api/calendar/{id}  
        [HttpPut("UpdateCalendar/{id}")]
        public async Task<IActionResult> UpdateCalendar(int id, [FromBody] Calendar calendar)
        {
            var existing = await _context.Calendar.FindAsync(id);
            if (existing == null)
                return NotFound("Calendar entry not found.");

            existing.date = calendar.date;
            existing.flag = calendar.flag;
            existing.CalendarDescription = calendar.CalendarDescription;
            existing.Occasion = calendar.Occasion;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Calendar entry updated successfully." });
        }

        //  DELETE: DELETE /api/calendar/{id} 
        [HttpDelete("DeleteCalendar/{id}")]
        public async Task<IActionResult> DeleteCalendar(int id)
        {
            var calendar = await _context.Calendar.FindAsync(id);
            if (calendar == null)
                return NotFound("Calendar entry not found.");

            _context.Calendar.Remove(calendar);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Calendar entry deleted successfully." });
        }

        //GET:  api/get/allcalendars
        [HttpGet("GetAllCalendars")]
        public async Task<ActionResult> GetAllCalendars()
        {
            var calendar = await _context.Calendar.ToListAsync();
            return Ok(calendar);
        }

    }

}

