using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceDB.Data;
using EcommerceDB.Models;

namespace EcommerceDB.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoryController : ControllerBase
{
    private readonly EcommerceDbContext _context;

    public CategoryController(EcommerceDbContext context)
    {
        _context = context;
    }

    // GET: api/categories/tree
    [HttpGet("tree")]
    public async Task<IActionResult> GetCategoriesTree()
    {
        // Load all categories from DB
        var allCategories = await _context.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();

        // Build the tree starting from top-level (ParentCategoryId == null)
        var topLevel = allCategories
            .Where(c => c.ParentCategoryId == null)
            .Select(c => MapToDto(c, allCategories))
            .ToList();

        return Ok(topLevel);
    }

    // GET: api/categories
    [HttpGet]
    public async Task<IActionResult> GetAllCategories()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Include(c => c.ParentCategory)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var dtoList = categories.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description ?? string.Empty,
            ImageUrl = c.ImageUrl ?? string.Empty,
            ParentCategoryId = c.ParentCategoryId,
            ParentCategoryName = c.ParentCategory?.Name ?? string.Empty,
            HeroImages = ParseHeroImages(c.HeroImages)
        }).ToList();

        return Ok(dtoList);
    }

    // POST: api/categories
    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "اسم الفئة مطلوب" });
        }

        // Check if category name exists under the same parent to prevent duplicates
        var duplicateExists = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower() && c.ParentCategoryId == request.ParentCategoryId);

        if (duplicateExists)
        {
            return BadRequest(new { message = "هذه الفئة موجودة بالفعل في هذا القسم" });
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ImageUrl = request.ImageUrl,
            ParentCategoryId = request.ParentCategoryId,
            HeroImages = SerializeHeroImages(request.HeroImages)
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم إضافة الفئة بنجاح", categoryId = category.Id });
    }

    // PUT: api/categories/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound(new { message = "الفئة غير موجودة" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "اسم الفئة مطلوب" });
        }

        // Check if duplicate exists under the same parent
        var duplicateExists = await _context.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == request.Name.ToLower() && c.ParentCategoryId == category.ParentCategoryId);

        if (duplicateExists)
        {
            return BadRequest(new { message = "هذه الفئة موجودة بالفعل في هذا القسم" });
        }

        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim();
        if (request.ImageUrl != null)
        {
            category.ImageUrl = request.ImageUrl;
        }
        if (request.HeroImages != null)
        {
            category.HeroImages = SerializeHeroImages(request.HeroImages);
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "تم تعديل الفئة بنجاح" });
    }

    // DELETE: api/categories/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var category = await _context.Categories
            .Include(c => c.Subcategories)
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return NotFound(new { message = "الفئة غير موجودة" });
        }

        if (category.Subcategories.Any())
        {
            return BadRequest(new { message = "لا يمكن حذف هذه الفئة لوجود فئات فرعية مرتبطة بها." });
        }

        if (category.Products.Any())
        {
            return BadRequest(new { message = "لا يمكن حذف هذه الفئة لوجود منتجات مرتبطة بها." });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم حذف الفئة بنجاح" });
    }

    private static List<string> ParseHeroImages(string? heroImagesStr)
    {
        if (string.IsNullOrWhiteSpace(heroImagesStr)) return new List<string>();
        try
        {
            if (heroImagesStr.Trim().StartsWith("["))
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(heroImagesStr) ?? new List<string>();
            }
        }
        catch { }
        return heroImagesStr.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
    }

    private static string SerializeHeroImages(List<string>? heroImages)
    {
        if (heroImages == null || !heroImages.Any()) return string.Empty;
        return System.Text.Json.JsonSerializer.Serialize(heroImages);
    }

    private static CategoryTreeNodeDto MapToDto(Category category, List<Category> allCategories)
    {
        return new CategoryTreeNodeDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description ?? string.Empty,
            ImageUrl = category.ImageUrl ?? string.Empty,
            ParentCategoryId = category.ParentCategoryId,
            HeroImages = ParseHeroImages(category.HeroImages),
            Subcategories = allCategories
                .Where(c => c.ParentCategoryId == category.Id)
                .Select(c => MapToDto(c, allCategories))
                .ToList()
        };
    }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public List<string>? HeroImages { get; set; }
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? HeroImages { get; set; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string ParentCategoryName { get; set; } = string.Empty;
    public List<string> HeroImages { get; set; } = new();
}

public class CategoryTreeNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public List<string> HeroImages { get; set; } = new();
    public List<CategoryTreeNodeDto> Subcategories { get; set; } = new();
}
