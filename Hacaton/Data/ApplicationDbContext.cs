using Hacaton.Models;
using Microsoft.EntityFrameworkCore;

namespace Hacaton.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Буряк", Category = "Овочі", Price = 18m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1582515073490-39981397c445?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 2, Name = "Картопля", Category = "Овочі", Price = 24m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1518977676601-b53f82aba655?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 3, Name = "Морква", Category = "Овочі", Price = 16m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1447175008436-054170c2e979?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 4, Name = "Капуста", Category = "Овочі", Price = 22m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1576045057995-568f588f82fb?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 5, Name = "Цибуля", Category = "Овочі", Price = 12m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1502741338009-cac2772e18bc?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 6, Name = "Сметана", Category = "Молочні", Price = 52m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1563636619-e9143da7973b?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 7, Name = "Молоко", Category = "Молочні", Price = 58m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1550583724-b2692b85b150?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 8, Name = "Яйця", Category = "Молочні", Price = 84m, InStock = true, ImageUrl = "https://vip.shuvar.com/pub/media/catalog/product/_/3/_3.jpg" },
            new Product { Id = 9, Name = "Хліб", Category = "Хлібобулочні", Price = 36m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 10, Name = "Яблука", Category = "Фрукти", Price = 42m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1560806887-1e4cd0b6cbd6?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 11, Name = "Вода", Category = "Напої", Price = 28m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1548839140-29a749e1cf4d?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 12, Name = "Сир", Category = "Молочні", Price = 92m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1486297678162-eb2a19b0a32d?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 13, Name = "Куряче філе", Category = "М'ясо", Price = 120m, InStock = true, ImageUrl = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRvDp1l1CoLJXCl4nV8QwJuCBQJPo_T78QU9_guUehG0g&s" },
            new Product { Id = 14, Name = "Свинина", Category = "М'ясо", Price = 140m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1604908553252-7c7d8d0d3f27?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 15, Name = "Яловичина", Category = "М'ясо", Price = 180m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 16, Name = "Лосось", Category = "Риба", Price = 210m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1467003909585-2f8a72700288?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 17, Name = "Скумбрія", Category = "Риба", Price = 120m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1559847844-5315695dadae?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 18, Name = "Груша", Category = "Фрукти", Price = 54m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1471193945509-9ad0617afabf?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 19, Name = "Апельсини", Category = "Фрукти", Price = 66m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1547514701-42782101795e?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 20, Name = "Банани", Category = "Фрукти", Price = 48m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1571771894821-ce9b6c11b08e?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 21, Name = "Огірки", Category = "Овочі", Price = 34m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1449300079323-02e209d1d3f5?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 22, Name = "Помідори", Category = "Овочі", Price = 46m, InStock = true, ImageUrl = "https://images.unsplash.com/photo-1546094096-0df4bcaaa337?auto=format&fit=crop&w=600&q=80" },
            new Product { Id = 23, Name = "Йогурт", Category = "Молочні", Price = 39m, InStock = true, ImageUrl = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcS3UI3APAgFwIi15_OL9ztSqU9s45cOYVJIS-5mFlxqOw&s=10" }
        );
    }
}
