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
            // 讀取 appsettings.json 或環境變數中的 SmtpSettings
            _smtpSettings = _configuration.GetSection("SmtpSettings").Get<SmtpSettings>()
                            ?? new SmtpSettings();
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // 檢查必要設定是否存在
            if (string.IsNullOrEmpty(_smtpSettings.Server) ||
                string.IsNullOrEmpty(_smtpSettings.Username) ||
                string.IsNullOrEmpty(_smtpSettings.FromAddress))
            {
                Console.WriteLine("警告：SMTP 設定不完整，無法寄送郵件。");
                return;
            }

            try
            {
                using (var client = new SmtpClient(_smtpSettings.Server, _smtpSettings.Port))
                {
                    client.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                    client.EnableSsl = _smtpSettings.EnableSsl;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_smtpSettings.FromAddress, _smtpSettings.FromName),
                        Subject = subject,
                        Body = htmlMessage,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(email);

                    // 使用 await 確保非同步執行完成
                    await client.SendMailAsync(mailMessage);
                    Console.WriteLine($"成功寄送郵件至: {email}");
                }
            }
            catch (Exception ex)
            {
                // 當寄信失敗時，只在後台日誌紀錄原因，不影響前端註冊流程
                Console.WriteLine("==================================================");
                Console.WriteLine($"[郵件發送失敗] 目的地: {email}");
                Console.WriteLine($"錯誤訊息: {ex.Message}");
                Console.WriteLine("==================================================");

                // 這裡不 return Task.FromException(ex)，而是讓程式繼續走下去
            }
        }
    }

    public class SmtpSettings
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string FromName { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
    }
}