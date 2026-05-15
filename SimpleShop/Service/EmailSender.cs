using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SimpleShop.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly SmtpSettings _smtpSettings;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
            // 讀取設定檔，若讀不到則建立空物件避免報錯
            _smtpSettings = _configuration.GetSection("SmtpSettings").Get<SmtpSettings>()
                            ?? new SmtpSettings();
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // 基礎檢查：如果連伺服器位址都沒設定，直接在日誌報錯並結束
            if (string.IsNullOrEmpty(_smtpSettings.Server) || string.IsNullOrEmpty(_smtpSettings.Username))
            {
                Console.WriteLine("【錯誤】SMTP 設定不完整 (Server 或 Username 為空)，請檢查 Render 環境變數。");
                return;
            }

            try
            {
                using (var client = new SmtpClient(_smtpSettings.Server, _smtpSettings.Port))
                {
                    // --- 雲端環境關鍵設定 ---
                    client.UseDefaultCredentials = false; // 必須設為 false，否則 Credentials 會失效
                    client.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                    client.EnableSsl = _smtpSettings.EnableSsl;
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 20000; // 設定 20 秒超時，預防雲端網路延遲

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_smtpSettings.FromAddress, _smtpSettings.FromName),
                        Subject = subject,
                        Body = htmlMessage,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(email);

                    // 執行發送
                    await client.SendMailAsync(mailMessage);
                    Console.WriteLine($"【成功】郵件已送達: {email}");
                }
            }
            catch (Exception ex)
            {
                // 攔截所有錯誤，確保註冊流程不中斷
                Console.WriteLine("==================================================");
                Console.WriteLine($"【郵件發送失敗】");
                Console.WriteLine($"目的地: {email}");
                Console.WriteLine($"錯誤訊息: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"詳細原因: {ex.InnerException.Message}");
                }
                Console.WriteLine("==================================================");

                // 即使失敗也直接 return，不拋出 Exception，讓註冊網頁順利跳轉
            }
        }
    }

    public class SmtpSettings
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string FromName { get; set; } = "SimpleShop 管理員";
        public string FromAddress { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
    }
}