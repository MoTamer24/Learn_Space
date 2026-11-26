using System.Text;
using System.Text.Json.Serialization;
using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Common.Storage;
using LearnSpace.Api.Data;
using LearnSpace.Api.Data.Seed;
using LearnSpace.Api.Modules.Assessments;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Content;
using LearnSpace.Api.Modules.Enrollments;
using LearnSpace.Api.Modules.Identity;
using LearnSpace.Api.Modules.Groups;
using LearnSpace.Api.Modules.Attendance;
using LearnSpace.Api.Modules.Payments;
using LearnSpace.Api.Modules.Notifications;
using LearnSpace.Api.Modules.Subscriptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------------------------------------------------------------------
// Request size: big enough for the largest upload (the 200 MB video) plus form overhead.
// ---------------------------------------------------------------------
var largestUploadMb = config.GetSection("Storage:Uploads").GetChildren()
    .Select(c => c.GetValue<int?>("MaxMb") ?? 0)
    .DefaultIfEmpty(20)
    .Max();
var maxRequestBytes = (largestUploadMb + 10) * 1024L * 1024L;

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxRequestBytes);

// ---------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(config.GetConnectionString("Default")));

// ---------------------------------------------------------------------
// Identity + JWT
// ---------------------------------------------------------------------
builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

var jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   // keep the short claim names "sub" and "role"
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

// ---------------------------------------------------------------------
// MVC, errors, Swagger, CORS
// ---------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LearnSpace API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken returned by POST /api/auth/login (no 'Bearer ' prefix)."
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

var allowedOrigin = config["Cors:AllowedOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Range", "Accept-Ranges", "Content-Length")));

// ---------------------------------------------------------------------
// Module services (concrete classes; interfaces only where another module needs them)
// ---------------------------------------------------------------------
builder.Services.AddSingleton<IStorageService, LocalDiskStorage>();
builder.Services.AddSingleton<UploadValidator>();

// Identity
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IUserLookup, UserLookup>();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<IStudentDebtService>(sp => sp.GetRequiredService<IdentityService>());
builder.Services.AddScoped<IStaffDirectory>(sp => sp.GetRequiredService<IdentityService>());

// Catalog
builder.Services.AddScoped<CourseService>();
builder.Services.AddScoped<ICourseService>(sp => sp.GetRequiredService<CourseService>());
builder.Services.AddScoped<TeacherService>();
builder.Services.AddScoped<ITeacherService>(sp => sp.GetRequiredService<TeacherService>());
builder.Services.AddScoped<SubjectService>();
builder.Services.AddScoped<ICatalogLookup>(sp => sp.GetRequiredService<SubjectService>());

// Groups, payments, attendance, and notifications coordinate through module contracts.
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<IGroupBillingService>(sp => sp.GetRequiredService<GroupService>());
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<IPaymentService>(sp => sp.GetRequiredService<PaymentService>());
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
builder.Services.AddScoped<TutoringAssessmentService>();

// Enrollment
builder.Services.AddScoped<EnrollmentService>();
builder.Services.AddScoped<IEnrollmentService>(sp => sp.GetRequiredService<EnrollmentService>());
builder.Services.AddScoped<CourseResultsService>();

// Content
builder.Services.AddScoped<MaterialService>();

// Assessments
builder.Services.AddScoped<QuizService>();
builder.Services.AddScoped<CourseTaskService>();
builder.Services.AddScoped<IAssessmentSummaryService, AssessmentSummaryService>();

builder.Services.AddScoped<DbSeeder>();

var app = builder.Build();

// ---------------------------------------------------------------------
// Development startup: migrate, create roles, seed
// ---------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDbContext>();

    // If you have generated migrations (see README) they are applied.
    // If not, the schema is created straight from the model so the project runs out of the box.
    if (db.Database.GetMigrations().Any())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var role in AppRoles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
    }

    await services.GetRequiredService<DbSeeder>().SeedAsync();
    await services.GetRequiredService<DbSeeder>().SeedTutoringAsync();
}

// ---------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------
app.UseCors();                                   // before the error middleware so error responses keep their CORS headers
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();                        // ProblemDetails bodies for bare 401 / 403 / 404

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
