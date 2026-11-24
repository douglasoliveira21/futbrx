using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
namespace Brasileirao.IntegrationTests;

// Sign in and sign up with Google, with the Google token check replaced by a fake that knows a few credentials.
public static class GoogleApiTests
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static async Task<JsonElement> Json(HttpResponseMessage r) { var body = await r.Content.ReadAsStringAsync(); if (!r.IsSuccessStatusCode) throw new Exception(r.StatusCode + ": " + body); return JsonDocument.Parse(body).RootElement.Clone(); }

    sealed class FakeGoogle(string takenEmail) : IGoogleTokenVerifier
    {
        public Task<GoogleIdentity?> Verify(string credential, string clientId, CancellationToken cancellation) =>
            Task.FromResult<GoogleIdentity?>(clientId != "test-client-id" ? null : credential switch
            {
                "novo" => new("Nova.Pessoa@Example.test", "Nova Pessoa", true),
                "sem-verificar" => new("nao-verificado@example.test", "Sem Verificação", false),
                "ocupado" => new(takenEmail, "Dono do e-mail", true),
                _ => null
            });
    }

    public static async Task Run(WebApplicationFactory<Program> factory)
    {
        // Without the client ID the feature is off: no button and no endpoint.
        using var plain = factory.CreateClient(new() { HandleCookies = false });
        var off = await Json(await plain.GetAsync("/api/auth/config"));
        Check(off.GetProperty("googleClientId").ValueKind == JsonValueKind.Null && (await plain.PostAsJsonAsync("/api/auth/google", new { credential = "novo" })).StatusCode == HttpStatusCode.NotFound, "Sem Client ID configurado não há entrar com Google");

        string taken = "dono-" + Guid.NewGuid().ToString("N") + "@example.test";
        using var enabled = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Google:ClientId", "test-client-id");
            b.ConfigureServices(s => s.AddSingleton<IGoogleTokenVerifier>(new FakeGoogle(taken)));
        });
        using var client = enabled.CreateClient(new() { HandleCookies = false });
        var on = await Json(await client.GetAsync("/api/auth/config"));
        Check(on.GetProperty("googleClientId").GetString() == "test-client-id", "Tela de login recebe o Client ID para mostrar o botão");
        Check((await client.PostAsJsonAsync("/api/auth/google", new { credential = "invalido" })).StatusCode == HttpStatusCode.Unauthorized, "Credencial que o Google não valida é recusada");
        Check((await client.PostAsJsonAsync("/api/auth/google", new { credential = "sem-verificar" })).StatusCode == HttpStatusCode.Unauthorized, "E-mail não verificado no Google é recusado");

        var first = await client.PostAsJsonAsync("/api/auth/google", new { credential = "novo" });
        var created = await Json(first);
        string id = created.GetProperty("user").GetProperty("id").GetString()!;
        Check(created.GetProperty("user").GetProperty("email").GetString() == "nova.pessoa@example.test" && created.GetProperty("user").GetProperty("name").GetString() == "Nova Pessoa" && created.GetProperty("token").GetString()!.Length > 20, "Primeiro acesso com Google cria a conta com o nome e o e-mail do Google");
        var again = await Json(await client.PostAsJsonAsync("/api/auth/google", new { credential = "novo" }));
        Check(again.GetProperty("user").GetProperty("id").GetString() == id, "Segundo acesso com Google entra na mesma conta");
        var forgotten = await client.PostAsJsonAsync("/api/auth/google", new { credential = "novo", remember = false });
        string Cookie(HttpResponseMessage r) => r.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("bm_refresh="));
        Check(Cookie(first).Contains("max-age", StringComparison.OrdinalIgnoreCase) && !Cookie(forgotten).Contains("max-age", StringComparison.OrdinalIgnoreCase), "Lembrar de mim também vale para o login com Google");
        Check((await client.PostAsJsonAsync("/api/auth/login", new { email = "nova.pessoa@example.test", password = "qualquer-coisa-123" })).StatusCode == HttpStatusCode.Unauthorized, "Conta criada pelo Google não aceita login por senha");

        // A password account with the same e-mail is never taken over by a Google sign-in.
        Check((await client.PostAsJsonAsync("/api/auth/register", new { email = taken, password = "Senha-bem-longa-123", name = "Dono" })).IsSuccessStatusCode, "Conta por senha criada");
        Check((await client.PostAsJsonAsync("/api/auth/google", new { credential = "ocupado" })).StatusCode == HttpStatusCode.Conflict, "E-mail já cadastrado por senha não é vinculado automaticamente ao Google");
        Check((await client.PostAsJsonAsync("/api/auth/login", new { email = taken, password = "Senha-bem-longa-123" })).IsSuccessStatusCode, "A conta por senha continua entrando normalmente");
    }
}
