using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
namespace Brasileirao.IntegrationTests;

public static class AdvancedApiTests
{
    static void Check(bool yes, string text) { if (!yes) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static async Task<JsonElement> Json(HttpResponseMessage r) { r.EnsureSuccessStatusCode(); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone(); }
    public static async Task Run(WebApplicationFactory<global::Program> factory, HttpClient other)
    {
        using var owner = factory.CreateClient(new() { HandleCookies = false }); using var anon = factory.CreateClient(new() { HandleCookies = false });
        var auth = await Json(await owner.PostAsJsonAsync("/api/auth/register", new { email = $"advanced-{Guid.NewGuid():N}@example.test", password = "Advanced-password-123!", name = "Gestor de teste" })); owner.DefaultRequestHeaders.Authorization = new("Bearer", auth.GetProperty("token").GetString());
        var releases = await Json(await owner.GetAsync("/api/catalogs")); var release = releases.EnumerateArray().First(x => x.GetProperty("version").GetString()!.StartsWith("fictional-"));
        var career = await Json(await owner.PostAsJsonAsync("/api/careers/", new { name = "Gestão avançada", clubId = "pal", releaseId = release.GetProperty("id").GetString() })); var id = career.GetProperty("id").GetString(); var path = $"/api/careers/{id}"; int revision = 1;
        Check((await anon.GetAsync(path + "/advanced-market")).StatusCode == HttpStatusCode.Unauthorized, "Gestão avançada exige autenticação");
        Check((await other.GetAsync(path + "/divisions")).StatusCode == HttpStatusCode.NotFound, "Outra conta não consulta divisões da carreira");
        var enabled = await Json(await owner.PostAsJsonAsync(path + "/divisions/enable", new { revision })); revision = enabled.GetProperty("revision").GetInt32();
        var division = await Json(await owner.GetAsync(path + "/divisions")); Check(division.GetProperty("divisions").GetProperty("otherClubs").GetArrayLength() == 20, "API ativa vinte clubes na divisão demonstrativa complementar");
        var candidates = await Json(await owner.GetAsync(path + "/advanced-market")); var player = candidates.GetProperty("candidates").EnumerateArray().First(p => p.GetProperty("available").GetBoolean() && p.GetProperty("wage").GetInt32() < 90000); int pid = player.GetProperty("id").GetInt32(); long fee = player.GetProperty("loanFee").GetInt64();
        Check((await other.PostAsJsonAsync(path + "/loans", new { revision, playerId = pid, fee })).StatusCode == HttpStatusCode.NotFound, "Outra conta não contrata empréstimo");
        Check((await owner.PostAsJsonAsync(path + "/loans", new { revision = 1, playerId = pid, fee })).StatusCode == HttpStatusCode.Conflict, "Empréstimo com revisão antiga é bloqueado");
        Check((await owner.PostAsJsonAsync(path + "/loans", new { revision, playerId = pid, fee = fee + 1 })).StatusCode == HttpStatusCode.BadRequest, "API rejeita taxa de empréstimo adulterada");
        // Goalkeepers can appear in the candidate list, but CanRelease rejects those below the keeper minimum.
        foreach (var p in candidates.GetProperty("candidates").EnumerateArray().Where(p => p.GetProperty("available").GetBoolean()))
        {
            pid = p.GetProperty("id").GetInt32(); fee = p.GetProperty("loanFee").GetInt64(); var response = await owner.PostAsJsonAsync(path + "/loans", new { revision, playerId = pid, fee, wage = 0 });
            if (response.StatusCode == HttpStatusCode.BadRequest) continue;
            var accepted = await Json(response); revision = accepted.GetProperty("revision").GetInt32(); break;
        }
        var after = await Json(await owner.GetAsync(path)); Check(after.GetProperty("state").GetProperty("loans").GetArrayLength() == 1 && after.GetProperty("state").GetProperty("squad").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == pid).GetProperty("wage").GetInt32() > 0, "Empréstimo mantém salário autoritativo e registra vínculo temporário");
        Check((await owner.PostAsJsonAsync(path + "/loans", new { revision, playerId = pid, fee })).StatusCode == HttpStatusCode.BadRequest, "Repetir empréstimo não duplica atleta");
        var ownPlayer = after.GetProperty("state").GetProperty("squad").EnumerateArray().First(p => p.GetProperty("id").GetInt32() != pid && !p.GetProperty("starter").GetBoolean() && p.GetProperty("position").GetString() != "GOL");
        int lendId = ownPlayer.GetProperty("id").GetInt32(); long lendFee = Math.Max(50000, ownPlayer.GetProperty("value").GetInt64() / 20);
        string borrower = after.GetProperty("state").GetProperty("clubs").EnumerateArray().First(c => c.GetProperty("id").GetString() != "pal").GetProperty("id").GetString()!;
        Check((await other.PostAsJsonAsync(path + "/loans/lend", new { revision, playerId = lendId, borrowerClub = borrower, fee = lendFee })).StatusCode == HttpStatusCode.NotFound, "Outra conta não cede seu atleta por empréstimo");
        var lent = await Json(await owner.PostAsJsonAsync(path + "/loans/lend", new { revision, playerId = lendId, borrowerClub = borrower, fee = lendFee })); revision = lent.GetProperty("revision").GetInt32();
        after = await Json(await owner.GetAsync(path)); Check(after.GetProperty("state").GetProperty("loans").GetArrayLength() == 2 && !after.GetProperty("state").GetProperty("squad").EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == lendId), "API registra empréstimo de saída e mantém a vaga de retorno");
        var round = await Json(await BroadcastApiTests.CompleteRound(owner, id!, revision)); revision = round.GetProperty("revision").GetInt32();
        Check((await owner.PostAsJsonAsync(path + "/divisions/enable", new { revision })).StatusCode == HttpStatusCode.BadRequest, "API não muda composição da liga depois de iniciada");
        after = await Json(await owner.GetAsync(path)); var fixture = after.GetProperty("state").GetProperty("fixtures")[0];
        var replay = await Json(await owner.GetAsync(path + $"/matches/1/{fixture.GetProperty("home").GetString()}/replay"));
        Check(replay.GetProperty("engineVersion").GetString() == MatchSimulation.Version && replay.GetProperty("duration").GetInt32() > 5400, "API reproduz motor atual até o fim dos acréscimos");
        var calendar = await Json(await anon.GetAsync("/api/calendar/2026")); Check(calendar.GetProperty("fixtures").GetArrayLength() == 380, "Calendário aberto acessível sem expor dados de carreiras");
    }
}
