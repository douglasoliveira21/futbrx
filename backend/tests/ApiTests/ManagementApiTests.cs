using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Brasileirao.IntegrationTests;

public static class ManagementApiTests
{
    public static async Task Run(HttpClient owner, HttpClient other, string id)
    {
        async Task<JsonElement> Json(HttpResponseMessage r) { var body = await r.Content.ReadAsStringAsync(); if (!r.IsSuccessStatusCode) throw new Exception($"HTTP {(int)r.StatusCode}: {body}"); return JsonDocument.Parse(body).RootElement.Clone(); }
        async Task<JsonElement> Read() => await Json(await owner.GetAsync("/api/careers/" + id));
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        var c = await Read(); var state = c.GetProperty("state"); var revision = c.GetProperty("revision").GetInt32();
        var initialBalance = state.GetProperty("balance").GetInt64();
        var market = await Json(await owner.GetAsync($"/api/careers/{id}/market?club=__free&page=1"));
        Check(market.GetProperty("total").GetInt32() == 12 && market.GetProperty("players").GetArrayLength() == 12, "Mercado pagina e filtra agentes livres da carreira");
        var pid = market.GetProperty("players")[0].GetProperty("player").GetProperty("id").GetInt32();
        Check((await other.GetAsync($"/api/careers/{id}/market")).StatusCode == HttpStatusCode.NotFound, "Mercado de outra carreira não é exposto");
        var quote = await Json(await owner.GetAsync($"/api/careers/{id}/market/quote/{pid}?kind=inbound&years=3"));
        var wage = quote.GetProperty("wage").GetInt32(); var bonus = quote.GetProperty("signingBonus").GetInt64();var commission=quote.GetProperty("agentCommission").GetInt64();
        Check((await owner.PostAsJsonAsync($"/api/careers/{id}/actions", new { type = "buy", playerId = pid, revision })).StatusCode == HttpStatusCode.BadRequest, "Operação legada não contorna registro de contrato");
        Check((await owner.PostAsJsonAsync($"/api/careers/{id}/market/offers", new { revision, playerId = pid, fee = -1, wage, years = 3 })).StatusCode == HttpStatusCode.BadRequest, "Taxa negativa enviada pelo navegador é rejeitada");
        var response = await Json(await owner.PostAsJsonAsync($"/api/careers/{id}/market/offers", new { revision, playerId = pid, fee = 0, wage, years = 3, signingBonus = -1, status = "completed", balance = 9999999999L }));
        var offer = response.GetProperty("result"); var offerId = offer.GetProperty("id").GetString();
        Check(offer.GetProperty("status").GetString() == "agreed" && offer.GetProperty("signingBonus").GetInt64() == bonus, "Status e luvas enviados pelo cliente são ignorados");
        c = await Read(); Check(c.GetProperty("state").GetProperty("balance").GetInt64() == initialBalance, "Proposta não altera o caixa");
        Check((await owner.PostAsJsonAsync($"/api/careers/{id}/market/offers/{offerId}/accept", new { revision })).StatusCode == HttpStatusCode.Conflict, "Confirmação com revisão antiga é bloqueada");
        revision = response.GetProperty("revision").GetInt32();
        Check((await other.PostAsJsonAsync($"/api/careers/{id}/market/offers/{offerId}/accept", new { revision })).StatusCode == HttpStatusCode.NotFound, "Outra conta não confirma a negociação");
        response = await Json(await owner.PostAsJsonAsync($"/api/careers/{id}/market/offers/{offerId}/accept", new { revision, fee = -999999, signingBonus = -999999 }));
        revision = response.GetProperty("revision").GetInt32(); c = await Read(); state = c.GetProperty("state");
        Check(state.GetProperty("balance").GetInt64() == initialBalance - bonus - commission && state.GetProperty("contracts").GetProperty(pid.ToString()).GetProperty("wage").GetInt32() == wage, "Confirmação usa valores armazenados e salário autoritativo");
        Check((await owner.PostAsJsonAsync($"/api/careers/{id}/market/offers/{offerId}/accept", new { revision })).StatusCode == HttpStatusCode.BadRequest, "Repetir confirmação não duplica contratação ou cobrança");
        var owned = state.GetProperty("squad")[0]; var ownId = owned.GetProperty("id").GetInt32(); var season = state.GetProperty("season").GetInt32();
        var oldEnd = state.GetProperty("contracts").GetProperty(ownId.ToString()).GetProperty("endSeason").GetInt32(); var years = oldEnd - season + 2;
        quote = await Json(await owner.GetAsync($"/api/careers/{id}/market/quote/{ownId}?kind=renewal&years={years}")); wage = quote.GetProperty("wage").GetInt32();
        response = await Json(await owner.PostAsJsonAsync($"/api/careers/{id}/contracts/{ownId}/renew", new { revision, wage, years })); revision = response.GetProperty("revision").GetInt32(); offerId = response.GetProperty("result").GetProperty("id").GetString();
        response = await Json(await owner.PostAsJsonAsync($"/api/careers/{id}/market/offers/{offerId}/accept", new { revision })); revision = response.GetProperty("revision").GetInt32(); c = await Read(); state = c.GetProperty("state");
        Check(state.GetProperty("contracts").GetProperty(ownId.ToString()).GetProperty("endSeason").GetInt32() > oldEnd && state.GetProperty("ledger").EnumerateArray().Any(l => l.GetProperty("season").GetInt32() == season && l.GetProperty("description").GetString()!.Contains("renovação")), "Renovação estende contrato e lança luvas na temporada correta");
        var sale = state.GetProperty("squad").EnumerateArray().First(p => !p.GetProperty("starter").GetBoolean() && p.GetProperty("position").GetString() != "GOL"); var saleId = sale.GetProperty("id").GetInt32();
        response = await Json(await owner.PostAsJsonAsync($"/api/careers/{id}/market/list/{saleId}", new { revision, listed = true })); revision = response.GetProperty("revision").GetInt32();
        response = await Json(await BroadcastApiTests.CompleteRound(owner, id, revision)); revision = response.GetProperty("revision").GetInt32();
        c = await Read(); state = c.GetProperty("state"); var outbound = state.GetProperty("offers").EnumerateArray().Single(o => o.GetProperty("playerId").GetInt32() == saleId && o.GetProperty("kind").GetString() == "outbound");
        offerId = outbound.GetProperty("id").GetString(); initialBalance = state.GetProperty("balance").GetInt64(); var fee = outbound.GetProperty("fee").GetInt64();
        response = await Json(await owner.PostAsJsonAsync($"/api/careers/{id}/market/offers/{offerId}/accept", new { revision })); c = await Read(); state = c.GetProperty("state");
        Check(state.GetProperty("balance").GetInt64() == initialBalance + fee && !state.GetProperty("squad").EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == saleId), "Venda remove atleta e credita somente a taxa negociada");
        Check(!state.TryGetProperty("opponentSquads", out _) && state.GetProperty("transfers").GetArrayLength() == 3, "Gerenciamento retorna histórico de operações sem carregar todos os elencos adversários");
        Console.WriteLine("Testes HTTP de contratos e negociações passaram.");
    }
}
