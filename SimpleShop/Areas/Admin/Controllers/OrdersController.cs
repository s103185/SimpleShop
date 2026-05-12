using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpleShop.Data;
using SimpleShop.Models;
using SimpleShop.Models.Enums;
using SimpleShop.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services; // <--- 這是關鍵的 using 語句

namespace SimpleShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrdersController : Controller
    {
        // 店長出貨必備的兩大法寶：
        private readonly ApplicationDbContext _context; // 法寶一：看全店訂單的監視器 (資料庫)
        private readonly IEmailSender _emailSender;     // 法寶二：幫忙寄出出貨通知的郵差

        // 老闆上班報到，領取裝備
        public OrdersController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                                    .Include(o => o.User)
                                    .OrderByDescending(o => o.OrderDate)
                                    .ToListAsync();
            return View(orders);
        }
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShipOrder(int id, string trackingNumber)
        {
            // 先把這張單找出來
            var order = await _context.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            // 【防呆機制】：只有狀態是「等待中 (Pending)」或「處理中 (Processing)」的單可以出貨
            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Processing)
            {
                // 步驟 1：修改訂單狀態
                order.Status = OrderStatus.Shipped; // 狀態改成：已出貨！
                order.ShippedDate = DateTime.UtcNow; // 記錄按下出貨按鈕的確切時間
                order.TrackingNumber = trackingNumber; // 把黑貓單號抄進資料庫裡

                // 叫資料庫存檔
                _context.Update(order);
                await _context.SaveChangesAsync();

                // 步驟 2：寄送郵件通知
                if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
                {
                    string subject = $"您的訂單 #{order.Id} 已出貨";

                    string message = $"親愛的顧客，<br><br>" +
                                     $"您的訂單編號 {order.Id} 已於 {order.ShippedDate:yyyy-MM-dd HH:mm} 出貨。<br>" +
                                     $"{(string.IsNullOrWhiteSpace(trackingNumber) ? "您的包裹正在路上。" : $"您的貨運追蹤號碼是：{trackingNumber}")}<br><br>" +
                                     $"感謝您的訂購！<br><br>" +
                                     $"SimpleShop 團隊";

                    await _emailSender.SendEmailAsync(order.User.Email, subject, message); // 寄出！
                }
                // 在店長的畫面上貼一張綠色的成功紙條
                TempData["SuccessMessage"] = $"訂單 #{order.Id} 已標記為已出貨。";
            }
            else
            {
                // 如果店長亂按（例如對已經出貨的訂單按出貨），貼一張紅色的警告紙條
                TempData["ErrorMessage"] = $"訂單 #{order.Id} 的狀態為 {order.Status}，無法設定為已出貨。";
            }
            
            return RedirectToAction(nameof(Details), new { id = id });
        }
    }
}