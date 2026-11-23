using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
namespace Brasileirao.IntegrationTests;

public static class YouthApiTests
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static async Task<JsonElement> Json(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone(); }
    public static async Task Run(WebApplicationFactory<global::Program> factory, HttpClient other)
    {
        using var owner = factory.CreateClient(new() { HandleCookies = false }); using var anonymous = factory.CreateClient(new() { HandleCookies = false });
        var identity = await Json(await owner.PostAsJsonAsync("/api/auth/register", new { email = $"youth-{Guid.NewGuid():N}@example.test", password = "Youth-password-123!", name = "Formador de teste" })); owner.DefaultRequestHeaders.Authorization = new("Bearer", identity.GetProperty("token").GetString());
        var created = await Json(await owner.PostAsJsonAsync("/api/careers/", new { name = "Categorias de base", clubId = "pal" })); var id = created.GetProperty("id").GetString(); var path = $"/api/careers/{id}/youth";
        var data = await Json(await owner.GetAsync(path + "/")); int rev = data.GetProperty("revision").GetInt32(); int young = data.GetProperty("academy")[0].GetProperty("id").GetInt32();
        Check(data.GetProperty("academy").GetArrayLength() == 6 && data.GetProperty("prospects").GetArrayLength() == 0, "API retorna base inicial e nenhum candidato não observado");
        Check((await anonymous.GetAsync(path + "/")).StatusCode == HttpStatusCode.Unauthorized, "Base exige autenticação"); Check((await other.GetAsync(path + "/")).StatusCode == HttpStatusCode.NotFound, "Base de outra conta não é exposta");
        Check((await other.PostAsJsonAsync(path + $"/players/{young}/promote", new { revision = rev })).StatusCode == HttpStatusCode.NotFound, "Outra conta não promove seus jovens");
        var promoted = await Json(await owner.PostAsJsonAsync(path + $"/players/{young}/promote", new { revision = rev, signingBonus = 0, wage = 0 })); int next = promoted.GetProperty("revision").GetInt32();
        var after = await Json(await owner.GetAsync($"/api/careers/{id}")); var contract = after.GetProperty("state").GetProperty("contracts").GetProperty(young.ToString());
        Check(after.GetProperty("state").GetProperty("balance").GetInt64() == data.GetProperty("balance").GetInt64() - data.GetProperty("promotionFee").GetInt64() && contract.GetProperty("wage").GetInt32() > 0 && contract.GetProperty("signingBonus").GetInt64() == 100000, "Promoção ignora luvas e salário adulterados pelo cliente");
        Check((await owner.PostAsJsonAsync(path + $"/players/{young}/promote", new { revision = next })).StatusCode == HttpStatusCode.BadRequest, "Dupla promoção é rejeitada sem cobrança adicional");
        Check((await owner.PostAsJsonAsync(path + "/scouting", new { revision = rev, region = "Sul", position = "ATA" })).StatusCode == HttpStatusCode.Conflict, "Observação com revisão antiga é rejeitada");
        Check((await owner.PostAsJsonAsync(path + "/scouting", new { revision = next, region = "Inválida", position = "ATA" })).StatusCode == HttpStatusCode.BadRequest, "API valida a região antes de cobrar");
        var search = await Json(await owner.PostAsJsonAsync(path + "/scouting", new { revision = next, region = "Sul", position = "ATA", fee = 0 })); next = search.GetProperty("revision").GetInt32(); data = await Json(await owner.GetAsync(path + "/"));
        Check(data.GetProperty("prospects").GetArrayLength() == 0 && !data.GetProperty("scouting").TryGetProperty("seed", out _), "Busca não revela candidatos ou seed antes da conclusão");
        var unchanged = data.GetRawText(); Check((await Json(await owner.GetAsync(path + "/"))).GetRawText() == unchanged, "Consultar observação não avança prazo nem cobra novamente");
        for (int i = 0; i < 2; i++) { var advance = await Json(await BroadcastApiTests.CompleteRound(owner, id!, next)); next = advance.GetProperty("revision").GetInt32(); }
        data = await Json(await owner.GetAsync(path + "/")); Check(data.GetProperty("prospects").GetArrayLength() == 6 && data.GetProperty("scouting").GetProperty("status").GetString() == "completed", "Relatório chega após duas rodadas pelo avanço da carreira");
        int candidate = data.GetProperty("prospects")[0].GetProperty("player").GetProperty("id").GetInt32(); long cash = data.GetProperty("balance").GetInt64();
        var recruited = await Json(await owner.PostAsJsonAsync(path + $"/prospects/{candidate}/recruit", new { revision = next, fee = 0 })); next = recruited.GetProperty("revision").GetInt32(); data = await Json(await owner.GetAsync(path + "/"));
        Check(data.GetProperty("balance").GetInt64() == cash - 50000 && data.GetProperty("academy").EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == candidate), "Captação aplica taxa autoritativa e move candidato para a base");
        var training = await Json(await owner.GetAsync($"/api/careers/{id}/training/")); Check(training.GetProperty("players").EnumerateArray().Any(p => p.GetProperty("player").GetProperty("id").GetInt32() == candidate && p.GetProperty("group").GetString() == "Base"), "Captado aparece no treino com grupo Base");
        Check((await owner.PostAsJsonAsync($"/api/careers/{id}/training/players/{candidate}/focus", new { revision = next, focus = "Finalização" })).IsSuccessStatusCode, "Jovem da base aceita foco individual");
        Console.WriteLine("Testes HTTP de categorias de base passaram.");
    }
}
