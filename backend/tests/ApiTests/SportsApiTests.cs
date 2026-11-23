using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Brasileirao.IntegrationTests;

public static class SportsApiTests
{
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static async Task<JsonElement> Json(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone(); }
    public static async Task Run(HttpClient owner, HttpClient other, string id)
    {
        var career = await Json(await owner.GetAsync($"/api/careers/{id}")); int revision = career.GetProperty("revision").GetInt32(); var state = career.GetProperty("state");
        Check(state.GetProperty("sportsVersion").GetInt32() == 1 && state.GetProperty("benchIds").GetArrayLength() > 0, "Carreira expõe banco e versão esportiva");
        var path = $"/api/careers/{id}/sports";
        Check((await other.GetAsync(path + "/statistics")).StatusCode == HttpStatusCode.NotFound, "Estatísticas respeitam isolamento de contas");
        Check((await other.PostAsJsonAsync(path + "/lineup/auto", new { revision })).StatusCode == HttpStatusCode.NotFound, "Outra conta não altera escalação automática");
        Check((await owner.PostAsJsonAsync(path + "/plan", new { revision = revision - 1, substitutions = Array.Empty<object>(), autoSubstitute = true, autoReplaceUnavailable = true })).StatusCode == HttpStatusCode.Conflict, "Plano com revisão antiga é bloqueado");
        Check((await owner.PostAsJsonAsync(path + "/plan", new { revision, substitutions = new object?[] { null }, autoSubstitute = true, autoReplaceUnavailable = true })).StatusCode == HttpStatusCode.BadRequest, "API rejeita item nulo sem falha interna");
        Check((await owner.PostAsJsonAsync(path + "/plan", new { revision, substitutions = Array.Empty<object>(), autoSubstitute = false, autoReplaceUnavailable = true })).IsSuccessStatusCode, "Plano e controles automáticos são persistidos");
        career = await Json(await owner.GetAsync($"/api/careers/{id}")); revision = career.GetProperty("revision").GetInt32();
        Check(!career.GetProperty("state").GetProperty("autoSubstitute").GetBoolean(), "Leitura confirma configuração de substituições");
        Check((await owner.PostAsJsonAsync(path + "/lineup/auto", new { revision })).IsSuccessStatusCode, "API recompõe titulares elegíveis");
        var stats = await Json(await owner.GetAsync(path + "/statistics?sort=goals"));
        Check(stats.GetProperty("fromRound").GetInt32() == 1 && stats.GetProperty("players").GetArrayLength() > 0, "Artilharia retorna estatísticas calculadas das partidas");
        Console.WriteLine("Testes HTTP esportivos passaram.");
    }
}
