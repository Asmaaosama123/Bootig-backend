using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceDB.Models;

public class Store
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Required]
    [MaxLength(150)]
    public string StoreName { get; set; } = string.Empty;

    public string? StoreDescription { get; set; }

    public decimal Rating { get; set; } = 0;
    public int ReviewCount { get; set; } = 0;

    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }

    public bool IsFeatured { get; set; } = false;

    public string? Category { get; set; } // Main business area e.g. Clothing

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
