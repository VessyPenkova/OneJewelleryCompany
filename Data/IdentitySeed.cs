using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OneJevelsCompany.Web.Data
{
    public static class IdentitySeed
    {
        public static async Task ApplyAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

            const string AdminRole = "Admin";
            if (!await roleMgr.RoleExistsAsync(AdminRole))
                await roleMgr.CreateAsync(new IdentityRole(AdminRole));

            // Never create a known default-password account outside Development.
            if (!env.IsDevelopment()) return;

            var email = "admin@onejevels.test";
            var pwd = "Admin!2345"; // strong dev password
            var admin = await userMgr.FindByEmailAsync(email);
            if (admin == null)
            {
                admin = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                var create = await userMgr.CreateAsync(admin, pwd);
                if (create.Succeeded)
                {
                    await userMgr.AddToRoleAsync(admin, AdminRole);
                }
                else
                {
                    throw new Exception("Failed to create seed admin user: " +
                        string.Join("; ", create.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                if (!await userMgr.IsInRoleAsync(admin, AdminRole))
                    await userMgr.AddToRoleAsync(admin, AdminRole);
            }
        }
    }
}
