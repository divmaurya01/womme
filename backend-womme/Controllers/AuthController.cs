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
 
            var user = _context.EmployeeMst.FirstOrDefault(u => u.emp_num == login.emp_num);
            if (user == null)
                return BadRequest(new { message = "User not found." });
 
          //  if (!user.IsActive)  
          //      return BadRequest(new { message = "User is not active." });  

           if (user.IsActive != true)  // null or false = inactive
                return BadRequest(new { message = "User is not active." });

 
            if (user.PasswordHash != login.PasswordHash)
                return BadRequest(new { message = "Invalid password." });
 
            var role = _context.RoleMaster.FirstOrDefault(r => r.RoleID == user.RoleID);
            if (role == null)
                return BadRequest(new { message = "Role not found." });
 
            // Generate JWT Token
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
                    new Claim("EmployeeCode", user.emp_num!)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
 
            var jwtToken = tokenHandler.CreateToken(tokenDescriptor);
            var jwtTokenString = tokenHandler.WriteToken(jwtToken);
 
            // Generate Custom Random Token
            var randomToken = GenerateRandomToken();
 
            // Save random token to UserToken table
            var userToken = new UserToken
            {
                EmployeeCode = user.emp_num!,
                TokenNumber = randomToken,
                CreatedAt = DateTime.UtcNow,
                ValidTill = DateTime.UtcNow.AddMonths(1)
            };
 
            _context.UserToken.Add(userToken);
            int saveResult = _context.SaveChanges();
 
            if (saveResult == 0)
            {
                return StatusCode(500, new { message = "Token generation failed. Please try again." });
            }
 
            // Return both tokens and user details
            return Ok(new
            {
                jwtToken = jwtTokenString,
                randomToken = randomToken,
                userDetails = new
                {

                    UserName = user.name,
                    EmployeeCode = user.emp_num,
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
 

