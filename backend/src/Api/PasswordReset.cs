using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Brasileirao;

// "Esqueceu sua senha?": the player asks for a link by e-mail, opens it and chooses a new password. The link carries a signed,
// expiring token that is bound to the current password, so it works once (after the change it no longer matches) and nothing
// extra has to be stored in the database. It is available only when an SMTP server and the public address are configured.
public interface IEmailSender
{
    bool Configured { get; }
    Task Send(string to, string subject, string body, CancellationToken cancellation);
}

public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> log) : IEmailSender
{
    public bool Configured => !string.IsNullOrWhiteSpace(config["Smtp:Host"]) && !string.IsNullOrWhiteSpace(config["Smtp:From"]);
    public async Task Send(string to, string subject, string body, CancellationToken cancellation)
    {
        int port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        using var client = new SmtpClient(config["Smtp:Host"], port) { EnableSsl = !string.Equals(config["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase), Timeout = 15_000 };
        if (!string.IsNullOrWhiteSpace(config["Smtp:User"])) client.Credentials = new NetworkCredential(config["Smtp:User"], config["Smtp:Password"]);
        using var message = new MailMessage(new MailAddress(config["Smtp:From"]!, config["Smtp:FromName"] ?? "futbrx"), new MailAddress(to)) { Subject = subject, Body = body, IsBodyHtml = false, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8 };
        try { await client.SendMailAsync(message, cancellation); }
        catch (Exception e) { log.LogError(e, "Falha ao enviar o e-mail de redefinição de senha."); throw; }
    }
}

public static class PasswordResetTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    static string Fingerprint(Account a) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(a.PasswordHash)))[..24];
    static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static byte[] FromB64(string text) { text = text.Replace('-', '+').Replace('_', '/'); return Convert.FromBase64String(text.PadRight(text.Length + (4 - text.Length % 4) % 4, '=')); }
    static string Sign(string payload, string key) => B64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes("pwd-reset:" + payload)));

    public static string Create(Account account, DateTimeOffset now, string key)
    {
        var payload = $"{account.Id:N}|{now.Add(Lifetime).ToUnixTimeSeconds()}|{Fingerprint(account)}";
        return B64(Encoding.UTF8.GetBytes(payload)) + "." + Sign(payload, key);
    }

    // The account id inside a token whose signature and expiry are valid (the fingerprint is checked against the account afterwards).
    public static (Guid UserId, string Fingerprint)? Read(string? token, DateTimeOffset now, string key)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 400 || token.Split('.') is not [var body, var signature]) return null;
        try
        {
            var payload = Encoding.UTF8.GetString(FromB64(body));
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(signature), Encoding.UTF8.GetBytes(Sign(payload, key)))) return null;
            if (payload.Split('|') is not [var id, var expiry, var fingerprint] || !Guid.TryParseExact(id, "N", out var userId) || !long.TryParse(expiry, out var exp)) return null;
            return DateTimeOffset.FromUnixTimeSeconds(exp) <= now ? null : (userId, fingerprint);
        }
        catch (FormatException) { return null; }
    }
    public static bool Matches(Account account, string fingerprint) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Fingerprint(account)), Encoding.UTF8.GetBytes(fingerprint));
}

public sealed record ForgotRequest(string Email);
public sealed record ResetRequest(string Token, string Password);

public static class PasswordResetEndpoints
{
    public static bool Available(IConfiguration config, IEmailSender sender) => sender.Configured && !string.IsNullOrWhiteSpace(config["App:PublicUrl"]);

    public static void MapPasswordReset(this RouteGroupBuilder auth)
    {
        auth.MapPost("/forgot", async (ForgotRequest r, IConfiguration config, IEmailSender sender, GameDb db, IMemoryCache cache, TimeProvider clock, HttpContext ctx) =>
        {
            if (!Available(config, sender)) return Results.Json(new { message = "A recuperação por e-mail ainda não está configurada. Peça a redefinição ao administrador." }, statusCode: 503);
            var email = (r.Email ?? "").Trim().ToLowerInvariant();
            if (!MailAddress.TryCreate(email, out _) || email.Length > 254) return Results.BadRequest(new { message = "Informe um e-mail válido." });
            // The answer is the same whether the e-mail exists or not, so the form cannot be used to find out who has an account.
            var answer = Results.Ok(new { message = "Se existir uma conta com este e-mail, enviamos um link para criar uma nova senha. O link vale por 1 hora." });
            if (cache.TryGetValue("forgot:" + email, out _)) return answer;
            cache.Set("forgot:" + email, true, TimeSpan.FromMinutes(2));
            var account = await db.Accounts.SingleOrDefaultAsync(a => a.Email == email);
            if (account == null || GoogleSignIn.IsGoogleAccount(account)) return answer;
            var link = $"{config["App:PublicUrl"]!.TrimEnd('/')}/redefinir-senha?token={Uri.EscapeDataString(PasswordResetTokens.Create(account, clock.GetUtcNow(), config["Jwt:Key"]!))}";
            db.Audits.Add(new() { UserId = account.Id, Action = "auth:password-reset-requested" }); await db.SaveChangesAsync();
            try
            {
                await sender.Send(account.Email, "futbrx: crie uma nova senha",
                    $"Olá, {account.Name}.\n\nRecebemos um pedido para redefinir a senha da sua conta no futbrx. Para criar uma nova senha, abra o link abaixo (vale por 1 hora):\n\n{link}\n\nSe não foi você, ignore este e-mail: a sua senha continua a mesma.\n", ctx.RequestAborted);
            }
            catch (Exception) { /* logged by the sender; the answer stays the same on purpose */ }
            return answer;
        }).RequireRateLimiting("auth");

        auth.MapPost("/reset", async (ResetRequest r, IConfiguration config, GameDb db, TimeProvider clock) =>
        {
            if (string.IsNullOrEmpty(r.Password) || r.Password.Length is < 6 or > 128) return Results.BadRequest(new { message = "A senha precisa ter de 6 a 128 caracteres." });
            var token = PasswordResetTokens.Read(r.Token, clock.GetUtcNow(), config["Jwt:Key"]!);
            var account = token == null ? null : await db.Accounts.FindAsync(token.Value.UserId);
            if (token == null || account == null || GoogleSignIn.IsGoogleAccount(account) || !PasswordResetTokens.Matches(account, token.Value.Fingerprint))
                return Results.BadRequest(new { message = "Este link expirou ou já foi usado. Peça um novo em “Esqueceu sua senha?”." });
            account.PasswordHash = new PasswordHasher<Account>().HashPassword(account, r.Password);
            db.Sessions.RemoveRange(db.Sessions.Where(s => s.UserId == account.Id)); // every device has to sign in again
            db.Audits.Add(new() { UserId = account.Id, Action = "auth:password-reset" });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Senha alterada. Entre com a nova senha." });
        }).RequireRateLimiting("auth");
    }
}
