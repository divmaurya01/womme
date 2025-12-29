using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using WommeAPI.Data;
using WommeAPI.Models;
using System.Security.Cryptography; 
 
namespace WommeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
 
        public AuthController(IConfiguration config, AppDbContext context)
        {
            _config = config;
            _context = context;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel login)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 🔹 Extract numeric part from emp_num (e.g. "wme0095" -> 95)
            var numericPart = new string(login.emp_num.Where(char.IsDigit).ToArray());

            if (!int.TryParse(numericPart, out int wommId))
                return BadRequest(new { message = "Invalid Employee Code format." });

            // 🔹 Find user using womm_id
            var user = _context.EmployeeMst.FirstOrDefault(u => u.womm_id == wommId);

            if (user == null)
                return BadRequest(new { message = "User not found." });

            // 🔹 Check active status
            if (user.IsActive != true)
                return BadRequest(new { message = "User is not active." });

            // 🔹 Validate password
            if (user.PasswordHash != login.PasswordHash)
                return BadRequest(new { message = "Invalid password." });

            // 🔹 Get role
            var role = _context.RoleMaster.FirstOrDefault(r => r.RoleID == user.RoleID);
            if (role == null)
                return BadRequest(new { message = "Role not found." });

            // 🔹 JWT setup
            var jwtKey = _config["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is missing in configuration.");

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.name!),
                    new Claim(ClaimTypes.Role, role.RoleName),
                    new Claim("EmployeeCode", user.emp_num!),   // REAL emp_num
                    new Claim("WommId", user.womm_id.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var jwtToken = tokenHandler.CreateToken(tokenDescriptor);
            var jwtTokenString = tokenHandler.WriteToken(jwtToken);

            // 🔹 Generate random token
            var randomToken = GenerateRandomToken();

            // 🔹 Save token
            var userToken = new UserToken
            {
                EmployeeCode = user.emp_num!,
                TokenNumber = randomToken,
                CreatedAt = DateTime.UtcNow,
                ValidTill = DateTime.UtcNow.AddMonths(1)
            };

            _context.UserToken.Add(userToken);

            if (_context.SaveChanges() == 0)
            {
                return StatusCode(500, new { message = "Token generation failed." });
            }

            // 🔹 Response
            return Ok(new
            {
                jwtToken = jwtTokenString,
                randomToken = randomToken,
                userDetails = new
                {
                    UserName = user.name,
                    EmployeeCode = user.emp_num,   // actual emp_num
                    WommId = user.womm_id,
                    user.RoleID,
                    role.RoleName,
                    user.IsActive,
                    user.dept
                }
            });
        }

 
        // Random token generator
        private string GenerateRandomToken(int length = 32)
        {
            var bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
 
        public class LoginModel
        {
            [Required(ErrorMessage = "Employee Code is required.")]
            public string? emp_num { get; set; }
 
            [Required(ErrorMessage = "Password is required.")]
            public string? PasswordHash { get; set; }
        }
    }
}
 

