using Microsoft.AspNetCore.Identity.UI.Services;

namespace Monexia.Helpers
{
    public class DummyEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Gerçek e-posta gönderimi yapılmaz, log'a yazabiliriz
            Console.WriteLine($"Fake Email Sent To: {email} | Subject: {subject}");
            return Task.CompletedTask;
        }
    }
}
