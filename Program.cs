using SimpleBlogAPI.Data;
using SimpleBlogAPI.Endpoints;
using SimpleBlogAPI.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Identity;

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
                await HelpMethods.SeedRolesAndAdminAsync(app);
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            // ---- Endpoints ---- //
            app.MapAuthEndpoints();
            app.MapPostEndpoints();

            app.Run();
        }
    }
}