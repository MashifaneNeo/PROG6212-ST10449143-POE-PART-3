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

// Add Identity
builder.Services.AddDefaultIdentity<User>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;

    try
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

        string[] roleNames = { "HR", "Lecturer", "Coordinator" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

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
        }

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
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error seeding database: {ex.Message}");
    }
}

app.Run();