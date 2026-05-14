using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace SimpleShop.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailSender(IConfiguration configuration)
        {
            // 讀取您在 Render 設定的環境變數
            _smtpSettings = configuration.GetSection("SmtpSettings").Get<SmtpSettings>() 
                            ?? new SmtpSettings();
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // 1. 準備信件內容 (使用 MimeKit)
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpSettings.FromName, _smtpSettings.FromAddress));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
            message.Body = bodyBuilder.ToMessageBody();

            // 2. 執行寄信動作 (使用 MailKit)
            using (var client = new SmtpClient())
            {
                try
                {
                    // 在 Render Logs 顯示進度 (Debug 改成 Console 雲端才看得到)
                    Console.WriteLine($"[Email] 準備連線至 {_smtpSettings.Server}:{_smtpSettings.Port}...");

                    // 關鍵：使用 StartTls 配合 Port 587
                    await client.ConnectAsync(_smtpSettings.Server, _smtpSettings.Port, SecureSocketOptions.StartTls);

                    // 使用您的 16 碼應用程式密碼進行驗證
                    Console.WriteLine($"[Email] 正在驗證帳號: {_smtpSettings.Username}");
                    await client.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password);

                    // 發送信件
                    await client.SendAsync(message);
                    
                    Console.WriteLine("[Email] 信件已成功寄出！");
                }
                catch (Exception ex)
                {
                    // 捕捉錯誤並顯示在 Render Logs 中
                    Console.WriteLine($"[Email] 錯誤：{ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"[Email] 詳細原因：{ex.InnerException.Message}");
                    
                    throw; // 讓系統知道發信失敗
                }
                finally
                {
                    // 中斷連線並釋放資源
                    await client.DisconnectAsync(true);
                }
            }
        }
    }

    // 您的 SmtpSettings 類別保持不變
}