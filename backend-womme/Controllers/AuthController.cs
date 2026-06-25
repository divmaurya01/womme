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
           if (string.IsNullOrWhiteSpace(login.emp_num))
            {
                return BadRequest("Employee number is required");
            }

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

        [HttpPost("forgot-login")]
        public async Task<IActionResult> ForgotLogin([FromBody] ForgotLoginDto dto)
        {
            if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Name))
                return BadRequest("Name and Email are required");

            var employee = _context.EmployeeMst
                .FirstOrDefault(x => x.email_addr == dto.Email);

            if (employee == null)
                return NotFound("Email not found in system");

            // ── Save notification ─────────────────────────────────────────────
            var notification = new Notification
            {
                Name        = employee.name,
                Email       = employee.email_addr,
                Subject     = dto.Flag == "FORGOT_PASSWORD"
                                ? "Forgot Password Request"
                                : "Forgot User ID Request",
                Details     = $"Request raised for {dto.Flag}",
                Status      = "PENDING",
                CreatedDate = DateTime.UtcNow
            };

            _context.Notification.Add(notification);
            _context.SaveChanges();

            var emailApiUrl = _config["EmailSettings:ApiUrl"];
            if (string.IsNullOrEmpty(emailApiUrl))
                return StatusCode(500, "Email API URL not configured");

            // ── Email bodies ──────────────────────────────────────────────────

            string adminEmailBody = $@"
            <p>Dear Admin,</p>
            <p>A login assistance request has been submitted.</p>
            <ul>
                <li><strong>Name:</strong> {employee.name}</li>
                <li><strong>Email:</strong> {employee.email_addr}</li>
                <li><strong>Request:</strong>
                    {(dto.Flag == "FORGOT_PASSWORD" ? "Forgot Password" : "Forgot User ID")}
                </li>
            </ul>
            <p>Please review and take necessary action.</p>
            <p>Regards,<br/><strong>WOMME System</strong></p>";

            string userEmailBody = $@"
            <p>Dear {employee.name},</p>
            <p>
                We have received your request regarding login assistance.
                Our support team will review it and get back to you shortly.
            </p>
            <p>Thank you for your patience.</p>
            <p>Regards,<br/><strong>WOMME Support Team</strong></p>";

            using var client = new HttpClient();

            // ── Helper: build multipart form ──────────────────────────────────
            MultipartFormDataContent BuildForm(string to, string subject, string body)
            {
                var form = new MultipartFormDataContent();
                form.Add(new StringContent(to),      "to");
                form.Add(new StringContent(subject), "subject");
                form.Add(new StringContent(body),    "body");
                form.Add(new StringContent("true"),  "isHtml");
                return form;
            }

            // ── Send Admin Email ──────────────────────────────────────────────
            var adminForm     = BuildForm("manhour@womme.ae", "Action Required: Login Assistance Request", adminEmailBody);
            var adminResponse = await client.PostAsync(emailApiUrl, adminForm);
            var adminError    = !adminResponse.IsSuccessStatusCode
                ? await adminResponse.Content.ReadAsStringAsync()
                : null;

            // ── Send User Email ───────────────────────────────────────────────
            var userEmailTo   = !string.IsNullOrEmpty(employee.email_addr)
                ? employee.email_addr
                : "manhour@womme.ae";

            var userForm      = BuildForm(userEmailTo, "We Have Received Your Request", userEmailBody);
            var userResponse  = await client.PostAsync(emailApiUrl, userForm);
            var userError     = !userResponse.IsSuccessStatusCode
                ? await userResponse.Content.ReadAsStringAsync()
                : null;

            // ── Update notification status ────────────────────────────────────
            notification.Status =
                adminResponse.IsSuccessStatusCode && userResponse.IsSuccessStatusCode
                    ? "SENT"
                    : "PARTIAL";

            notification.UpdatedDate = DateTime.UtcNow;
            _context.SaveChanges();

            // ── Return result ─────────────────────────────────────────────────
            if (!adminResponse.IsSuccessStatusCode || !userResponse.IsSuccessStatusCode)
            {
                return StatusCode(500, new
                {
                    message     = "Email sending failed",
                    adminStatus = adminResponse.StatusCode.ToString(),
                    adminError  = adminError,
                    userStatus  = userResponse.StatusCode.ToString(),
                    userError   = userError
                });
            }

            return Ok(new
            {
                message = "Request submitted successfully. A confirmation email has been sent."
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
 

