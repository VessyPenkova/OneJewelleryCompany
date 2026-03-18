using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using OneJevelsCompany.Web.Data;
using OneJevelsCompany.Web.Services.Cart;
using OneJevelsCompany.Web.Services.Inventory;
using OneJevelsCompany.Web.Services.Orders;
using OneJevelsCompany.Web.Services.Payment;
using OneJevelsCompany.Web.Services.Product;
using OneJevelsCompany.Web.Services.Dashboard;
using OneJevelsCompany.Web.Services.Common;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor Pages (Identity UI uses Razor Pages)
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// EF Core — SQL Server (supports ActiveConnection switch)
var activeConnKey = builder.Configuration["ActiveConnection"] ?? "DefaultConnection";
var connectionString = builder.Configuration.GetConnectionString(activeConnKey)
    ?? throw new InvalidOperationException($"Connection string '{activeConnKey}' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Identity (Default UI + Roles)
builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

// ✅ IMPORTANT: Force redirects to YOUR MVC login/register (NOT /Identity/Account/Login)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";

    options.SlidingExpiration = true;
});

// Session (cart)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".OneJevelsCompany.Session";
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Domain services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, StripePaymentService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IImageStorage, DiskImageStorage>();
builder.Services.AddScoped<ICategoryLookup, CategoryLookup>();

var app = builder.Build();

// Apply migrations + create roles + create permanent admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

    // Create roles if missing
    string[] roles = { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Create permanent admin user if missing
    string adminEmail = "admin@onejewellery.com";
    string adminPassword = "Admin123";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Keep this if you use other Identity pages (you can keep it safely)
app.MapRazorPages();

app.Run();