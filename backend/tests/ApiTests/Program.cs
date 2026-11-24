using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Brasileirao;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Brasileirao.IntegrationTests;

public static class ApiIntegrationTests
{
    public static async Task Main(string[] args)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Database__InMemory", "true");
        Environment.SetEnvironmentVariable("RateLimit__GamePerMinute", "1000");
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-only-32-characters-key");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "brasileirao-manager");
        Environment.SetEnvironmentVariable("Jwt__Audience", "brasileirao-web");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
        if (args.Contains("--persistence")) { await PersistenceApiTests.Run(); return; }
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Api"))));
        BroadcastApiTests.Factory = factory;
        using var admin = factory.CreateClient(new() { HandleCookies = false });
        using var other = factory.CreateClient(new() { HandleCookies = false });
        async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
        void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        var suffix = Guid.NewGuid().ToString("N");
        var register = await admin.PostAsJsonAsync("/api/auth/register", new { email = "admin-" + suffix + "@example.test", password = "Test-password-123!", name = "Administrador de teste" });
        Check(register.IsSuccessStatusCode, "Cadastro para testes");
        var identity = await Json(register);
        admin.DefaultRequestHeaders.Authorization = new("Bearer", identity.GetProperty("token").GetString());
        var adminId = identity.GetProperty("user").GetProperty("id").GetString()!;
        var otherRegistration = await other.PostAsJsonAsync("/api/auth/register", new { email = "other-" + suffix + "@example.test", password = "Test-password-123!", name = "Treinador" });
        var otherIdentity = await Json(otherRegistration);
        other.DefaultRequestHeaders.Authorization = new("Bearer", otherIdentity.GetProperty("token").GetString());
        Check((await admin.GetAsync("/api/admin/catalogs/")).StatusCode == HttpStatusCode.Forbidden, "Cadastro não concede acesso administrativo");
        factory.Services.GetRequiredService<IConfiguration>()["Admin:UserIds"] = adminId;
        Check((await admin.GetAsync("/api/admin/catalogs/")).IsSuccessStatusCode, "Administrador precisa de ID autorizado pelo operador");
        Check((await other.GetAsync("/api/admin/catalogs/")).StatusCode == HttpStatusCode.Forbidden, "Outra conta não acessa o editor");
        var list = await Json(await admin.GetAsync("/api/catalogs"));
        var seedId = list.EnumerateArray().First(r => r.GetProperty("version").GetString() == "fictional-2026-v1").GetProperty("id").GetString();
        var official = list.EnumerateArray().Single(r => r.GetProperty("version").GetString() == Game.Clubs2026Version);
        var officialClubs = await Json(await admin.GetAsync("/api/clubs?releaseId=" + official.GetProperty("id").GetString()));
        Check(officialClubs.GetArrayLength() == 20 && officialClubs.EnumerateArray().Any(c => c.GetProperty("id").GetString() == "rem") && officialClubs.EnumerateArray().Any(c => c.GetProperty("id").GetString() == "cha"), "Catálogo público oferece os vinte clubes da Série A 2026");
        var oldResponse = await Json(await admin.PostAsJsonAsync("/api/careers/", new { name = "Antes da publicação", clubId = "pal", releaseId = seedId }));
        var oldId = oldResponse.GetProperty("id").GetString();
        var old = await Json(await admin.GetAsync("/api/careers/" + oldId));
        var originalName = old.GetProperty("state").GetProperty("squad")[0].GetProperty("name").GetString();
        var data = Game.DemoCatalog(); data.Players[0].Player.Name = "Jogador do novo catálogo";
        var request = new ImportRequest("json", JsonSerializer.Serialize(data, Game.Json), "integration-" + suffix, 2027, "Fonte sintética de teste", "Dados gerados para teste");
        var draftResponse = await admin.PostAsJsonAsync("/api/admin/catalogs/", request);
        Check(draftResponse.IsSuccessStatusCode, "Importação JSON cria rascunho");
        var draft = await Json(draftResponse); var id = draft.GetProperty("id").GetString();
        Check((await Json(await other.GetAsync("/api/catalogs"))).GetArrayLength() == list.GetArrayLength(), "Rascunhos não aparecem na seleção pública");
        var bad = request with { Content = "{broken" };
        Check((await admin.PostAsJsonAsync("/api/admin/catalogs/validate", bad)).StatusCode == HttpStatusCode.BadRequest, "JSON inválido é rejeitado sem salvar");
        data.Players[0].Player.Rating = 89;
        var edit = request with { Content = JsonSerializer.Serialize(data, Game.Json), Revision = 1 };
        Check((await admin.PostAsJsonAsync($"/api/admin/catalogs/{id}/edit", edit)).IsSuccessStatusCode, "Edição de rascunho valida e salva revisão");
        Check((await admin.PostAsJsonAsync($"/api/admin/catalogs/{id}/edit", edit)).StatusCode == HttpStatusCode.Conflict, "Edição concorrente é bloqueada");
        Check((await admin.PostAsJsonAsync($"/api/admin/catalogs/{id}/publish", new { revision = 1 })).StatusCode == HttpStatusCode.Conflict, "Publicação de revisão antiga é bloqueada");
        Check((await admin.PostAsJsonAsync($"/api/admin/catalogs/{id}/publish", new { revision = 2 })).IsSuccessStatusCode, "Publicação validada");
        Check((await admin.PostAsJsonAsync($"/api/admin/catalogs/{id}/edit", edit with { Revision = 3 })).StatusCode == HttpStatusCode.Conflict, "Versões publicadas são imutáveis");
        old = await Json(await admin.GetAsync("/api/careers/" + oldId));
        Check(old.GetProperty("state").GetProperty("dataVersion").GetString() == "fictional-2026-v1" && old.GetProperty("state").GetProperty("squad")[0].GetProperty("name").GetString() == originalName, "Publicação preserva versão e jogadores de carreira existente");
        var newer = await Json(await admin.PostAsJsonAsync("/api/careers/", new { name = "Catálogo atualizado", clubId = "pal", releaseId = id }));
        var careerId = newer.GetProperty("id").GetString();
        var career = await Json(await admin.GetAsync("/api/careers/" + careerId));
        Check(career.GetProperty("state").GetProperty("dataVersion").GetString() == request.Version && career.GetProperty("state").GetProperty("season").GetInt32() == 2027 && career.GetProperty("state").GetProperty("squad")[0].GetProperty("rating").GetInt32() == 89, "Nova carreira usa a versão escolhida");
        Check((await other.GetAsync("/api/careers/" + careerId)).StatusCode == HttpStatusCode.NotFound, "Saves continuam isolados entre contas");
        Check((await admin.PostAsJsonAsync($"/api/careers/{careerId}/actions", new { type = "tactics", value = "3-5-2", extra = "Defensiva", revision = 1 })).IsSuccessStatusCode, "Tática salva antes da partida");
        await BroadcastApiTests.Run(admin, other, careerId!, 2);
        career = await Json(await admin.GetAsync("/api/careers/" + careerId));
        var fixture = career.GetProperty("state").GetProperty("fixtures").EnumerateArray().First(f => f.GetProperty("round").GetInt32() == 1 && (f.GetProperty("home").GetString() == "pal" || f.GetProperty("away").GetString() == "pal"));
        var matchHome = fixture.GetProperty("home").GetString();
        var managedSide = matchHome == "pal" ? "home" : "away";
        var path = $"/api/careers/{careerId}/matches/1/{matchHome}/replay";
        var replay = await Json(await admin.GetAsync(path));
        Check(replay.GetProperty("frames").GetArrayLength() >= 901 && replay.GetProperty("setup").GetProperty(managedSide).GetProperty("formation").GetString() == "3-5-2", "Replay fornece frames e formação persistida");
        Check((await other.GetAsync(path)).StatusCode == HttpStatusCode.NotFound, "Replay de outra conta não pode ser lido");
        Check((await admin.PostAsJsonAsync($"/api/careers/{careerId}/actions", new { type = "tactics", value = "4-4-2", extra = "Ofensiva", revision = career.GetProperty("revision").GetInt32() })).IsSuccessStatusCode, "Tática pode mudar para a próxima partida");
        Check((await (await admin.GetAsync(path)).Content.ReadAsStringAsync()) == replay.GetRawText(), "Mudar a tática não altera a reprodução de partida já disputada");
        var csvRequest = request with { Format = "csv", Content = CatalogImport.ToCsv(data), Version = "csv-" + suffix };
        var csvValidation=await admin.PostAsJsonAsync("/api/admin/catalogs/validate", csvRequest);
        Check(csvValidation.IsSuccessStatusCode, "CSV validado pela API: " + (int)csvValidation.StatusCode + " " + await csvValidation.Content.ReadAsStringAsync());
        await ManagementApiTests.Run(admin, other, careerId!);
        await SportsApiTests.Run(admin, other, careerId!);
        await TrainingApiTests.Run(factory, other);
        await YouthApiTests.Run(factory, other);
        await AdvancedApiTests.Run(factory, other);
        await ClubLifeApiTests.Run(admin,other);
        await CoachApiTests.Run(admin,other);
        await SeasonApiTests.Run(other);
        await IntelligenceApiTests.Run(admin,other,careerId!);
        await AdminPanelApiTests.Run(factory, admin, other, "admin-" + suffix + "@example.test", adminId);
        await GoogleApiTests.Run(factory);
        await PasswordResetApiTests.Run(factory);
        var sessions = await Json(await admin.GetAsync("/api/auth/sessions/"));
        Check(sessions.GetArrayLength() == 1 && sessions[0].GetProperty("current").GetBoolean(), "Lista de sessões não revela sessões alheias");
        var current = sessions[0].GetProperty("id").GetString();
        Check((await other.PostAsJsonAsync($"/api/auth/sessions/{current}/revoke", new { })).StatusCode == HttpStatusCode.NotFound, "Outra conta não revoga sua sessão");
        factory.Services.GetRequiredService<IConfiguration>()["Admin:UserIds"] = "";
        Check((await admin.GetAsync("/api/admin/catalogs/")).StatusCode == HttpStatusCode.Forbidden, "Remover a autorização administrativa produz efeito imediato");
        Check((await admin.PostAsJsonAsync($"/api/auth/sessions/{current}/revoke", new { })).IsSuccessStatusCode, "Revogação da própria sessão");
        Check((await admin.GetAsync("/api/auth/me")).StatusCode == HttpStatusCode.Unauthorized, "Revogação invalida JWT imediatamente");
        await PersistenceApiTests.Run();
        Console.WriteLine("Integração de catálogos, snapshots, replay e sessões passou.");

    }
}
