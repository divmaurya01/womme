using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using WommeAPI.Data;
using WommeAPI.Services;
using Microsoft.Extensions.FileProviders;
using WommeAPI.Middleware;

var builder = WebApplication.CreateBuilder(args); 

// Read JWT Key from appsettings.json
var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("JWT key not configured in appsettings.");

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

// JWT Authentication Setup
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key
    };
});
 
// Swagger + JWT Authorization
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WommeAPI", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by your token",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement 
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    }); 
});

builder.Services.AddControllers();

// for sync
builder.Services.AddScoped<SyncService>(); 
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddSingleton<DataSyncScheduler>();
builder.Services.AddScoped<SytelineService>();

// Database Configurations
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
     sqlOptions => sqlOptions.CommandTimeout(180)));

builder.Services.AddDbContext<SytelineDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SytelineConnection"),
    sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        )
    )
);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});



var app = builder.Build();  

//  Ensure wwwroot/ProfileImages exists before static file serving
var profileImagesPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "ProfileImages");
if (!Directory.Exists(profileImagesPath))
{
    Directory.CreateDirectory(profileImagesPath);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
// Add Logging Middleware
app.UseRequestLogging(); 


// Serve default wwwroot files
app.UseStaticFiles();

// Serve ProfileImages folder specifically
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(profileImagesPath),
    RequestPath = "/ProfileImages"
});

app.MapControllers();
try
{

    Console.WriteLine("App built successfully");
    app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var url in app.Urls)
        Console.WriteLine("Listening on " + url);
});


    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("Exception during startup: " + ex.Message);
    throw;
}

 