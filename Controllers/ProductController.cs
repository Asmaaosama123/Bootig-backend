using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using EcommerceDB.Data;
using EcommerceDB.Models;

namespace EcommerceDB.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly EcommerceDbContext _context;

    public ProductController(EcommerceDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllProducts([FromQuery] string? keyword)
    {
        var query = _context.Products
            .Include(p => p.Store)
            .Include(p => p.Images)
            .Include(p => p.Sizes)
            .Include(p => p.Colors)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .Where(p => p.Status == "available")
            .AsQueryable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => p.Name.Contains(keyword) || p.Description.Contains(keyword));
        }

        var products = await query.ToListAsync();

        var productsDto = products.Select(p => {
            string parentCatName = p.Category?.ParentCategory != null ? p.Category.ParentCategory.Name : (p.Category?.Name ?? string.Empty);
            string subCatName = p.Category?.ParentCategory != null ? p.Category.Name : string.Empty;

            if (parentCatName.ToLower() == "woman") parentCatName = "women";
            if (parentCatName.ToLower() == "man") parentCatName = "men";

            return new ProductDto
            {
                Id = p.Id.ToString(),
                _id = p.Id.ToString(),
                Name = p.Name,
                Price = p.Price,
                OriginalPrice = p.OriginalPrice,
                Description = p.Description,
                Category = parentCatName,
                Subcategory = subCatName,
                SubSubcategory = string.Empty,
                Store = p.Store?.StoreName ?? string.Empty,
                IsNew = p.IsNew,
                IsFeatured = p.IsFeatured,
                IsOffer = p.OriginalPrice.HasValue && p.OriginalPrice > p.Price,
                Rating = p.Rating,
                ReviewCount = p.ReviewCount,
                Stock = p.Stock,
                Status = p.Status,
                Image = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
                ImageUrl = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
                Images = p.Images.Select(img => img.ImageUrl).ToList(),
                Sizes = p.Sizes.Select(s => s.SizeName).ToList(),
                Colors = p.Colors.Select(c => c.ColorName).ToList()
            };
        }).ToList();

        // Wrap products in { products: [...] } as expected by Header.DebouncedSearch (data.products)
        return Ok(new { products = productsDto });
    }

    [HttpGet("category/{categoryName}")]
    public async Task<IActionResult> GetProductsByCategory(string categoryName, [FromQuery] string? subcategoryName)
    {
        var normalizedCategory = categoryName.ToLower();
        if (normalizedCategory == "women" || normalizedCategory == "woman") normalizedCategory = "woman";
        if (normalizedCategory == "men" || normalizedCategory == "man") normalizedCategory = "man";

        var query = _context.Products
            .Include(p => p.Store)
            .Include(p => p.Images)
            .Include(p => p.Sizes)
            .Include(p => p.Colors)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .Where(p => p.Status == "available")
            .AsQueryable();

        if (string.IsNullOrEmpty(subcategoryName))
        {
            // Filter by parent category name
            query = query.Where(p => 
                p.Category.Name.ToLower() == normalizedCategory || 
                p.Category.ParentCategory!.Name.ToLower() == normalizedCategory);
        }
        else
        {
            // Filter by specific subcategory name and parent category name
            var normalizedSub = subcategoryName.ToLower();
            query = query.Where(p => 
                p.Category.Name.ToLower() == normalizedSub && 
                (p.Category.ParentCategory!.Name.ToLower() == normalizedCategory || p.Category.ParentCategory == null));
        }

        var products = await query.ToListAsync();
        var productsDto = products.Select(p => new ProductDto
        {
            Id = p.Id.ToString(),
            _id = p.Id.ToString(),
            Name = p.Name,
            Price = p.Price,
            OriginalPrice = p.OriginalPrice,
            Description = p.Description,
            Category = p.Category?.ParentCategory != null ? p.Category.ParentCategory.Name : (p.Category?.Name ?? string.Empty),
            Subcategory = p.Category?.ParentCategory != null ? p.Category.Name : string.Empty,
            Store = p.Store?.StoreName ?? string.Empty,
            IsNew = p.IsNew,
            IsFeatured = p.IsFeatured,
            IsOffer = p.OriginalPrice.HasValue && p.OriginalPrice > p.Price,
            Rating = p.Rating,
            ReviewCount = p.ReviewCount,
            Stock = p.Stock,
            Status = p.Status,
            Image = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
            ImageUrl = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
            Images = p.Images.Select(img => img.ImageUrl).ToList(),
            Sizes = p.Sizes.Select(s => s.SizeName).ToList(),
            Colors = p.Colors.Select(c => c.ColorName).ToList()
        }).ToList();

        return Ok(new { products = productsDto });
    }

    // Support both GET /api/products/vendor/{vendorId} and GET /api/api/products/vendor/{vendorId}
    // to safeguard against Axios double-prefix typos in the frontend code
    [HttpGet("vendor/{vendorId}")]
    [HttpGet("/api/api/products/vendor/{vendorId}")]
    public async Task<IActionResult> GetProductsByVendor(string vendorId)
    {
        if (!Guid.TryParse(vendorId, out var vendorGuid))
        {
            return BadRequest(new { message = "Invalid vendor ID format." });
        }

        var vendor = await _context.Stores
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == vendorGuid);

        if (vendor == null)
        {
            return NotFound(new { message = "Vendor not found." });
        }

        var products = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Sizes)
            .Include(p => p.Colors)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .Where(p => p.StoreId == vendorGuid && p.Status == "available")
            .ToListAsync();

        var productsDto = products.Select(p => {
            string parentCatName = p.Category?.ParentCategory != null ? p.Category.ParentCategory.Name : (p.Category?.Name ?? string.Empty);
            string subCatName = p.Category?.ParentCategory != null ? p.Category.Name : string.Empty;

            if (parentCatName.ToLower() == "woman") parentCatName = "women";
            if (parentCatName.ToLower() == "man") parentCatName = "men";

            return new ProductDto
            {
                Id = p.Id.ToString(),
                _id = p.Id.ToString(),
                Name = p.Name,
                Price = p.Price,
                OriginalPrice = p.OriginalPrice,
                Description = p.Description,
                Category = parentCatName,
                Subcategory = subCatName,
                SubSubcategory = string.Empty,
                Store = vendor.StoreName,
                IsNew = p.IsNew,
                IsFeatured = p.IsFeatured,
                IsOffer = p.OriginalPrice.HasValue && p.OriginalPrice > p.Price,
                Rating = p.Rating,
                ReviewCount = p.ReviewCount,
                Stock = p.Stock,
                Status = p.Status,
                Image = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
                ImageUrl = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
                Images = p.Images.Select(img => img.ImageUrl).ToList(),
                Sizes = p.Sizes.Select(s => s.SizeName).ToList(),
                Colors = p.Colors.Select(c => c.ColorName).ToList()
            };
        }).ToList();

        // Response shape expected: { vendor: { storeName, storeDescription }, products: [...] }
        return Ok(new
        {
            vendor = new
            {
                storeName = vendor.StoreName,
                storeDescription = vendor.StoreDescription ?? string.Empty,
                logoUrl = vendor.LogoUrl ?? string.Empty,
                coverUrl = vendor.CoverUrl ?? string.Empty
            },
            products = productsDto
        });
    }

    [Authorize]
    [HttpGet("myproducts")]
    public async Task<IActionResult> GetMyProducts()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "غير مصرح بالدخول" });
        }

        var store = await _context.Stores.FirstOrDefaultAsync(s => s.UserId == userId);
        if (store == null)
        {
            return BadRequest(new { message = "المتجر غير موجود" });
        }

        var products = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Sizes)
            .Include(p => p.Colors)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .Where(p => p.StoreId == store.Id)
            .ToListAsync();

        var productsDto = products.Select(p => {
            string parentCatName = p.Category?.ParentCategory != null ? p.Category.ParentCategory.Name : (p.Category?.Name ?? string.Empty);
            string subCatName = p.Category?.ParentCategory != null ? p.Category.Name : string.Empty;

            if (parentCatName.ToLower() == "woman") parentCatName = "women";
            if (parentCatName.ToLower() == "man") parentCatName = "men";

            return new ProductDto
            {
                Id = p.Id.ToString(),
                _id = p.Id.ToString(),
                Name = p.Name,
                Price = p.Price,
                OriginalPrice = p.OriginalPrice,
                Description = p.Description,
                Category = parentCatName,
                Subcategory = subCatName,
                SubSubcategory = string.Empty,
                Store = store.StoreName,
                IsNew = p.IsNew,
                IsFeatured = p.IsFeatured,
                IsOffer = p.OriginalPrice.HasValue && p.OriginalPrice > p.Price,
                Rating = p.Rating,
                ReviewCount = p.ReviewCount,
                Stock = p.Stock,
                Status = p.Status,
                Image = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
                ImageUrl = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
                Images = p.Images.Select(img => img.ImageUrl).ToList(),
                Sizes = p.Sizes.Select(s => s.SizeName).ToList(),
                Colors = p.Colors.Select(c => c.ColorName).ToList()
            };
        }).ToList();

        return Ok(new { products = productsDto });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var p = await _context.Products
            .Include(p => p.Store)
            .Include(p => p.Images)
            .Include(p => p.Sizes)
            .Include(p => p.Colors)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .FirstOrDefaultAsync(product => product.Id == id);

        if (p == null)
        {
            return NotFound(new { message = "المنتج غير موجود" });
        }

        string parentCatName = p.Category?.ParentCategory != null ? p.Category.ParentCategory.Name : (p.Category?.Name ?? string.Empty);
        string subCatName = p.Category?.ParentCategory != null ? p.Category.Name : string.Empty;

        if (parentCatName.ToLower() == "woman") parentCatName = "women";
        if (parentCatName.ToLower() == "man") parentCatName = "men";

        var productDto = new ProductDto
        {
            Id = p.Id.ToString(),
            _id = p.Id.ToString(),
            Name = p.Name,
            Price = p.Price,
            OriginalPrice = p.OriginalPrice,
            Description = p.Description,
            Category = parentCatName,
            Subcategory = subCatName,
            SubSubcategory = string.Empty,
            Store = p.Store?.StoreName ?? string.Empty,
            IsNew = p.IsNew,
            IsFeatured = p.IsFeatured,
            IsOffer = p.OriginalPrice.HasValue && p.OriginalPrice > p.Price,
            Rating = p.Rating,
            ReviewCount = p.ReviewCount,
            Stock = p.Stock,
            Status = p.Status,
            Image = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
            ImageUrl = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
            Images = p.Images.Select(img => img.ImageUrl).ToList(),
            Sizes = p.Sizes.Select(s => s.SizeName).ToList(),
            Colors = p.Colors.Select(c => c.ColorName).ToList()
        };

        return Ok(productDto);
    }

    [Authorize]
    [HttpGet("/api/vendor/stats")]
    public async Task<IActionResult> GetVendorStats()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "غير مصرح بالدخول" });
        }

        var store = await _context.Stores.FirstOrDefaultAsync(s => s.UserId == userId);
        if (store == null)
        {
            return BadRequest(new { message = "المتجر غير موجود" });
        }

        var productCount = await _context.Products.CountAsync(p => p.StoreId == store.Id);

        // Get all product IDs belonging to this store
        var storeProductIds = await _context.Products
            .Where(p => p.StoreId == store.Id)
            .Select(p => p.Id)
            .ToListAsync();

        // Get all order items for this store's products
        var orderItems = await _context.OrderItems
            .Where(oi => storeProductIds.Contains(oi.ProductId))
            .ToListAsync();

        // Total sales = sum of (price * quantity) for all order items
        var totalSales = orderItems.Sum(oi => oi.Price * oi.Quantity);

        // Profits = 80% of sales (after platform fee of 20%)
        var totalProfits = totalSales * 0.8m;

        return Ok(new
        {
            products = productCount,
            sales = Math.Round(totalSales, 2),
            profits = Math.Round(totalProfits, 2)
        });
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "غير مصرح بالدخول" });
        }

        var store = await _context.Stores.FirstOrDefaultAsync(s => s.UserId == userId);
        if (store == null)
        {
            return BadRequest(new { message = "المتجر غير موجود" });
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.StoreId == store.Id);
        if (product == null)
        {
            return NotFound(new { message = "المنتج غير موجود أو لا ينتمي لهذا المتجر" });
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم حذف المنتج بنجاح" });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AddProduct([FromBody] AddProductRequest request)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "غير مصرح بالدخول" });
        }

        var store = await _context.Stores.FirstOrDefaultAsync(s => s.UserId == userId);
        if (store == null)
        {
            return BadRequest(new { message = "المتجر غير موجود" });
        }

        // Category resolution
        var parentName = request.Category.ToLower();
        if (parentName == "women") parentName = "woman";
        if (parentName == "men") parentName = "man";
        if (parentName == "offers") parentName = (store.Category ?? "woman").ToLower();

        var parentCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == parentName && c.ParentCategoryId == null);
        if (parentCat == null)
        {
            return BadRequest(new { message = $"قسم رئيسي غير موجود: {request.Category}" });
        }

        var subName = request.Subcategory.ToLower();
        var subCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == subName && c.ParentCategoryId == parentCat.Id);
        var finalCategoryId = subCat != null ? subCat.Id : parentCat.Id;

        // Is it an offer? An offer has originalPrice > price
        decimal? originalPrice = null;
        if (request.OriginalPrice != null && request.OriginalPrice > request.Price)
        {
            originalPrice = request.OriginalPrice;
        }

        var product = new Product
        {
            StoreId = store.Id,
            CategoryId = finalCategoryId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Price = request.Price,
            OriginalPrice = originalPrice,
            Stock = request.Stock ?? 0,
            Status = "pending", // Default to pending
            IsNew = true,
            IsFeatured = originalPrice != null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Sizes
        if (request.Sizes != null)
        {
            foreach (var sizeName in request.Sizes)
            {
                _context.ProductSizes.Add(new ProductSize { ProductId = product.Id, SizeName = sizeName });
            }
        }

        // Colors
        if (request.Colors != null)
        {
            foreach (var colorInput in request.Colors)
            {
                _context.ProductColors.Add(new ProductColor { ProductId = product.Id, ColorName = colorInput.Name });
            }
        }

        // Images
        if (!string.IsNullOrEmpty(request.Image))
        {
            _context.ProductImages.Add(new ProductImage { ProductId = product.Id, ImageUrl = request.Image });
        }
        if (request.Images != null)
        {
            foreach (var imgUrl in request.Images)
            {
                if (imgUrl != request.Image)
                {
                    _context.ProductImages.Add(new ProductImage { ProductId = product.Id, ImageUrl = imgUrl });
                }
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "تمت إضافة المنتج بنجاح", productId = product.Id });
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] AddProductRequest request)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "غير مصرح بالدخول" });
        }

        var store = await _context.Stores.FirstOrDefaultAsync(s => s.UserId == userId);
        if (store == null)
        {
            return BadRequest(new { message = "المتجر غير موجود" });
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.StoreId == store.Id);
        if (product == null)
        {
            return NotFound(new { message = "المنتج غير موجود أو لا ينتمي لمتجرك" });
        }

        // Category resolution
        var parentName = request.Category.ToLower();
        if (parentName == "women") parentName = "woman";
        if (parentName == "men") parentName = "man";
        if (parentName == "offers") parentName = (store.Category ?? "woman").ToLower();

        var parentCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == parentName && c.ParentCategoryId == null);
        if (parentCat != null)
        {
            var subName = request.Subcategory.ToLower();
            var subCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == subName && c.ParentCategoryId == parentCat.Id);
            product.CategoryId = subCat != null ? subCat.Id : parentCat.Id;
        }

        product.Name = request.Name;
        product.Price = request.Price;
        product.Stock = request.Stock ?? product.Stock;
        product.Description = request.Description ?? string.Empty;
        if (request.OriginalPrice != null && request.OriginalPrice > request.Price)
        {
            product.OriginalPrice = request.OriginalPrice;
        }
        else
        {
            product.OriginalPrice = null;
        }
        product.UpdatedAt = DateTime.UtcNow;

        // Clear existing sizes, colors, images
        var oldSizes = await _context.ProductSizes.Where(ps => ps.ProductId == product.Id).ToListAsync();
        _context.ProductSizes.RemoveRange(oldSizes);

        var oldColors = await _context.ProductColors.Where(pc => pc.ProductId == product.Id).ToListAsync();
        _context.ProductColors.RemoveRange(oldColors);

        var oldImages = await _context.ProductImages.Where(pi => pi.ProductId == product.Id).ToListAsync();
        _context.ProductImages.RemoveRange(oldImages);

        await _context.SaveChangesAsync();

        // Add new sizes
        if (request.Sizes != null)
        {
            foreach (var sizeName in request.Sizes)
            {
                _context.ProductSizes.Add(new ProductSize { ProductId = product.Id, SizeName = sizeName });
            }
        }

        // Add new colors
        if (request.Colors != null)
        {
            foreach (var colorInput in request.Colors)
            {
                _context.ProductColors.Add(new ProductColor { ProductId = product.Id, ColorName = colorInput.Name });
            }
        }

        // Add new images
        if (!string.IsNullOrEmpty(request.Image))
        {
            _context.ProductImages.Add(new ProductImage { ProductId = product.Id, ImageUrl = request.Image });
        }
        if (request.Images != null)
        {
            foreach (var imgUrl in request.Images)
            {
                if (imgUrl != request.Image)
                {
                    _context.ProductImages.Add(new ProductImage { ProductId = product.Id, ImageUrl = imgUrl });
                }
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "تم تعديل المنتج بنجاح" });
    }

    [Authorize]
    [HttpGet("/api/vendor/profile")]
    public async Task<IActionResult> GetVendorProfile()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "غير مصرح بالدخول" });
        }

        var store = await _context.Stores.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == userId);
        if (store == null)
        {
            return BadRequest(new { message = "المتجر غير موجود" });
        }

        return Ok(new
        {
            storeName = store.StoreName,
            storeDescription = store.StoreDescription ?? string.Empty,
            username = store.User.Username ?? string.Empty,
            phone = store.User.WhatsApp ?? string.Empty,
            logoUrl = store.LogoUrl ?? string.Empty,
            coverUrl = store.CoverUrl ?? string.Empty
        });
    }

    [Authorize]
    [HttpPut("/api/vendor/profile")]
    public async Task<IActionResult> UpdateVendorProfile([FromBody] UpdateVendorProfileRequest request)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "غير مصرح بالدخول" });
        }

        var store = await _context.Stores.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == userId);
        if (store == null)
        {
            return BadRequest(new { message = "المتجر غير موجود" });
        }

        store.StoreName = request.StoreName;
        store.StoreDescription = request.StoreDescription;
        if (request.LogoUrl != null) store.LogoUrl = request.LogoUrl;
        if (request.CoverUrl != null) store.CoverUrl = request.CoverUrl;

        store.User.Name = request.StoreName; // sync user name
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            store.User.Username = request.Username.Trim().ToLower();
        }
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            store.User.WhatsApp = request.Phone.Trim();
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "تم تعديل الملف الشخصي بنجاح" });
    }
}

public class UpdateVendorProfileRequest
{
    public string StoreName { get; set; } = string.Empty;
    public string StoreDescription { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }
}

public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string _id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subcategory { get; set; } = string.Empty;
    public string SubSubcategory { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public bool IsNew { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsOffer { get; set; }
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public int Stock { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<string> Sizes { get; set; } = new();
    public List<string> Colors { get; set; } = new();
}

public class ColorInput
{
    public string Name { get; set; } = string.Empty;
}

public class AddProductRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subcategory { get; set; } = string.Empty;
    public string SubSubcategory { get; set; } = string.Empty;
    public int? Stock { get; set; }
    public List<string> Sizes { get; set; } = new();
    public List<ColorInput> Colors { get; set; } = new();
    public string Image { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
}
