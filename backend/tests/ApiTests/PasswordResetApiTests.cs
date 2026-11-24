using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Brasileirao.IntegrationTests;

// "Esqueceu sua senha?": link by e-mail with a signed, expiring, single-use token; the e-mail sender is replaced by a fake.
public static class PasswordResetApiTests
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static async Task<JsonElement> Json(HttpResponseMessage r) { var body = await r.Content.ReadAsStringAsync(); return JsonDocument.Parse(body).RootElement.Clone(); }

    sealed class FakeMail : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];
        public bool Configured => true;
        public Task Send(string to, string subject, string body, CancellationToken cancellation) { Sent.Add((to, subject, body)); return Task.CompletedTask; }
    }
    sealed class OffMail : IEmailSender
    {
        public bool Configured => false;
        public Task Send(string to, string subject, string body, CancellationToken cancellation) => throw new InvalidOperationException();
    }
    static string TokenOf(string body)
    {
        var line = body.Split('\n').First(l => l.Contains("/redefinir-senha?token="));
        return Uri.UnescapeDataString(line[(line.IndexOf("token=", StringComparison.Ordinal) + 6)..].Trim());
    }

    public static async Task Run(WebApplicationFactory<Program> factory)
    {
        // Without an SMTP server the feature is off and says so.
        using var off = factory.WithWebHostBuilder(b => { b.UseSetting("App:PublicUrl", "https://jogo.example.test"); b.ConfigureServices(s => s.AddSingleton<IEmailSender>(new OffMail())); });
        using var offClient = off.CreateClient(new() { HandleCookies = false });
        Check(!(await Json(await offClient.GetAsync("/api/auth/config"))).GetProperty("passwordReset").GetBoolean() && (await offClient.PostAsJsonAsync("/api/auth/forgot", new { email = "a@example.test" })).StatusCode == HttpStatusCode.ServiceUnavailable, "Sem servidor de e-mail a recuperação de senha fica desligada e avisa");

        var mail = new FakeMail();
        using var on = factory.WithWebHostBuilder(b => { b.UseSetting("App:PublicUrl", "https://jogo.example.test/"); b.ConfigureServices(s => s.AddSingleton<IEmailSender>(mail)); });
        using var client = on.CreateClient(new() { HandleCookies = false });
        Check((await Json(await client.GetAsync("/api/auth/config"))).GetProperty("passwordReset").GetBoolean(), "Com e-mail e endereço públicos configurados a tela de login oferece a recuperação");

        string email = "esqueci-" + Guid.NewGuid().ToString("N") + "@example.test", oldPassword = "Senha-antiga-12345", newPassword = "Senha-nova-segura-99";
        var registered = await Json(await client.PostAsJsonAsync("/api/auth/register", new { email, password = oldPassword, name = "Esquecido" }));
        var oldToken = registered.GetProperty("token").GetString();
        Check((await client.PostAsJsonAsync("/api/auth/forgot", new { email = "nao-e-email" })).StatusCode == HttpStatusCode.BadRequest, "E-mail inválido é recusado");
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot", new { email = "ninguem-" + Guid.NewGuid().ToString("N") + "@example.test" });
        var known = await client.PostAsJsonAsync("/api/auth/forgot", new { email });
        Check(unknown.IsSuccessStatusCode && known.IsSuccessStatusCode && (await unknown.Content.ReadAsStringAsync()) == (await known.Content.ReadAsStringAsync()), "A resposta é a mesma para e-mail cadastrado ou não, sem revelar quem tem conta");
        Check(mail.Sent.Count == 1 && mail.Sent[0].To == email && mail.Sent[0].Body.Contains("https://jogo.example.test/redefinir-senha?token="), "Só o e-mail cadastrado recebe o link, montado com o endereço público");
        await client.PostAsJsonAsync("/api/auth/forgot", new { email });
        Check(mail.Sent.Count == 1, "Pedidos repetidos em seguida não geram outro e-mail");

        var token = TokenOf(mail.Sent[0].Body);
        Check((await client.PostAsJsonAsync("/api/auth/reset", new { token, password = "curta" })).StatusCode == HttpStatusCode.BadRequest, "Senha curta é recusada");
        Check((await client.PostAsJsonAsync("/api/auth/reset", new { token = token + "x", password = newPassword })).StatusCode == HttpStatusCode.BadRequest && (await client.PostAsJsonAsync("/api/auth/reset", new { token = "lixo", password = newPassword })).StatusCode == HttpStatusCode.BadRequest, "Token adulterado ou inventado é recusado");
        Check((await client.PostAsJsonAsync("/api/auth/reset", new { token, password = newPassword })).IsSuccessStatusCode, "O link permite criar uma nova senha");
        Check((await client.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword })).IsSuccessStatusCode && (await client.PostAsJsonAsync("/api/auth/login", new { email, password = oldPassword })).StatusCode == HttpStatusCode.Unauthorized, "Entra com a senha nova e a antiga deixa de valer");
        Check((await client.PostAsJsonAsync("/api/auth/reset", new { token, password = "Outra-senha-ainda-mais-1" })).StatusCode == HttpStatusCode.BadRequest, "O mesmo link não pode ser usado duas vezes");
        using var stale = on.CreateClient(new() { HandleCookies = false }); stale.DefaultRequestHeaders.Authorization = new("Bearer", oldToken);
        Check((await stale.GetAsync("/api/auth/me")).StatusCode == HttpStatusCode.Unauthorized, "As sessões abertas antes da troca são encerradas");

        // Expiry and the other token rules, directly on the token.
        var config = on.Services.GetRequiredService<IConfiguration>(); var key = config["Jwt:Key"]!; var now = DateTimeOffset.UtcNow;
        var account = new Account { Email = email, PasswordHash = "hash-de-teste" };
        var fresh = PasswordResetTokens.Create(account, now, key);
        Check(PasswordResetTokens.Read(fresh, now.AddMinutes(59), key) is { } ok && ok.UserId == account.Id && PasswordResetTokens.Matches(account, ok.Fingerprint), "Token vale por uma hora");
        Check(PasswordResetTokens.Read(fresh, now.AddMinutes(61), key) == null && PasswordResetTokens.Read(fresh, now, key + "outra") == null, "Token expira em uma hora e não vale com outra chave de assinatura");
        account.PasswordHash = "outro-hash";
        Check(!PasswordResetTokens.Matches(account, PasswordResetTokens.Read(fresh, now, key)!.Value.Fingerprint), "Trocar a senha invalida os links antigos");

        // A Google account has no password to reset: no mail is sent.
        var before = mail.Sent.Count;
        using var google = on.WithWebHostBuilder(b => { b.UseSetting("Google:ClientId", "test-client-id"); b.ConfigureServices(s => { s.AddSingleton<IEmailSender>(mail); s.AddSingleton<IGoogleTokenVerifier>(new GoogleOnly()); }); });
        using var googleClient = google.CreateClient(new() { HandleCookies = false });
        await googleClient.PostAsJsonAsync("/api/auth/google", new { credential = "ok" });
        await googleClient.PostAsJsonAsync("/api/auth/forgot", new { email = "so-google@example.test" });
        Check(mail.Sent.Count == before, "Conta criada pelo Google não recebe e-mail de redefinição de senha");
    }
    sealed class GoogleOnly : IGoogleTokenVerifier
    {
        public Task<GoogleIdentity?> Verify(string credential, string clientId, CancellationToken cancellation) => Task.FromResult<GoogleIdentity?>(new("so-google@example.test", "Só Google", true));
    }
}
