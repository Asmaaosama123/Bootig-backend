using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceDB.Data;
using EcommerceDB.Models;

namespace EcommerceDB.Controllers;

[ApiController]
[Route("api")]
public class NavigationController : ControllerBase
{
    private readonly EcommerceDbContext _context;

    public NavigationController(EcommerceDbContext context)
    {
        _context = context;
    }

    [HttpGet("getActiveCategories")]
    public async Task<IActionResult> GetActiveCategories()
    {
        // Get all top-level categories (where ParentCategoryId is null)
        var parentCategories = await _context.Categories
            .Where(c => c.ParentCategoryId == null)
            .Select(c => c.Name.ToLower())
            .ToListAsync();

        // If database is empty, return default placeholders to match static names
        if (parentCategories.Count == 0)
        {
            parentCategories = new List<string> { "woman", "man", "kids" };
        }

        return Ok(new { categories = parentCategories });
    }

    [HttpGet("getSubCategories/{activeCategory}")]
    public async Task<IActionResult> GetSubCategories(string activeCategory)
    {
        var normalizedCategory = activeCategory.ToLower();
        if (normalizedCategory == "women" || normalizedCategory == "woman") normalizedCategory = "woman";
        if (normalizedCategory == "men" || normalizedCategory == "man") normalizedCategory = "man";

        // Find the parent category matching the activeCategory name (case-insensitive)
        var parentCategory = await _context.Categories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedCategory && c.ParentCategoryId == null);

        if (parentCategory == null)
        {
            return Ok(new { subcategories = new List<object>() });
        }

        // Get subcategory objects
        var subcategories = await _context.Categories
            .Where(c => c.ParentCategoryId == parentCategory.Id)
            .Select(c => new {
                name = c.Name.ToLower(),
                imageUrl = c.ImageUrl ?? "https://images.unsplash.com/photo-1526170375885-4d8ecf77b99f?w=300&h=300&fit=crop"
            })
            .ToListAsync();

