using System.Net.Http.Json;
using System.Text.Json;
using Brasileirao;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Brasileirao.IntegrationTests;

public static class PersistenceApiTests
{
    static async Task<JsonElement> Json(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone(); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    public static async Task Run()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../.runtime")); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "persistence-test-" + Guid.NewGuid().ToString("N") + ".db");
        var content = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Api"));
        WebApplicationFactory<global::Program> Host() => new WebApplicationFactory<global::Program>().WithWebHostBuilder(b => b.UseContentRoot(content).ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:InMemory"] = "false", ["Database:LocalConnection"] = "Data Source=" + path })));
        var oldKey = Environment.GetEnvironmentVariable("Jwt__Key");
        Environment.SetEnvironmentVariable("Jwt__Key", null);
        var oldMemory = Environment.GetEnvironmentVariable("Database__InMemory");
        var oldConnection = Environment.GetEnvironmentVariable("Database__LocalConnection");
        Environment.SetEnvironmentVariable("Database__InMemory", "false");
        Environment.SetEnvironmentVariable("Database__LocalConnection", "Data Source=" + path);
        try {
        var email = "persist-" + Guid.NewGuid().ToString("N") + "@example.test"; string token, id, liveId;
        using (var first = Host())
        using (var client = first.CreateClient(new() { HandleCookies = false }))
        {
            using var scope = first.Services.CreateScope();
            Check(scope.ServiceProvider.GetRequiredService<GameDb>().Database.ProviderName!.Contains("Sqlite"), "Teste usa banco SQLite real em arquivo");
            var auth = await Json(await client.PostAsJsonAsync("/api/auth/register", new { email, password = "Persistence-test-123!", name = "Persistência" }));
            token = auth.GetProperty("token").GetString()!; client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var created = await Json(await client.PostAsJsonAsync("/api/careers", new { name = "Carreira persistente", clubId = "pal" })); id = created.GetProperty("id").GetString()!;
            var live = await Json(await client.PostAsJsonAsync($"/api/careers/{id}/live/start", new { revision = 1 })); liveId = live.GetProperty("id").GetString()!;
            Check(live.GetProperty("incremental").GetBoolean(), "Nova partida começa em motor incremental sem rodada pré-calculada");
            var tick = await Json(await client.PostAsJsonAsync($"/api/careers/{id}/live/tick", new { id = liveId, playing = true }));
            Check(tick.GetProperty("session").GetProperty("simulatedSecond").GetInt32() == 0 && !tick.GetProperty("complete").GetBoolean(), "Entrada das equipes não calcula os lances futuros");
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var saved = await db.Careers.FindAsync(Guid.Parse(id)); var state = Game.Read(saved!);
            state.LiveMatch!.WatchedSeconds = LiveBroadcast.EntranceSeconds + 10;
            state.LiveMatch.WasPlaying = false;
            using (var runtime = new LiveSimulation()) runtime.Advance(state.LiveMatch);
            Game.Write(saved!, state); await db.SaveChangesAsync();

        }
        using (var second = Host())
        using (var client = second.CreateClient(new() { HandleCookies = false }))
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var career = await Json(await client.GetAsync($"/api/careers/{id}"));
            Check(career.GetProperty("name").GetString() == "Carreira persistente" && career.GetProperty("state").GetProperty("liveMatch").GetProperty("id").GetString() == liveId, "Reiniciar API preserva conta, sessão, carreira e partida em andamento");
            var tick = await Json(await client.PostAsJsonAsync($"/api/careers/{id}/live/tick", new { id = liveId, playing = true }));
            Check(tick.GetProperty("session").GetProperty("simulatedSecond").GetInt32() == 120 && tick.GetProperty("replay").GetProperty("frames").EnumerateArray().Last().GetProperty("second").GetInt32() == 120, "Motor reiniciado reconstrói o estado sem depender do cache anterior");
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Persistence-test-123!" });
            Check(login.IsSuccessStatusCode && File.Exists(path), "Credenciais e dados sobrevivem em disco após novo processo de hospedagem");
        }
        } finally {
            Environment.SetEnvironmentVariable("Jwt__Key", oldKey);
            Environment.SetEnvironmentVariable("Database__InMemory", oldMemory);
            Environment.SetEnvironmentVariable("Database__LocalConnection", oldConnection);
        }

    }
}
