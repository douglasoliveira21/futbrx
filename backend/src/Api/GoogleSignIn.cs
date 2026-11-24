using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Brasileirao;

// Sign in and sign up with a Google account. The browser gets an ID token from Google Identity Services and the server
// validates it with Google (audience, issuer, expiry, verified e-mail) before it opens a normal session. It is enabled only
// when Google:ClientId is configured (OAuth client of type "Web application" with the site origin as authorized origin).
public sealed record GoogleIdentity(string Email, string Name, bool EmailVerified);

public interface IGoogleTokenVerifier
{
    Task<GoogleIdentity?> Verify(string credential, string clientId, CancellationToken cancellation);
}

public sealed class GoogleTokenVerifier(HttpClient http) : IGoogleTokenVerifier
{
    sealed record TokenInfo(
        [property: JsonPropertyName("aud")] string? Audience,
        [property: JsonPropertyName("iss")] string? Issuer,
        [property: JsonPropertyName("exp")] string? Expires,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] string? EmailVerified,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("given_name")] string? GivenName);

    public async Task<GoogleIdentity?> Verify(string credential, string clientId, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(credential) || credential.Length > 4096) return null;
        using var response = await http.GetAsync("https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(credential), cancellation);
        if (!response.IsSuccessStatusCode) return null;
        var info = await response.Content.ReadFromJsonAsync<TokenInfo>(cancellationToken: cancellation);
        if (info == null || info.Audience != clientId || info.Issuer is not ("accounts.google.com" or "https://accounts.google.com")) return null;
        if (!long.TryParse(info.Expires, out var exp) || DateTimeOffset.FromUnixTimeSeconds(exp) <= DateTimeOffset.UtcNow) return null;
        if (string.IsNullOrWhiteSpace(info.Email)) return null;
        return new GoogleIdentity(info.Email, info.Name ?? info.GivenName ?? "", string.Equals(info.EmailVerified, "true", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record GoogleLogin(string Credential, bool Remember = true);

public static class GoogleSignIn
{
    // Accounts created through Google have no password; this marker keeps the password login from ever matching them.
    public const string PasswordMarker = "google:";
    public static bool IsGoogleAccount(Account a) => a.PasswordHash.StartsWith(PasswordMarker, StringComparison.Ordinal);

    public static void MapGoogleSignIn(this RouteGroupBuilder auth)
    {
        // Public configuration of the login screen: the Client ID is not a secret, and no button is shown without it.
        auth.MapGet("/config", (IConfiguration config) => Results.Ok(new { googleClientId = string.IsNullOrWhiteSpace(config["Google:ClientId"]) ? null : config["Google:ClientId"], contactEmail = string.IsNullOrWhiteSpace(config["Legal:ContactEmail"]) ? null : config["Legal:ContactEmail"] }));
        auth.MapPost("/google", async (GoogleLogin r, IConfiguration config, IGoogleTokenVerifier verifier, GameDb db, AuthSession sessions, HttpContext ctx) =>
        {
            var clientId = config["Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId)) return Results.NotFound(new { message = "Entrar com Google não está configurado." });
            GoogleIdentity? identity;
            try { identity = await verifier.Verify(r.Credential, clientId, ctx.RequestAborted); }
            catch (HttpRequestException) { return Results.Json(new { message = "Não foi possível falar com o Google agora. Tente de novo em instantes." }, statusCode: 503); }
            if (identity == null) return Results.Json(new { message = "Não foi possível validar o login com o Google." }, statusCode: 401);
            if (!identity.EmailVerified) return Results.Json(new { message = "O e-mail da conta Google não está verificado." }, statusCode: 401);
            var email = identity.Email.Trim().ToLowerInvariant();
            if (!System.Net.Mail.MailAddress.TryCreate(email, out _) || email.Length > 254) return Results.Json(new { message = "O Google devolveu um e-mail inválido." }, statusCode: 401);
            var account = await db.Accounts.SingleOrDefaultAsync(a => a.Email == email);
            if (account != null && !IsGoogleAccount(account))
                // A password account of the same e-mail is never linked automatically: nobody verified that e-mail when it was registered.
                return Results.Conflict(new { message = "Já existe uma conta com este e-mail. Entre com a senha." });
            if (account == null)
            {
                var name = string.IsNullOrWhiteSpace(identity.Name) ? email.Split('@')[0] : identity.Name.Trim();
                account = new Account { Email = email, Name = name.Length > 60 ? name[..60] : name };
                account.PasswordHash = PasswordMarker + Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                db.Add(account); db.Audits.Add(new() { UserId = account.Id, Action = "auth:google-register" });
                try { return await sessions.Issue(account, ctx, null, r.Remember); }
                catch (DbUpdateException) { return Results.Conflict(new { message = "Não foi possível entrar com este e-mail." }); }
            }
            db.Audits.Add(new() { UserId = account.Id, Action = "auth:google-login" });
            return await sessions.Issue(account, ctx, null, r.Remember);
        }).RequireRateLimiting("auth");
    }
}
