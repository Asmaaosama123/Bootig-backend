using System.ComponentModel.DataAnnotations;

namespace EcommerceDB.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? WhatsApp { get; set; }

    // اسم المستخدم لتسجيل الدخول
    [MaxLength(100)]
    public string? Username { get; set; }

    // كلمة المرور المشفرة (BCrypt)
    public string? PasswordHash { get; set; }

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "customer"; // customer, vendor, admin

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "نشط"; // نشط, قيد المراجعة, موقوف

    public string? Otp { get; set; }
    public DateTime? OtpExpires { get; set; }

    public virtual Store? Store { get; set; }
    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
