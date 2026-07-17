using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceDB.Models;

public class Product
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StoreId { get; set; }

    [ForeignKey("StoreId")]
    public virtual Store Store { get; set; } = null!;

    [Required]
    public Guid CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category Category { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? OriginalPrice { get; set; }

    public int Stock { get; set; } = 0;

    public decimal Rating { get; set; } = 0;
    public int ReviewCount { get; set; } = 0;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "pending"; // pending, available, rejected, archived

    public bool IsNew { get; set; } = true;
    public bool IsFeatured { get; set; } = false;

    public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public virtual ICollection<ProductSize> Sizes { get; set; } = new List<ProductSize>();
    public virtual ICollection<ProductColor> Colors { get; set; } = new List<ProductColor>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
