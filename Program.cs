using SimpleBlogAPI.Data;
using SimpleBlogAPI.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using SimpleBlogAPI.Model;

namespace SimpleBlogAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            //---- Services Configuration ----//
            var builder = WebApplication.CreateBuilder(args);

            //Fetch conection string and register DbContext with SQLite provider
            var connectionString = builder.Configuration.GetConnectionString("BlogDbContext")
                ?? throw new InvalidOperationException("Connection string 'DbContext' not found.");
            builder.Services.AddDbContext<BlogDbContext>(options => options.UseSqlite(connectionString));

            //Config Identity
            builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                //Password settings
                options.SignIn.RequireConfirmedAccount = false;
            })
                .AddEntityFrameworkStores<BlogDbContext>()
                .AddDefaultTokenProviders();

            // Add services to the container.
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                //Validate Tokens
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    ValidAudience = builder.Configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"]))
                };
            });


            builder.Services.AddAuthorization(options =>
            {
                //Add policy named "User" - needs the user to have claimed Role "User"
                options.AddPolicy("User", policy => policy.RequireRole("User"));

                //Add policy named "Admin"
                options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // ----- HTTP request pipeline. ----//
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();

                //Seed method here
                await SeedRolesAndAdminAsync(app);
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            // ---- Endpoints ---- //
            //Register endpoint
            app.MapPost("/register", async (RegisterUserRequest request,
                UserManager<IdentityUser> UserManager) =>
            {
                //Check if user already exists
                if (await UserManager.FindByEmailAsync(request.Email) != null)
                {
                    return Results.BadRequest(new { Message = "User already exists." });
                }

                //Logic to register user
                var user = new IdentityUser { UserName = request.Email, Email = request.Email };
                var result = await UserManager.CreateAsync(user, request.Password);

                if (result.Succeeded)
                {
                    //Assign role here
                    var roleResult = await UserManager.AddToRoleAsync(user, "User");

                    if (roleResult.Succeeded)
                    {
                        return Results.Ok(new { Message = "User registered and assigned role 'User'" });
                    }
                    //If assignment fails, handle
                    return Results.Problem("User created but role assignment failed");
                }

                //Error message
                return Results.BadRequest(result.Errors.Select(e => e.Description));
            }).WithOpenApi();

            // New login endpoint
            app.MapPost("/login", async (LoginRequest request,
                UserManager<IdentityUser> UserManager, IConfiguration config) =>
            {
                var user = await UserManager.FindByEmailAsync(request.Email);

                //Verify if user exists and password is correct
                if (user == null || !await UserManager.CheckPasswordAsync(user, request.Password))
                {
                    return Results.Unauthorized(); //401 Not authorized
                }

                //Generate JWT Token
                var tokenString = await GenerateJwtToken(user, UserManager, config);

                //Return token to client
                return Results.Ok(new { Token = tokenString });

            }).WithOpenApi();

            //Create blog post endpooint
            app.MapPost("/posts", async (PostRequest request, BlogDbContext context, HttpContext httpContext) =>
            {
                var authorId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                //Check if value found
                if (string.IsNullOrEmpty(authorId))
                {
                    return Results.Unauthorized();
                }

                //Map DTO to model
                var post = new BlogPost
                {
                    Title = request.Title,
                    Content = request.Content,
                    CreatedAt = DateTime.UtcNow,
                    AuthorId = authorId
                };

                //save
                context.BlogPosts.Add(post);
                await context.SaveChangesAsync();

                //return new post
                return Results.Created($"/posts/{post.Id}", post);
            })
                .RequireAuthorization("User")
                .WithOpenApi();

            //Create delete posts endpoint
            app.MapDelete("/posts/{id:int}", async (int id, BlogDbContext context, HttpContext httpContext) =>
            {
                var post = await context.BlogPosts.FindAsync(id);
                if (post == null)
                {
                    return Results.NotFound();
                }

                //Get logged in user info
                var currentUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                //Check if user is admin or author of post
                if (post.AuthorId != currentUserId)
                {
                    return Results.Forbid();
                }

                context.BlogPosts.Remove(post);
                await context.SaveChangesAsync();

                return Results.NoContent();
            })
                .RequireAuthorization()
                .WithOpenApi();

            app.Run();
        }

        // ---- Help methods ---- //
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
        private static async Task<string> GenerateJwtToken(IdentityUser user, UserManager<IdentityUser> userManager, IConfiguration config)
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
            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}