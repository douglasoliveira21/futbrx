using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Brasileirao;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Brasileirao.IntegrationTests;

public static class BroadcastApiTests
{
    public static WebApplicationFactory<Program> Factory = null!;
    static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    // Unrelated market/training integration tests advance a fully watched fixture via their DB fixture.
    // Production has no shortcut; clock accounting is covered by BroadcastTests and the HTTP assertions here.
    public static async Task<HttpResponseMessage> CompleteRound(HttpClient client, string id, int revision)
    {
        var original=await Json(await client.GetAsync($"/api/careers/{id}"));int initialRound=original.GetProperty("state").GetProperty("round").GetInt32();
        for(int attempt=0;attempt<8;attempt++){
            var start=await client.PostAsJsonAsync($"/api/careers/{id}/live/start",new{revision});if(!start.IsSuccessStatusCode)return start;var live=await Json(start);
            using(var scope=Factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<GameDb>();var c=await db.Careers.FindAsync(Guid.Parse(id));var g=Game.Read(c!);g.LiveMatch!.HalfTimeAccepted=true;g.LiveMatch.WatchedSeconds=1000;using(var runtime=new LiveSimulation())runtime.Advance(g.LiveMatch);Game.Write(c!,g);await db.SaveChangesAsync();}
            var finished=await client.PostAsJsonAsync($"/api/careers/{id}/live/finish",new{id=live.GetProperty("id").GetString()});if(!finished.IsSuccessStatusCode)return finished;
            var state=await Json(finished);if(state.GetProperty("state").GetProperty("round").GetInt32()>initialRound)return finished;revision=state.GetProperty("revision").GetInt32();
        }
        throw new Exception("A fila de partidas não concluiu a rodada.");
    }

    public static async Task Run(HttpClient owner, HttpClient other, string careerId, int revision)
    {
        var path = $"/api/careers/{careerId}";
        Check((await owner.PostAsJsonAsync(path + "/actions", new { revision, type = "advance" })).StatusCode == HttpStatusCode.BadRequest, "API bloqueia avanço instantâneo da rodada");
        var start = await Json(await owner.PostAsJsonAsync(path + "/live/start", new { revision })); var id = start.GetProperty("id").GetString();
        Check(start.GetProperty("fixture").GetProperty("homeGoals").ValueKind == JsonValueKind.Null && !start.TryGetProperty("preparedState", out _), "Transmissão inicia sem revelar resultado");
        Check((await other.PostAsJsonAsync(path + "/live/tick", new { id, playing = true })).StatusCode == HttpStatusCode.NotFound, "Outra conta não acompanha nem adianta a partida");
        Check((await owner.PostAsJsonAsync(path + "/live/finish", new { id })).StatusCode == HttpStatusCode.BadRequest, "API recusa encerramento antes do apito final");
        var state = await Json(await owner.GetAsync(path));
        Check(state.GetProperty("state").GetProperty("round").GetInt32() == 1 && state.GetProperty("state").GetProperty("liveMatch").GetProperty("id").GetString() == id, "Reabrir carreira retoma transmissão sem avançar rodada");
        Check((await owner.PostAsJsonAsync(path + "/actions", new { revision = state.GetProperty("revision").GetInt32(), type = "tactics", value = "4-4-2", extra = "Ofensiva" })).StatusCode == HttpStatusCode.BadRequest, "Escalação congelada durante transmissão");
        var tick = await Json(await owner.PostAsJsonAsync(path + "/live/tick", new { id, playing = true }));
        Check(tick.GetProperty("replay").GetProperty("frames").EnumerateArray().All(f => f.GetProperty("second").GetInt32() == 0) && tick.GetProperty("replay").GetProperty("events").EnumerateArray().All(e => e.GetProperty("minute").GetInt32() == 0), "Servidor entrega apenas lances e posições já liberados pelo relógio");
        Check((await other.PostAsJsonAsync(path+"/live/control",new{id,type="speed",speed=48})).StatusCode==HttpStatusCode.NotFound,"Outra conta não envia comandos de treinador");
        var speed=await Json(await owner.PostAsJsonAsync(path+"/live/control",new{id,type="speed",speed=48}));
        Check(speed.GetProperty("speed").GetInt32()==48,"API aplica multiplicador durante transmissão");
        Check((await owner.PostAsJsonAsync(path+"/live/control",new{id,type="second_half"})).StatusCode==HttpStatusCode.BadRequest,"Não antecipa segundo tempo fora do intervalo");
        var tactics=await owner.PostAsJsonAsync(path+"/live/control",new{id,type="tactics",formation="3-5-2",mentality="Ofensiva"});
        Check(tactics.IsSuccessStatusCode,"API aceita mudança tática durante partida");
        var done = await CompleteRound(owner, careerId, state.GetProperty("revision").GetInt32()); Check(done.IsSuccessStatusCode, "Rodada registrada ao concluir transmissão");
        Check((await owner.PostAsJsonAsync(path + "/live/finish", new { id })).StatusCode == HttpStatusCode.BadRequest, "Finalizar novamente não duplica resultado");
    }
}
