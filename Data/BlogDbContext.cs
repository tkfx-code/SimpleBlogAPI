using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SimpleBlogAPI.Model;

namespace SimpleBlogAPI.Data
{
    public class BlogDbContext : IdentityDbContext<IdentityUser>
    {
        public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options)
        {
        }

        public DbSet<BlogPost> BlogPosts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //Connect the AuthorID explicitly
            builder.Entity<BlogPost>()
                .HasOne(p => p.Author)
                .WithMany() //One post has one author, but one author many posts
                .HasForeignKey(p => p.AuthorId);
        }
    }
}
