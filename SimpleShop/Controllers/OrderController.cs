// 檔案：SimpleShop/Controllers/OrderController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpleShop.Data;
using SimpleShop.Models;         // 確保 Order, OrderItem, ApplicationUser, OrderCheckoutViewModel 在這裡
using SimpleShop.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleShop.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ShoppingCart _shoppingCart;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public OrderController(ApplicationDbContext context,
                               ShoppingCart shoppingCart,
                               UserManager<ApplicationUser> userManager,
                               IEmailSender emailSender)
        {
            _context = context;
            _shoppingCart = shoppingCart;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        public IActionResult Checkout()
        {
            if (!_shoppingCart.Items.Any())
            {
                TempData["ErrorMessage"] = "您的購物車是空的。";
                return RedirectToAction("Index", "Cart");
            }

            ViewBag.Total = _shoppingCart.GetTotal();
            return View(new OrderCheckoutViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(OrderCheckoutViewModel viewModel)
        {
            if (!_shoppingCart.Items.Any())
            {
                ModelState.AddModelError("", "您的購物車是空的。");
            }

            foreach (var item in _shoppingCart.Items)
            {
                var productInDb = await _context.Products.FindAsync(item.ProductId);
                if (productInDb == null || productInDb.StockQuantity < item.Quantity)
                {
                    ModelState.AddModelError("", $"商品 '{item.ProductName}' 庫存不足或已下架。");
                }
            }

            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var newDbOrder = new Order
                {
                    UserId = currentUser.Id,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = _shoppingCart.GetTotal(),
                    Status = OrderStatus.Pending,
                    ShippingAddress = viewModel.ShippingAddress,
                    OrderItems = new List<OrderItem>()
                };

                foreach (var cartItem in _shoppingCart.Items)
                {
                    var product = await _context.Products.FindAsync(cartItem.ProductId);
                    if (product != null && product.StockQuantity >= cartItem.Quantity)
                    {
                        product.StockQuantity -= cartItem.Quantity;
                        _context.Update(product);

                        newDbOrder.OrderItems.Add(new OrderItem
                        {
                            ProductId = cartItem.ProductId,
                            Quantity = cartItem.Quantity,
                            PriceAtPurchase = cartItem.Price
                        });
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"處理訂單時發現 '{cartItem.ProductName}' 庫存不足。請返回購物車修改。";
                        return RedirectToAction("Index", "Cart");
                    }
                }

                _context.Orders.Add(newDbOrder);
                await _context.SaveChangesAsync();

                _shoppingCart.ClearCart();

                if (currentUser != null && !string.IsNullOrEmpty(currentUser.Email))
                {
                    string subject = $"您的 SimpleShop 訂單 #{newDbOrder.Id} 已確認";
                    string messageBody = $"親愛的 {currentUser.FullName ?? currentUser.UserName}，<br><br>" +
                                         $"感謝您的訂購！您的訂單編號 <strong>{newDbOrder.Id}</strong> 已成功建立。<br>" +
                                         $"訂單總金額為：{newDbOrder.TotalAmount:C}<br>" +
                                         $"我們將在商品準備好出貨時再次通知您。<br><br>" +
                                         $"SimpleShop 團隊";
                    try
                    {
                        await _emailSender.SendEmailAsync(currentUser.Email, subject, messageBody);
                        TempData["SuccessMessage"] = $"訂單已成功建立！確認郵件已發送到 {currentUser.Email}。";
                    }
                    catch (Exception ex)
                    {
                        TempData["SuccessMessage"] = "訂單已成功建立！但發送確認郵件失敗。";
                        TempData["ErrorMessage"] = $"郵件發送錯誤: {ex.Message}";
                    }
                }
                else
                {
                    TempData["SuccessMessage"] = "訂單已成功建立！";
                }

                return RedirectToAction("Confirmation", new { id = newDbOrder.Id });
            }

            // 如果 ModelState 無效，執行到這裡
            ViewBag.Total = _shoppingCart.GetTotal();
            return View(viewModel);
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _context.Orders
                                    .Include(o => o.User)
                                    .Include(o => o.OrderItems)
                                    .ThenInclude(oi => oi.Product)
                                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();
            return View(order);
        }

        public async Task<IActionResult> MyOrders()
        {
            var userId = _userManager.GetUserId(User);
            var orders = await _context.Orders
                                    .Where(o => o.UserId == userId)
                                    .Include(o => o.OrderItems)
                                    .ThenInclude(oi => oi.Product)
                                    .OrderByDescending(o => o.OrderDate)
                                    .ToListAsync();

            return View(orders);
        }
    }
}