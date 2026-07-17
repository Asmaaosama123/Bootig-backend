using Microsoft.EntityFrameworkCore;
using EcommerceDB.Models;

namespace EcommerceDB.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(EcommerceDbContext context)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Categories ADD HeroImages NVARCHAR(MAX) NULL;");
        }
        catch
        {
            // Column already exists or table not ready, skip safely
        }

        // Seed admin users if none exist
        var adminExists = await context.Users.AnyAsync(u => u.Role == "admin");
        if (!adminExists)
        {
            var adminUser1 = new User
            {
                Name = "المدير العام",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "admin",
                WhatsApp = "+966500000000"
            };
            var adminUser2 = new User
            {
                Name = "المدير العام",
                Username = "admin@bootig.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "admin",
                WhatsApp = "+966500000000"
            };
            context.Users.AddRange(adminUser1, adminUser2);
            await context.SaveChangesAsync();
            Console.WriteLine("✅ Seeding admin users: admin and admin@bootig.com with password: admin123");
        }

        // Check if database already has categories to prevent duplicate seeding
        if (await context.Categories.AnyAsync())
        {
            return;
        }

        // 1. Seed Parent Categories
        var womanCategory = new Category { Name = "woman", Description = "أزياء وملابس نسائية عصرية" };
        var manCategory = new Category { Name = "man", Description = "أزياء وملابس رجالية أنيقة" };
        var kidsCategory = new Category { Name = "kids", Description = "ملابس وألعاب أطفال بجودة عالية" };

        context.Categories.AddRange(womanCategory, manCategory, kidsCategory);
        await context.SaveChangesAsync();

        // Seed Subcategories
        var dressesSub = new Category { Name = "dresses", ParentCategoryId = womanCategory.Id, Description = "فساتين سهرة، صيفية، وكاجوال" };
        var topsSub = new Category { Name = "tops", ParentCategoryId = womanCategory.Id, Description = "بلوزات وتيشرتات نسائية راقية" };
        var shoesSub = new Category { Name = "shoes", ParentCategoryId = womanCategory.Id, Description = "أحذية نسائية كلاسيكية ورياضية" };

        var shirtsSub = new Category { Name = "shirts", ParentCategoryId = manCategory.Id, Description = "قمصان رجالية كاجوال ورسمية" };
        var manShoesSub = new Category { Name = "shoes", ParentCategoryId = manCategory.Id, Description = "أحذية رجالية ورياضية أنيقة" };
        var accessoriesSub = new Category { Name = "accessories", ParentCategoryId = manCategory.Id, Description = "إكسسوارات وساعات رجالية" };

        var clothesSub = new Category { Name = "clothes", ParentCategoryId = kidsCategory.Id, Description = "ملابس وتيشرتات مريحة للأطفال" };
        var toysSub = new Category { Name = "toys", ParentCategoryId = kidsCategory.Id, Description = "ألعاب تعليمية وترفيهية للأطفال" };

        context.Categories.AddRange(dressesSub, topsSub, shoesSub, shirtsSub, manShoesSub, accessoriesSub, clothesSub, toysSub);
        await context.SaveChangesAsync();

        // 2. Seed Default Vendor Users and Stores
        var vendorUser = new User
        {
            Name = "موقع الموضة",
            WhatsApp = "+2222000000000",
            Role = "vendor"
        };
        context.Users.Add(vendorUser);
        await context.SaveChangesAsync();

        var store = new Store
        {
            UserId = vendorUser.Id,
            StoreName = "Fashion Store",
            StoreDescription = "أحدث صيحات الموضة النسائية والفساتين الراقية",
            Category = "woman",
            IsFeatured = true,
            Rating = 4.8m,
            ReviewCount = 1247
        };
        context.Stores.Add(store);
        await context.SaveChangesAsync();

        var zaraUser = new User
        {
            Name = "زارا",
            WhatsApp = "+9999000000000",
            Role = "vendor"
        };
        context.Users.Add(zaraUser);
        await context.SaveChangesAsync();

        var zaraStore = new Store
        {
            UserId = zaraUser.Id,
            StoreName = "Zara",
            StoreDescription = "أزياء عالمية عصرية",
            Category = "woman",
            IsFeatured = true,
            Rating = 4.9m,
            ReviewCount = 892
        };
        context.Stores.Add(zaraStore);
        await context.SaveChangesAsync();

        // 3. Seed Products
        var product1 = new Product
        {
            StoreId = store.Id,
            CategoryId = dressesSub.Id,
            Name = "Summer Floral Dress",
            Description = "Light floral dress perfect for summer days. Made with premium fabric.",
            Price = 89.99m,
            OriginalPrice = 120.00m,
            Stock = 50,
            Rating = 4.8m,
            ReviewCount = 152,
            Status = "available",
            IsNew = true,
            IsFeatured = true
        };

        var product2 = new Product
        {
            StoreId = zaraStore.Id,
            CategoryId = dressesSub.Id,
            Name = "Elegant Maxi Dress",
            Description = "Premium black maxi dress for special occasions.",
            Price = 149.99m,
            OriginalPrice = 180.00m,
            Stock = 20,
            Rating = 4.9m,
            ReviewCount = 85,
            Status = "available",
            IsNew = true,
            IsFeatured = true
        };

        context.Products.AddRange(product1, product2);
        await context.SaveChangesAsync();

        // Add Product Images
        context.ProductImages.AddRange(
            new ProductImage { ProductId = product1.Id, ImageUrl = "https://images.pexels.com/photos/428338/pexels-photo-428338.jpeg?auto=compress&cs=tinysrgb&w=600" },
            new ProductImage { ProductId = product2.Id, ImageUrl = "https://images.pexels.com/photos/428340/pexels-photo-428340.jpeg?auto=compress&cs=tinysrgb&w=600" }
        );

        // Add Product Sizes
        context.ProductSizes.AddRange(
            new ProductSize { ProductId = product1.Id, SizeName = "S" },
            new ProductSize { ProductId = product1.Id, SizeName = "M" },
            new ProductSize { ProductId = product1.Id, SizeName = "L" },
            new ProductSize { ProductId = product2.Id, SizeName = "M" },
            new ProductSize { ProductId = product2.Id, SizeName = "L" }
        );

        // Add Product Colors
        context.ProductColors.AddRange(
            new ProductColor { ProductId = product1.Id, ColorName = "Pink" },
            new ProductColor { ProductId = product1.Id, ColorName = "Yellow" },
            new ProductColor { ProductId = product2.Id, ColorName = "Black" }
        );

        await context.SaveChangesAsync();
    }
}
