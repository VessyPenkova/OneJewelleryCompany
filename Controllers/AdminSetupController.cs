using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace OneJevelsCompany.Web.Controllers
{
    [Authorize] // must be logged in
    public class AdminSetupController : Controller
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly RoleManager<IdentityRole> _roles;
        private readonly IWebHostEnvironment _env;

        public AdminSetupController(UserManager<IdentityUser> users, RoleManager<IdentityRole> roles, IWebHostEnvironment env)
        {
            _users = users;
            _roles = roles;
            _env = env;
        }

        // GET /AdminSetup/Elevate
        [HttpGet]
        public async Task<IActionResult> Elevate()
        {
            // Safety: only allow in Development
            if (!_env.IsDevelopment()) return Forbid();

            const string adminRole = "Admin";
            if (!await _roles.RoleExistsAsync(adminRole))
                await _roles.CreateAsync(new IdentityRole(adminRole));

            var me = await _users.GetUserAsync(User);
            if (me == null) return Unauthorized();

            if (!await _users.IsInRoleAsync(me, adminRole))
                await _users.AddToRoleAsync(me, adminRole);

            return Content($"User '{me.Email}' is now in role '{adminRole}'. You can open /Admin.");
        }
    }
}
