using Microsoft.AspNetCore.Identity;

namespace SimpleBlogAPI.Model
{
    public class BlogPost
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        //Foreign Key - to store ID
        public string AuthorId { get; set; }
        //Navigation Property - to access full Author details
        public IdentityUser Author { get; set; }
    }
}
