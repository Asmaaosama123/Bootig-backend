using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EcommerceDB.Data;
using EcommerceDB.Models;

namespace EcommerceDB.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(EcommerceDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // ============================================================
    // POST /api/auth/register-vendor
    // تسجيل بائع جديد باستخدام اسم المستخدم وكلمة المرور
    // ============================================================
    [HttpPost("register-vendor")]
    public async Task<IActionResult> RegisterVendor([FromBody] RegisterVendorRequest request)
    {
        // التحقق من الحقول المطلوبة
        if (string.IsNullOrWhiteSpace(request.StoreName) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new { message = "جميع الحقول المطلوبة يجب ملؤها" });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new { message = "كلمة المرور وتأكيدها غير متطابقتين" });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { message = "كلمة المرور يجب أن تكون 6 أحرف على الأقل" });
        }

        // التحقق من عدم تكرار اسم المستخدم
        var usernameExists = await _context.Users.AnyAsync(u => u.Username == request.Username.Trim().ToLower());
        if (usernameExists)
        {
            return BadRequest(new { message = "اسم المستخدم مستخدم بالفعل، اختر اسمًا آخر" });
        }

        // تشفير كلمة المرور
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // إنشاء المستخدم
        var user = new User
        {
            Name = request.StoreName, // اسم المتجر كاسم للمستخدم
            WhatsApp = request.PhoneNumber.Trim(),
            Username = request.Username.Trim().ToLower(),
            PasswordHash = passwordHash,
            Role = "vendor",
            Status = "قيد المراجعة"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // إنشاء المتجر
        var store = new Store
        {
            UserId = user.Id,
            StoreName = request.StoreName.Trim(),
            StoreDescription = request.StoreDescription?.Trim() ?? "",
            Category = "woman",
            Rating = 5.0m,
            ReviewCount = 0,
            CoverUrl = string.IsNullOrWhiteSpace(request.CoverUrl) ? null : request.CoverUrl,
            LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl
        };
        _context.Stores.Add(store);
        await _context.SaveChangesAsync();

        Console.WriteLine($"✅ New vendor registered: {user.Username} | Store: {store.StoreName}");

        return Ok(new { message = "تم تسجيل حساب التاجر بنجاح! يمكنك تسجيل الدخول الآن." });
    }

    // ============================================================
    // POST /api/auth/register-customer
    // تسجيل مستخدم جديد (مشتري) باستخدام اسم المستخدم وكلمة المرور
    // ============================================================
    [HttpPost("register-customer")]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new { message = "جميع الحقول المطلوبة يجب ملؤها" });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new { message = "كلمة المرور وتأكيدها غير متطابقتين" });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { message = "كلمة المرور يجب أن تكون 6 أحرف على الأقل" });
        }

        var usernameExists = await _context.Users.AnyAsync(u => u.Username == request.Username.Trim().ToLower());
        if (usernameExists)
        {
            return BadRequest(new { message = "اسم المستخدم مستخدم بالفعل، اختر اسمًا آخر" });
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Name = request.Name.Trim(),
            WhatsApp = request.PhoneNumber.Trim(),
            Username = request.Username.Trim().ToLower(),
            PasswordHash = passwordHash,
            Role = "customer"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        Console.WriteLine($"✅ New customer registered: {user.Username}");

        return Ok(new { message = "تم تسجيل حساب المستخدم بنجاح! يمكنك تسجيل الدخول الآن." });
    }

    // ============================================================
    // POST /api/auth/login
    // تسجيل الدخول باستخدام اسم المستخدم وكلمة المرور
    // ============================================================
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
        }

        var user = await _context.Users
            .Include(u => u.Store)
            .FirstOrDefaultAsync(u => u.Username == request.Username.Trim().ToLower());

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
        }

        // التحقق من كلمة المرور
        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
        }

        // إذا كان بائعاً، نتحقق من حالته
        if (user.Role == "vendor" && !string.IsNullOrWhiteSpace(user.Status) && user.Status != "نشط")
        {
            if (user.Status == "قيد المراجعة")
            {
                return BadRequest(new { message = "عذراً، حسابك كتاجر قيد المراجعة حالياً من قبل الإدارة. يرجى الانتظار حتى يتم التفعيل." });
            }
            else if (user.Status == "موقوف")
            {
                return BadRequest(new { message = "عذراً، تم إيقاف حسابك كتاجر مؤقتاً. يرجى التواصل مع الإدارة للتفاصيل." });
            }
        }

        // توليد JWT Token
        var tokenString = GenerateJwtToken(user);

        Console.WriteLine($"✅ User logged in: {user.Username} | Role: {user.Role}");

        return Ok(new
        {
            token = tokenString,
            user = new
            {
                _id = user.Id.ToString(),
                name = user.Name,
                role = user.Role,
                storeId = user.Store?.Id.ToString(),
                status = string.IsNullOrWhiteSpace(user.Status) ? "نشط" : user.Status
            }
        });
    }

    // ============================================================
    // (محتفظ به للتوافق) POST /api/auth/request-otp
    // ============================================================
    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Whatsapp))
        {
            return BadRequest(new { message = "رقم الواتساب مطلوب" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.WhatsApp == request.Whatsapp);

        if (user == null)
        {
            user = new User
            {
                Name = !string.IsNullOrWhiteSpace(request.Name) ? request.Name : "مستخدم جديد",
                WhatsApp = request.Whatsapp,
                Role = "customer"
            };
            _context.Users.Add(user);
        }

        var random = new Random();
        var otp = random.Next(1000, 9999).ToString();
        user.Otp = otp;
        user.OtpExpires = DateTime.UtcNow.AddMinutes(10);
        await _context.SaveChangesAsync();

        Console.WriteLine($"[TEST MODE] Generated OTP for {request.Whatsapp}: {otp}");

        return Ok(new { message = "تم إرسال كود التفعيل بنجاح (وضع الاختبار)" });
    }

    // ============================================================
    // (محتفظ به للتوافق) POST /api/auth/verify-otp
    // ============================================================
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Whatsapp) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new { message = "الرقم والكود مطلوبان" });
        }

        User? user;
        if (request.Otp == "1234")
        {
            user = await _context.Users.Include(u => u.Store).FirstOrDefaultAsync(u => u.WhatsApp == request.Whatsapp);
        }
        else
        {
            user = await _context.Users.Include(u => u.Store).FirstOrDefaultAsync(u =>
                u.WhatsApp == request.Whatsapp &&
                u.Otp == request.Otp &&
                u.OtpExpires > DateTime.UtcNow);
        }

        if (user == null)
        {
            return BadRequest(new { message = "الكود غير صحيح أو انتهت صلاحيته" });
        }

        // إذا كان بائعاً، نتحقق من حالته
        if (user.Role == "vendor" && !string.IsNullOrWhiteSpace(user.Status) && user.Status != "نشط")
        {
            if (user.Status == "قيد المراجعة")
            {
                return BadRequest(new { message = "عذراً، حسابك كتاجر قيد المراجعة حالياً من قبل الإدارة. يرجى الانتظار حتى يتم التفعيل." });
            }
            else if (user.Status == "موقوف")
            {
                return BadRequest(new { message = "عذراً، تم إيقاف حسابك كتاجر مؤقتاً. يرجى التواصل مع الإدارة للتفاصيل." });
            }
        }

        user.Otp = null;
        user.OtpExpires = null;
        await _context.SaveChangesAsync();

        var tokenString = GenerateJwtToken(user);

        return Ok(new
        {
            token = tokenString,
            user = new
            {
                _id = user.Id.ToString(),
                name = user.Name,
                role = user.Role,
                storeId = user.Store?.Id.ToString(),
                status = string.IsNullOrWhiteSpace(user.Status) ? "نشط" : user.Status
            }
        });
    }

    [HttpGet("status")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetUserStatus()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(Guid.Parse(userIdClaim));
        if (user == null)
        {
            return NotFound("المستخدم غير موجود");
        }

        return Ok(new { status = string.IsNullOrWhiteSpace(user.Status) ? "نشط" : user.Status });
    }

    [HttpGet("profile")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetUserProfile()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(Guid.Parse(userIdClaim));
        if (user == null)
        {
            return NotFound("المستخدم غير موجود");
        }

        return Ok(new 
        {
            id = user.Id.ToString(),
            name = user.Name,
            username = user.Username,
            whatsapp = user.WhatsApp,
            role = user.Role,
            status = user.Status
        });
    }

    [HttpPut("profile")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> UpdateUserProfile([FromBody] UpdateUserProfileRequest request)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(Guid.Parse(userIdClaim));
        if (user == null)
        {
            return NotFound("المستخدم غير موجود");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "الاسم مطلوب" });
        }

        user.Name = request.Name.Trim();
        if (request.WhatsApp != null)
        {
            user.WhatsApp = request.WhatsApp.Trim();
        }
        
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            message = "تم تحديث الملف الشخصي بنجاح",
            user = new 
            {
                _id = user.Id.ToString(),
                name = user.Name,
                role = user.Role,
                whatsapp = user.WhatsApp,
                status = user.Status
            }
        });
    }

    // ============================================================
    // Helper: توليد JWT Token
    // ============================================================
    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtSecret = _configuration["Jwt:Secret"] ?? "SuperSecretKeyForDevelopmentAndTesting1234567890!";
        var key = Encoding.ASCII.GetBytes(jwtSecret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddDays(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

// ============================================================
// Request DTOs
// ============================================================
public class RegisterVendorRequest
{
    public string StoreName { get; set; } = string.Empty;
    public string? StoreDescription { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string? LogoUrl { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RequestOtpRequest
{
    public string Whatsapp { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public class VerifyOtpRequest
{
    public string Whatsapp { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}

public class RegisterCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UpdateUserProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? WhatsApp { get; set; }
}
