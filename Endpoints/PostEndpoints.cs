using SimpleBlogAPI.Data;
using SimpleBlogAPI.DTOs;
using SimpleBlogAPI.Model;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Runtime.CompilerServices;

namespace SimpleBlogAPI.Endpoints
{
    public static class PostEndpoints
    {
        //extension method for IEndpointRouteBuilder to map all blog post endpoints
        public static void MapPostEndpoints (this IEndpointRouteBuilder app)
        {
            //create blog post endopoint
            app.MapPost("/posts", CreatePost)
                .RequireAuthorization("User")
                .WithOpenApi();

            //delete posts endpoint
            app.MapDelete("/posts/{id:int}", DeletePost)
                .RequireAuthorization()
                .WithOpenApi();
        }

        //endpoint handler
        public static async Task<IResult> CreatePost(PostRequest request, BlogDbContext context, HttpContext httpContext)
        {
            var authorId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            //check if value found
            if (string.IsNullOrEmpty(authorId))
            {
                return Results.Unauthorized();
            }

            //map DTO to model
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
        }

        public static async Task<IResult> DeletePost(int id, BlogDbContext context, HttpContext httpContext)
        {
            var post = await context.BlogPosts.FindAsync(id);
            if (post == null)
            {
                return Results.NotFound("Post not found.");
            }

            //get logged in user info
            var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            //checked if admin or author
            var isAdmin = httpContext.User.IsInRole("Admin");

            //only author or admin can delete
            if (post.AuthorId != currentUserId && !isAdmin)
            {
                return Results.Forbid();
            }

            //delete post
            context.BlogPosts.Remove(post);
            await context.SaveChangesAsync();
            return Results.Ok("Post deleted successfully.");
        }
    }
}
