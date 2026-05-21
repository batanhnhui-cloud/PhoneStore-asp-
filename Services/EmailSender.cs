using MimeKit;

public interface IEmailSender { Task SendEmailAsync(string email, string subject, string message); }

public class EmailSender : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string message)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("SunMobile Support", "email_cua_ban@gmail.com"));
        msg.To.Add(new MailboxAddress("", email));
        msg.Subject = subject;
        msg.Body = new TextPart("html") { Text = message };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, false);
        await client.AuthenticateAsync("email_cua_ban@gmail.com", "MÃ_APP_PASSWORD_16_KÝ_TỰ");
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
    }
}