        return Ok(new { subcategories });
    }

    [HttpGet("getSubSubCategories/{parentCategoryName}/{subcategoryName}")]
    public async Task<IActionResult> GetSubSubCategories(string parentCategoryName, string subcategoryName)
    {
        var normalizedParent = parentCategoryName.ToLower();
        if (normalizedParent == "women") normalizedParent = "woman";
        if (normalizedParent == "men") normalizedParent = "man";

        var normalizedSub = subcategoryName.ToLower();

        // Find the parent (top-level) category
        var parentCategory = await _context.Categories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedParent && c.ParentCategoryId == null);

        if (parentCategory == null)
            return Ok(new { subcategories = new List<object>() });

        // Find the mid-level subcategory under the parent
        var subCategory = await _context.Categories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedSub && c.ParentCategoryId == parentCategory.Id);

        if (subCategory == null)
            return Ok(new { subcategories = new List<object>() });

        // Get the sub-sub-categories under the subcategory
        var subSubCategories = await _context.Categories
            .Where(c => c.ParentCategoryId == subCategory.Id)
            .Select(c => new {
                name = c.Name.ToLower(),
                imageUrl = c.ImageUrl ?? "https://images.unsplash.com/photo-1526170375885-4d8ecf77b99f?w=300&h=300&fit=crop"
            })
            .ToListAsync();

        return Ok(new { subcategories = subSubCategories });
    }

    [HttpGet("getStores")]
    public async Task<IActionResult> GetStores()
    {
        var stores = await _context.Stores
            .Include(s => s.User)
            .ToListAsync();

        var storesList = new List<object>();
        foreach (var s in stores)
        {
            var products = await _context.Products
                .Include(p => p.Images)
                .Where(p => p.StoreId == s.Id && p.Status == "available")
                .Take(4)
                .ToListAsync();

            storesList.Add(new {
                id = s.Id.ToString(),
                name = s.StoreName,
                tagline = s.Category ?? "fashion",
                image = s.LogoUrl ?? "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=200",
                cover = s.CoverUrl ?? "https://images.unsplash.com/photo-1588099768531-a72d4a198538?w=600",
                products = products.Select(p => new {
                    id = p.Id.ToString(),
                    name = p.Name,
                    price = p.Price,
                    image = p.Images.FirstOrDefault()?.ImageUrl ?? "https://via.placeholder.com/150"
                }).ToList()
            });
        }
        return Ok(new { stores = storesList });
    }

    [HttpGet("getOffersByCategory/{activeCategory}")]
    public async Task<IActionResult> GetOffersByCategory(string activeCategory)
    {
        var normalizedCategory = activeCategory.ToLower();
        if (normalizedCategory == "women" || normalizedCategory == "woman") normalizedCategory = "woman";
        if (normalizedCategory == "men" || normalizedCategory == "man") normalizedCategory = "man";

        // Get active products under the active parent category that have discounts (OriginalPrice > Price)
        var products = await _context.Products
            .Include(p => p.Store)
            .Include(p => p.Images)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .Where(p => 
                (p.Category.Name.ToLower() == normalizedCategory || 
                 p.Category.ParentCategory!.Name.ToLower() == normalizedCategory) &&
                p.OriginalPrice > p.Price &&
                p.Status == "available")
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
                Store = p.Store?.StoreName ?? string.Empty,
                IsNew = p.IsNew,
                IsFeatured = p.IsFeatured,
                IsOffer = p.OriginalPrice.HasValue && p.OriginalPrice > p.Price,
                Rating = p.Rating,
                ReviewCount = p.ReviewCount,
                Image = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
                ImageUrl = p.Images.FirstOrDefault()?.ImageUrl ?? string.Empty,
                Images = p.Images.Select(img => img.ImageUrl).ToList()
            };
        }).ToList();

        // Response shape expected: { products: [...] } (mapped to setOffers(data.products))
        return Ok(new { products = productsDto });
    }

    [HttpGet("getStoresByCategory/{activeCategory}")]
    public async Task<IActionResult> GetStoresByCategory(string activeCategory)
    {
        var normalizedCategory = activeCategory.ToLower();
        if (normalizedCategory == "women" || normalizedCategory == "woman") normalizedCategory = "woman";
        if (normalizedCategory == "men" || normalizedCategory == "man") normalizedCategory = "man";

        // Retrieve vendors associated with this category
        var stores = await _context.Stores
            .Where(s => s.Category!.ToLower() == normalizedCategory || s.Category == null)
            .ToListAsync();

        var storesDto = stores.Select(s => new StoreDto
        {
            _id = s.Id.ToString(),
            StoreName = s.StoreName,
            StoreDescription = s.StoreDescription ?? string.Empty,
            Rating = s.Rating,
            ReviewCount = s.ReviewCount,
            LogoUrl = s.LogoUrl ?? string.Empty,
            CoverUrl = s.CoverUrl ?? string.Empty,
            IsFeatured = s.IsFeatured
        }).ToList();

        // Response shape expected: { vendors: [...] } (mapped to setTopStores(data.vendors))
        return Ok(new { vendors = storesDto });
    }

    [HttpGet("getCategoryByName/{categoryName}")]
    public async Task<IActionResult> GetCategoryByName(string categoryName)
    {
        var normalizedName = categoryName.ToLower();
        if (normalizedName == "women" || normalizedName == "woman") normalizedName = "woman";
        if (normalizedName == "men" || normalizedName == "man") normalizedName = "man";

        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedName && c.ParentCategoryId == null);

        if (category == null)
        {
            return NotFound(new { message = "Category not found" });
        }

        var heroImagesStr = category.HeroImages ?? "";
        List<string> heroImagesList;
        if (heroImagesStr.Trim().StartsWith("["))
        {
            try
            {
                heroImagesList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(heroImagesStr) ?? new List<string>();
            }
            catch
            {
                heroImagesList = new List<string>();
            }
        }
        else
        {
            heroImagesList = heroImagesStr.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }

        return Ok(new
        {
            id = category.Id,
            name = category.Name,
            description = category.Description ?? "",
            imageUrl = category.ImageUrl ?? "",
            heroImages = heroImagesList
        });
    }
}

public class StoreDto
{
    public string _id { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string StoreDescription { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public string LogoUrl { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
}
