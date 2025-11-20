using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PROG6212_ST10449143_POE_PART_1.Models;
using PROG6212_ST10449143_POE_PART_1.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity with proper configuration
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";
});

// Register services
builder.Services.AddScoped<IClaimService, DatabaseClaimService>();
builder.Services.AddScoped<IHRService, HRService>();
builder.Services.AddScoped<DocumentValidator>();
builder.Services.AddScoped<IClaimAutomationService, ClaimAutomationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession(); // Add session middleware

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed roles and admin user
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;

    try
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

        // Create roles - UPDATED WITH NEW ROLES
        string[] roleNames = { "HR", "Lecturer", "Coordinator", "AcademicManager" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
                Console.WriteLine($"Created role: {roleName}");
            }
        }

        // Create default HR user
        var hrUser = new User
        {
            FirstName = "HR",
            LastName = "Admin",
            UserName = "hr@university.ac.za",
            Email = "hr@university.ac.za",
            HourlyRate = 0,
            EmployeeId = "HR001",
            Department = "Human Resources"
        };

        var hrUserExists = await userManager.FindByEmailAsync(hrUser.Email);
        if (hrUserExists == null)
        {
            var createHR = await userManager.CreateAsync(hrUser, "TempPassword123!");
            if (createHR.Succeeded)
            {
                await userManager.AddToRoleAsync(hrUser, "HR");
                Console.WriteLine("HR user created successfully!");
            }
            else
            {
                Console.WriteLine($"Failed to create HR user: {string.Join(", ", createHR.Errors.Select(e => e.Description))}");
            }
        }

        // Create a sample lecturer
        var lecturerUser = new User
        {
            FirstName = "John",
            LastName = "Smith",
            UserName = "john.smith@university.ac.za",
            Email = "john.smith@university.ac.za",
            HourlyRate = 250,
            EmployeeId = "LEC001",
            Department = "Computer Science"
        };

        var lecturerExists = await userManager.FindByEmailAsync(lecturerUser.Email);
        if (lecturerExists == null)
        {
            var createLecturer = await userManager.CreateAsync(lecturerUser, "Lecturer123!");
            if (createLecturer.Succeeded)
            {
                await userManager.AddToRoleAsync(lecturerUser, "Lecturer");
                Console.WriteLine("Sample lecturer created successfully!");
            }
            else
            {
                Console.WriteLine($"Failed to create lecturer: {string.Join(", ", createLecturer.Errors.Select(e => e.Description))}");
            }
        }

        // Create sample Programme Coordinator
        var coordinatorUser = new User
        {
            FirstName = "Sarah",
            LastName = "Johnson",
            UserName = "sarah.johnson@university.ac.za",
            Email = "sarah.johnson@university.ac.za",
            HourlyRate = 0,
            EmployeeId = "COORD001",
            Department = "Computer Science"
        };

        var coordinatorExists = await userManager.FindByEmailAsync(coordinatorUser.Email);
        if (coordinatorExists == null)
        {
            var createCoordinator = await userManager.CreateAsync(coordinatorUser, "Coordinator123!");
            if (createCoordinator.Succeeded)
            {
                await userManager.AddToRoleAsync(coordinatorUser, "Coordinator");
                Console.WriteLine("Sample coordinator created successfully!");
            }
        }

        // Create sample Academic Manager
        var managerUser = new User
        {
            FirstName = "David",
            LastName = "Wilson",
            UserName = "david.wilson@university.ac.za",
            Email = "david.wilson@university.ac.za",
            HourlyRate = 0,
            EmployeeId = "MGR001",
            Department = "Academic Affairs"
        };

        var managerExists = await userManager.FindByEmailAsync(managerUser.Email);
        if (managerExists == null)
        {
            var createManager = await userManager.CreateAsync(managerUser, "Manager123!");
            if (createManager.Succeeded)
            {
                await userManager.AddToRoleAsync(managerUser, "AcademicManager");
                Console.WriteLine("Sample academic manager created successfully!");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error seeding database: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
}

app.Run();