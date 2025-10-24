using SimpleBlogAPI.Data;
using SimpleBlogAPI.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SimpleBlogAPI.Helpers;

namespace SimpleBlogAPI.Endpoints
{
    public static class AuthEndpoints
    {
        //IEndpointRouteBuilder to map and auth endpoints
        public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            //register endpoint
            app.MapPost("/register", RegisterUser)
                .WithOpenApi();
            //login endpoint
            app.MapPost("/login", LoginUser)
                .WithOpenApi();
        }

        //handle endpoint
        public static async Task<IResult> RegisterUser(
            RegisterUserRequest request,
            UserManager<IdentityUser> userManager)
        {
            //check if username already exists
            if (await userManager.FindByEmailAsync(request.Email) != null)
            {
                return Results.BadRequest("User already exists.");
            }


            //login to register new user
            var user = new IdentityUser
            {
                UserName = request.Email,
                Email = request.Email
            };
            var result = await userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                //Assign role 
                var roleResult = await userManager.AddToRoleAsync(user, "User");

                if (roleResult.Succeeded)
                {
                    return Results.Ok("User registered successfully.");
                }
                else
                {
                    return Results.Problem("User created but failed to assign role to user.");
                }
            }
            //error message
            return Results.BadRequest(result.Errors.Select(e => e.Description));
        }
        public static async Task<IResult> LoginUser(
            LoginRequest request,
            UserManager<IdentityUser> userManager,
            IConfiguration config)
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            //verify if exists and correct
            if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Results.Unauthorized();
            }

            //generate jwt token
            var tokenString = await HelpMethods.GenerateJwtToken(user, userManager, config);

            //return token to client
            return Results.Ok(new { Token = tokenString });
        }


    }
}