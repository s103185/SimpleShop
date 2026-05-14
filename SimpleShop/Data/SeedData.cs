using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimpleShop.Models;
using SimpleShop.Models.Enums; // For OrderStatus

namespace SimpleShop.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider,
                                            UserManager<ApplicationUser> userManager,
                                            RoleManager<IdentityRole> roleManager,
                                            ApplicationDbContext context,
                                            IConfiguration configuration)
        {
            // 檢查資料庫是否已遷移
            context.Database.Migrate(); // 自動應用掛起的遷移

            // 1. Seed Roles
            string[] roleNames = { "Admin", "Customer" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Admin User
            string adminEmail = configuration["AdminEmail"] ?? "admin@example.com";
            string adminPassword = configuration["AdminPassword"] ?? "Password123!";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true, // 直接確認
                    FullName = "Administrator"
                };
                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 3. Seed Products (if none exist)
            if (!context.Products.Any())
            {
                context.Products.AddRange(
                    new Product
                    {
                        Name = "RX-78-2 鋼彈 (Gundam)",
                        Description = "造型簡單大方，是所有鋼彈模型的技術指標，收藏必收的首選。",
                        Price = 150.00m,
                        StockQuantity = 100,
                        ImageUrl = "/images/RX-78-2.jpg" // 假設圖片放在 wwwroot/images/
                    },
                    new Product
                    {
                        Name = "MSZ-006 Z 鋼彈 (Zeta Gundam)",
                        Description = "結構精密，模型展現了高難度的變形設計，視覺感非常前衛。",
                        Price = 150.00m,
                        StockQuantity = 100,
                        ImageUrl = "/images/MSZ-006 Z.jpg" // 假設圖片放在 wwwroot/images/
                    },
                    new Product
                    {
                        Name = "RX-0 獨角獸鋼彈 (Unicorn Gundam)",
                        Description = "全白外觀可展開變形成「毀滅模式」，露出內部紅色的框架，視覺效果極其華麗。",
                        Price = 150.00m,
                        StockQuantity = 100,
                        ImageUrl = "/images/RX-0.jpg" // 假設圖片放在 wwwroot/images/
                    },
                    new Product
                    {
                        Name = "攻擊自由鋼彈 (Strike Freedom Gundam)",
                        Description = "標誌性的金色骨架與藍色大翅膀，張開後的「全彈發射」姿勢帥氣度滿分。",
                        Price = 150.00m,
                        StockQuantity = 100,
                        ImageUrl = "/images/ZGMF-X20A.jpg" // 假設圖片放在 wwwroot/images/
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}