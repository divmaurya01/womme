using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WommeAPI.Data;
using WommeAPI.Models;

namespace WommeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FetchController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FetchController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("validate-token")]
        public IActionResult ValidateToken([FromBody] TokenValidationModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tokenEntry = _context.UserToken
                .FirstOrDefault(t => t.EmployeeCode == model.EmployeeCode && t.TokenNumber == model.TokenNumber);

            if (tokenEntry == null)
                return Unauthorized(new { message = "Invalid token or user." });

            if (tokenEntry.ValidTill < DateTime.UtcNow)
                return Unauthorized(new { message = "token expired" });

            return Ok(new { message = "token valid" });
        }
    }

    public class TokenValidationModel
    {
        public string? EmployeeCode { get; set; }
        public string? TokenNumber { get; set; }
    }
}
