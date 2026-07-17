using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EcommerceDB.Data;
using EcommerceDB.Models;

namespace EcommerceDB.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly EcommerceDbContext _context;

    public OrderController(EcommerceDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // POST /api/orders
    // Create a new order
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var customerId = Guid.Parse(userIdClaim);

        if (request.OrderItems == null || !request.OrderItems.Any())
        {
            return BadRequest(new { message = "لا توجد منتجات في الطلب" });
        }

        if (request.ShippingAddress == null || string.IsNullOrWhiteSpace(request.ShippingAddress.Address) || string.IsNullOrWhiteSpace(request.ShippingAddress.City))
        {
            return BadRequest(new { message = "مطلوب عنوان الشحن (address و city)" });
        }

        var fullAddress = $"{request.ShippingAddress.City} - {request.ShippingAddress.Address}";

        var order = new Order
        {
            CustomerId = customerId,
            TotalPrice = request.TotalPrice,
            PaymentMethod = request.PaymentMethod,
            ShippingAddress = fullAddress,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in request.OrderItems)
        {
            if (!Guid.TryParse(item.ProductId, out var productGuid))
            {
                return BadRequest(new { message = $"معرف المنتج غير صالح: {item.ProductId}" });
            }

            var product = await _context.Products.FindAsync(productGuid);
            if (product == null)
            {
                return BadRequest(new { message = $"المنتج غير موجود: {item.ProductId}" });
            }

            var orderItem = new OrderItem
            {
                ProductId = productGuid,
                Quantity = item.Quantity,
                Price = item.Price,
                SelectedSize = item.SelectedSize,
                SelectedColor = item.SelectedColor
            };

            order.OrderItems.Add(orderItem);

            // Decrement stock
            product.Stock -= item.Quantity;
            if (product.Stock < 0)
            {
                product.Stock = 0;
            }
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Clear user's cart from database if they had any saved cart items
        var dbCartItems = await _context.CartItems.Where(ci => ci.UserId == customerId).ToListAsync();
        if (dbCartItems.Any())
        {
            _context.CartItems.RemoveRange(dbCartItems);
            await _context.SaveChangesAsync();
        }

        return StatusCode(201, new
        {
            id = order.Id.ToString(),
            totalPrice = order.TotalPrice,
            paymentMethod = order.PaymentMethod,
            shippingAddress = order.ShippingAddress,
            status = order.Status,
            createdAt = order.CreatedAt
        });
    }

    // ============================================================
    // GET /api/orders/customer
    // Get logged-in customer's own orders
    // ============================================================
    [HttpGet("customer")]
    public async Task<IActionResult> GetCustomerOrders()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var customerId = Guid.Parse(userIdClaim);

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(o => new
        {
            id = o.Id.ToString(),
            date = o.CreatedAt,
            status = o.Status,
            total = o.TotalPrice,
            shippingAddress = o.ShippingAddress,
            paymentMethod = o.PaymentMethod,
            items = o.OrderItems.Select(oi => new
            {
                id = oi.Id.ToString(),
                name = oi.Product.Name,
                image = oi.Product.Images != null && oi.Product.Images.Any()
                    ? oi.Product.Images.First().ImageUrl
                    : "",
                quantity = oi.Quantity,
                price = oi.Price,
                size = oi.SelectedSize,
                color = oi.SelectedColor
            })
        });

        return Ok(result);
    }

    // ============================================================
    // GET /api/orders/myorders
    // Get logged in vendor's orders (containing products from vendor's store)
    // ============================================================
    [HttpGet("myorders")]
    public async Task<IActionResult> GetMyOrders()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var userId = Guid.Parse(userIdClaim);

        var store = await _context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);
        if (store == null)
        {
            return NotFound(new { message = "لم يتم العثور على بيانات المتجر للتاجر الحالي" });
        }

        var storeProductIds = await _context.Products
            .AsNoTracking()
            .Where(p => p.StoreId == store.Id)
            .Select(p => p.Id)
            .ToListAsync();

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.OrderItems.Any(oi => storeProductIds.Contains(oi.ProductId)))
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(o => new
        {
            id = o.Id.ToString(),
            totalPrice = o.TotalPrice,
            paymentMethod = o.PaymentMethod,
            shippingAddress = o.ShippingAddress,
            status = o.Status,
            createdAt = o.CreatedAt,
            user = new
            {
                _id = o.Customer.Id.ToString(),
                name = o.Customer.Name,
                whatsapp = o.Customer.WhatsApp
            },
            orderItems = o.OrderItems.Where(oi => storeProductIds.Contains(oi.ProductId)).Select(oi => new
            {
                id = oi.Id.ToString(),
                productId = oi.ProductId.ToString(),
                name = oi.Product.Name,
                price = oi.Price,
                qty = oi.Quantity,
                quantity = oi.Quantity,
                selectedSize = oi.SelectedSize,
                selectedColor = oi.SelectedColor
            })
        });

        return Ok(result);
    }

    // ============================================================
    // GET /api/orders
    // Get all orders in the system (Admin only)
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> GetAllOrders()
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        if (roleClaim != "admin")
        {
            return Forbid();
        }

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Store)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(o => new
        {
            id = o.Id.ToString(),
            totalPrice = o.TotalPrice,
            paymentMethod = o.PaymentMethod,
            shippingAddress = o.ShippingAddress,
            status = o.Status,
            createdAt = o.CreatedAt,
            user = new
            {
                _id = o.Customer.Id.ToString(),
                name = o.Customer.Name,
                whatsapp = o.Customer.WhatsApp
            },
            orderItems = o.OrderItems.Select(oi => new
            {
                id = oi.Id.ToString(),
                productId = oi.ProductId.ToString(),
                name = oi.Product.Name,
                price = oi.Price,
                qty = oi.Quantity,
                quantity = oi.Quantity,
                selectedSize = oi.SelectedSize,
                selectedColor = oi.SelectedColor,
                storeName = oi.Product.Store.StoreName
            })
        });

        return Ok(result);
    }

    // ============================================================
    // PUT /api/orders/{id}/status
    // Update order status (Admin or Vendor owner)
    // ============================================================
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var userId = Guid.Parse(userIdClaim);

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = "الطلب غير موجود" });
        }

        if (roleClaim == "vendor")
        {
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.UserId == userId);
            if (store == null)
            {
                return Forbid();
            }

            var storeProductIds = await _context.Products
                .Where(p => p.StoreId == store.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var isOrderOwner = order.OrderItems.Any(oi => storeProductIds.Contains(oi.ProductId));
            if (!isOrderOwner)
            {
                return Unauthorized(new { message = "غير مصرح لك بتحديث هذا الطلب" });
            }
        }

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = order.Id.ToString(),
            status = order.Status,
            updatedAt = order.UpdatedAt
        });
    }
}

public class CreateOrderRequest
{
    public List<CreateOrderItemRequest> OrderItems { get; set; } = new();
    public CreateOrderAddressRequest ShippingAddress { get; set; } = null!;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
}

public class CreateOrderItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string? SelectedSize { get; set; }
    public string? SelectedColor { get; set; }
}

public class CreateOrderAddressRequest
{
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class UpdateOrderStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
