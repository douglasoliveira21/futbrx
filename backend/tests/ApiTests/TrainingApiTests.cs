using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Brasileirao.IntegrationTests;

public static class TrainingApiTests
{
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static async Task<JsonElement> Json(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone(); }
    public static async Task Run(WebApplicationFactory<global::Program> factory, HttpClient other)
    {
        using var owner = factory.CreateClient(new() { HandleCookies = false }); using var anonymous = factory.CreateClient(new() { HandleCookies = false });
        var identity = await Json(await owner.PostAsJsonAsync("/api/auth/register", new { email = $"training-{Guid.NewGuid():N}@example.test", password = "Training-password-123!", name = "Treinador de teste" }));
        owner.DefaultRequestHeaders.Authorization = new("Bearer", identity.GetProperty("token").GetString());
        var created = await Json(await owner.PostAsJsonAsync("/api/careers/", new { name = "Treinos", clubId = "pal" })); var id = created.GetProperty("id").GetString(); var path = $"/api/careers/{id}/training";
        var data = await Json(await owner.GetAsync(path + "/")); int rev = data.GetProperty("revision").GetInt32(); var player = data.GetProperty("players")[0]; int playerId = player.GetProperty("player").GetProperty("id").GetInt32();
        Check(data.GetProperty("plan").GetProperty("days").GetArrayLength() == 6 && data.GetProperty("reports").GetArrayLength() == 0, "Treinos carregam planejamento e não geram relatório na leitura");
        Check((await anonymous.GetAsync(path + "/")).StatusCode == HttpStatusCode.Unauthorized, "Treino exige autenticação");
        Check((await other.GetAsync(path + "/")).StatusCode == HttpStatusCode.NotFound, "Treinos de outra conta não são expostos");
        Check((await other.PostAsJsonAsync(path + "/plan", new { revision = rev, plan = new { days = new[] { "Passe", "Passe", "Passe", "Passe", "Passe", "Descanso" }, intensity = "Alta" } })).StatusCode == HttpStatusCode.NotFound, "Outra conta não altera a carga de treino");
        Check((await owner.PostAsJsonAsync(path + "/plan", new { revision = rev, plan = (object?)null })).StatusCode == HttpStatusCode.BadRequest, "Plano nulo retorna erro de validação");
        Check((await owner.PostAsJsonAsync(path + "/plan", new { revision = rev, plan = new { days = new[] { "Passe", "Passe", "Passe", "Passe", "Passe", "Passe" }, intensity = "Alta" } })).StatusCode == HttpStatusCode.BadRequest, "API exige recuperação na semana");
        var invalid = await Json(await owner.GetAsync(path + "/")); Check(invalid.GetProperty("revision").GetInt32() == rev, "Plano inválido não persiste revisão ou treino parcial");
        var saved = await Json(await owner.PostAsJsonAsync(path + "/plan", new { revision = rev, plan = new { days = new[] { "Passe", "Passe", "Passe", "Passe", "Passe", "Descanso" }, intensity = "Alta" } }));
        int next = saved.GetProperty("revision").GetInt32();
        Check((await owner.PostAsJsonAsync(path + "/plan", new { revision = rev, plan = new { days = new[] { "Descanso", "Descanso", "Descanso", "Descanso", "Descanso", "Descanso" }, intensity = "Leve" } })).StatusCode == HttpStatusCode.Conflict, "Treino rejeita revisão antiga");
        var focus = await Json(await owner.PostAsJsonAsync(path + $"/players/{playerId}/focus", new { revision = next, focus = "Passe", experience = 999, attributes = new { Passe = 99 } })); next = focus.GetProperty("revision").GetInt32();
        data = await Json(await owner.GetAsync(path + "/")); var dev = data.GetProperty("players")[0].GetProperty("development");
        Check(dev.GetProperty("focus").GetString() == "Passe" && dev.GetProperty("experience").GetProperty("Passe").GetInt32() == 0 && dev.GetProperty("attributes").GetProperty("Passe").GetInt32() == player.GetProperty("player").GetProperty("rating").GetInt32(), "Cliente escolhe foco, mas não concede XP nem atributos");
        Check((await owner.PostAsJsonAsync(path + "/players/10000/focus", new { revision = next, focus = "Passe" })).StatusCode == HttpStatusCode.BadRequest, "Foco em atleta de fora do elenco é bloqueado");
        Check((await BroadcastApiTests.CompleteRound(owner, id!, next)).IsSuccessStatusCode, "Rodada aplica plano e simula partida");
        data = await Json(await owner.GetAsync(path + "/")); Check(data.GetProperty("reports").GetArrayLength() == 1 && data.GetProperty("reports")[0].GetProperty("plan").GetProperty("intensity").GetString() == "Alta", "Relatório conserva o plano aplicado na rodada");
        var raw = data.GetRawText(); Check((await Json(await owner.GetAsync(path + "/"))).GetRawText() == raw, "Reabrir relatório não repete treino ou recuperação");
        Console.WriteLine("Testes HTTP de treinamento passaram.");
    }
}
