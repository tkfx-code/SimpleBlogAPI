using System.ComponentModel.DataAnnotations;

namespace SimpleBlogAPI.DTOs
{
    public class PostRequest
    {
        [Required]
        public string Title { get; set; }
        [Required]
        public string Content { get; set; }
    }
}
