using SimpleBlogAPI.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace SimpleBlogAPI.Helpers
{
    public class HelpMethods
    {
        public static async Task SeedRolesAndAdminAsync(IApplicationBuilder app)
        {
            //Create new scope to get services
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

                //Create roles if missing
                string[] roleNames = { "Admin", "User" };
                foreach (var roleName in roleNames)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }
                //Create first admin user (if it does not exist) 
                var adminUser = await userManager.FindByEmailAsync("admin@blog.com");
                if (adminUser == null)
                {
                    var admin = new IdentityUser { UserName = "AdminUser", Email = "admin@blog.com" };
                    var result = await userManager.CreateAsync(admin, "ASecurePassword123!");

                    if (result.Succeeded)
                    {
                        //Give role admin
                        await userManager.AddToRoleAsync(admin, "Admin");
                    }
                }
            }
        }
        public static async Task<string> GenerateJwtToken(IdentityUser user, UserManager<IdentityUser> userManager, IConfiguration config)
        {
            var userRoles = await userManager.GetRolesAsync(user);
            var authClaims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, user.UserName),
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Email),
            };
            //Add roles to claims
            foreach (var userRole in userRoles)
            {
                authClaims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, userRole));
            }
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JwtSettings:Key"]));

            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: config["JwtSettings:Issuer"],
                audience: config["JwtSettings:Audience"],
                expires: DateTime.Now.AddHours(3),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
