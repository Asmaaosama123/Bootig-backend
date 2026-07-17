using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceDB.Data;
using EcommerceDB.Models;
using System.Globalization;

namespace EcommerceDB.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly EcommerceDbContext _context;

    public AdminController(EcommerceDbContext context)
    {
        _context = context;
    }

    // GET: /api/admin/dashboard-stats
    [HttpGet("dashboard-stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var totalSellers = await _context.Users.CountAsync(u => u.Role == "vendor");
        var totalOrders = await _context.Orders.CountAsync();
        var totalSales = await _context.Orders.SumAsync(o => o.TotalPrice);
        var totalProducts = await _context.Products.CountAsync();
        var totalCustomers = await _context.Users.CountAsync(u => u.Role == "customer");
        var totalProfits = totalSales * 0.20m; // 20% platform fee

        // Shipped and Cancelled must be hardcoded to 0 as per user request
        var shippedOrders = 0;
        var cancelledOrders = 0;

        // Last 4 orders, status completed
        var lastOrdersDb = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .OrderByDescending(o => o.CreatedAt)
            .Take(4)
            .ToListAsync();

        var lastOrders = lastOrdersDb.Select(o => new
        {
            id = o.Id.ToString(),
            customer = o.Customer != null ? o.Customer.Name : "عميل غير معروف",
            amount = o.TotalPrice,
            status = "مكتملة", // As requested: "خلوهم كلهم مكتملين"
            date = o.CreatedAt.ToString("dd MMMM yyyy", new CultureInfo("ar-EG"))
        }).ToList();

        return Ok(new
        {
            totalSellers,
            totalOrders,
            totalSales = Math.Round(totalSales, 2),
            totalProducts,
            totalCustomers,
            totalProfits = Math.Round(totalProfits, 2),
            shippedOrders,
            cancelledOrders,
            lastOrders
        });
    }

    // GET: /api/admin/vendors
    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors()
    {
        var dbVendors = await _context.Users
            .AsNoTracking()
            .Include(u => u.Store)
            .Where(u => u.Role == "vendor")
            .ToListAsync();

        var vendorsList = new List<object>();
        int idCounter = 10008;

        foreach (var user in dbVendors)
        {
            var storeName = user.Store?.StoreName ?? user.Name;
            
            // Generate initials
            var initials = "V";
            if (!string.IsNullOrWhiteSpace(storeName))
            {
                var parts = storeName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    initials = parts.Length > 1 
                        ? (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper()
                        : parts[0][0].ToString().ToUpper();
                }
            }

            var username = user.Username ?? "";
            if (username.Contains("@"))
            {
                username = username.Split('@')[0];
            }

            vendorsList.Add(new
            {
                id = idCounter++,
                dbId = user.Id,
                name = user.Name,
                store = storeName,
                username = username,
                phone = user.WhatsApp ?? "+966 50 000 0000",
                registrationDate = user.CreatedAt.ToString("dd MMMM yyyy", new CultureInfo("ar-EG")),
                status = string.IsNullOrWhiteSpace(user.Status) ? "نشط" : user.Status,
                initials = initials,
                avatarBg = "bg-zinc-800 text-white",
                logoUrl = user.Store?.LogoUrl ?? null
            });
        }

        return Ok(vendorsList);
    }

    // PUT: /api/admin/vendors/{id}/status
    [HttpPut("vendors/{id}/status")]
    public async Task<IActionResult> UpdateVendorStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null || user.Role != "vendor")
        {
            return NotFound("البائع غير موجود");
        }

        if (request.Status != "نشط" && request.Status != "قيد المراجعة" && request.Status != "موقوف")
        {
            return BadRequest("حالة غير صالحة");
        }

        user.Status = request.Status;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok();
    }

    // GET: /api/admin/products
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var dbProducts = await _context.Products
            .AsNoTracking()
            .Include(p => p.Store)
            .Include(p => p.Images)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var productsList = dbProducts.Select(p => new
        {
            id = p.Id.ToString(),
            _id = p.Id.ToString(),
            name = p.Name,
            store = p.Store?.StoreName ?? "غير معروف",
            price = p.Price,
            stock = p.Stock,
            status = string.IsNullOrWhiteSpace(p.Status) ? "pending" : p.Status,
            image = p.Images.FirstOrDefault()?.ImageUrl ?? "https://via.placeholder.com/150",
            category = p.Category?.ParentCategory != null ? p.Category.ParentCategory.Name : (p.Category?.Name ?? string.Empty),
            createdAt = p.CreatedAt == DateTime.MinValue ? DateTime.UtcNow : p.CreatedAt
        }).ToList();

        return Ok(productsList);
    }

    // PUT: /api/admin/products/{id}/status
    [HttpPut("products/{id}/status")]
    public async Task<IActionResult> UpdateProductStatus(Guid id, [FromBody] UpdateProductStatusRequest request)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound("المنتج غير موجود");
        }

        if (request.Status != "available" && request.Status != "pending" && request.Status != "rejected" && request.Status != "archived")
        {
            return BadRequest("حالة غير صالحة");
        }

        product.Status = request.Status;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok();
    }
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateProductStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
