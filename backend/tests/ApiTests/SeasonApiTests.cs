using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Brasileirao.IntegrationTests;

// A week without a match for the club, through the HTTP endpoints.
public static class SeasonApiTests
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string text = await r.Content.ReadAsStringAsync();
        if (!r.IsSuccessStatusCode) throw new Exception(r.StatusCode + ": " + text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task Run(HttpClient client)
    {
        var created = await Json(await client.PostAsJsonAsync("/api/careers/", new { name = "Semana sem jogo", clubId = "pal" }));
        string path = "/api/careers/" + created.GetProperty("id").GetString();
        // Arrange the saved career: a season of 40 weeks whose league starts in round 3.
        using (var scope = BroadcastApiTests.Factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var id = Guid.Parse(created.GetProperty("id").GetString()!);
            var career = await db.Careers.SingleAsync(x => x.Id == id);
            var game = Game.Read(career);
            game.LeagueStart = 3; game.SeasonRounds = 40;
            game.Fixtures = SeasonCalendar.Generate(game.Clubs, game.Season, game.LotterySeed, "A", 3);
            game.Life!.Competitions.Clear();
            Game.Write(career, game);
            await db.SaveChangesAsync();
        }
        var state = await Json(await client.GetAsync(path));
        int revision = state.GetProperty("revision").GetInt32();
        var saved = state.GetProperty("state");
        Check(saved.GetProperty("seasonRounds").GetInt32() == 40 && saved.GetProperty("leagueStart").GetInt32() == 3, "Estado da carreira expõe o tamanho da temporada e o início da liga");
        Check(saved.GetProperty("fixtures").EnumerateArray().Min(f => f.GetProperty("round").GetInt32()) == 3, "Calendário da carreira começa na terceira semana");
        Check((await client.PostAsJsonAsync(path + "/live/start", new { revision })).StatusCode == HttpStatusCode.BadRequest, "Transmissão não abre em semana sem partida do clube");

        var first = await Json(await client.PostAsJsonAsync(path + "/advance-week", new { revision }));
        int next = first.GetProperty("revision").GetInt32();
        Check((await client.PostAsJsonAsync(path + "/advance-week", new { revision })).StatusCode == HttpStatusCode.Conflict, "Avançar a semana com revisão antiga é recusado");
        var second = await Json(await client.PostAsJsonAsync(path + "/advance-week", new { revision = next }));
        state = await Json(await client.GetAsync(path));
        Check(state.GetProperty("state").GetProperty("round").GetInt32() == 3 && second.GetProperty("revision").GetInt32() == state.GetProperty("revision").GetInt32(), "Duas semanas sem jogo levam a carreira à abertura da liga");
        Check((await client.PostAsJsonAsync(path + "/advance-week", new { revision = state.GetProperty("revision").GetInt32() })).StatusCode == HttpStatusCode.BadRequest, "Na semana com partida o servidor exige a transmissão");
    }
}
