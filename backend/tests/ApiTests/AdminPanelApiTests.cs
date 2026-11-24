using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Brasileirao.IntegrationTests;

public static class AdminPanelApiTests
{
    const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
    static async Task<JsonElement> Json(HttpResponseMessage r) { var body = await r.Content.ReadAsStringAsync(); if (!r.IsSuccessStatusCode) throw new Exception(r.StatusCode + ": " + body); return JsonDocument.Parse(body).RootElement.Clone(); }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }

    public static async Task Run(WebApplicationFactory<Program> factory, HttpClient admin, HttpClient other, string adminEmail, string adminId)
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        Check((await other.GetAsync("/api/admin/accounts")).StatusCode == HttpStatusCode.Forbidden, "Conta comum não lista contas");

        // Administrador identificado pelo e-mail em vez do ID.
        config["Admin:UserIds"] = adminEmail.ToUpperInvariant();
        Check((await admin.GetAsync("/api/admin/accounts")).IsSuccessStatusCode, "E-mail configurado concede acesso administrativo");
        Check((await other.GetAsync("/api/admin/accounts")).StatusCode == HttpStatusCode.Forbidden, "Outro e-mail continua sem acesso");
        config["Admin:UserIds"] = adminId;

        // Contas.
        using var victimClient = factory.CreateClient(new() { HandleCookies = false });
        var email = "vitima-" + Guid.NewGuid().ToString("N") + "@example.test";
        var victim = await Json(await victimClient.PostAsJsonAsync("/api/auth/register", new { email, password = "Test-password-123!", name = "Conta removivel" }));
        var victimId = victim.GetProperty("user").GetProperty("id").GetString();
        victimClient.DefaultRequestHeaders.Authorization = new("Bearer", victim.GetProperty("token").GetString());
        var found = await Json(await admin.GetAsync("/api/admin/accounts?q=removivel"));
        Check(found.GetArrayLength() == 1 && found[0].GetProperty("email").GetString() == email, "Busca de contas por nome");
        Check((await admin.PostAsJsonAsync($"/api/admin/accounts/{victimId}/rename", new { name = "Renomeada" })).IsSuccessStatusCode, "Admin renomeia conta");
        Check((await admin.PostAsJsonAsync($"/api/admin/accounts/{victimId}/reset-password", new { password = "curta" })).StatusCode == HttpStatusCode.BadRequest, "Senha curta é rejeitada");
        Check((await admin.PostAsJsonAsync($"/api/admin/accounts/{victimId}/reset-password", new { password = "Nova-senha-segura-1" })).IsSuccessStatusCode, "Admin redefine a senha");
        Check((await victimClient.GetAsync("/api/auth/me")).StatusCode == HttpStatusCode.Unauthorized, "Redefinir a senha encerra as sessões");
        using var fresh = factory.CreateClient(new() { HandleCookies = false });
        Check((await fresh.PostAsJsonAsync("/api/auth/login", new { email, password = "Nova-senha-segura-1" })).IsSuccessStatusCode, "Login com a nova senha");
        Check((await admin.DeleteAsync($"/api/admin/accounts/{adminId}")).StatusCode == HttpStatusCode.Conflict, "Admin não apaga a própria conta");
        Check((await admin.DeleteAsync($"/api/admin/accounts/{victimId}")).IsSuccessStatusCode, "Admin apaga conta");
        Check((await fresh.PostAsJsonAsync("/api/auth/login", new { email, password = "Nova-senha-segura-1" })).StatusCode == HttpStatusCode.Unauthorized, "Conta apagada não entra mais");

        // Clubes e jogadores: só carreiras novas mudam.
        var releases = await Json(await admin.GetAsync("/api/catalogs"));
        var rid = releases.EnumerateArray().First(r => r.GetProperty("version").GetString() == "fictional-2026-v1").GetProperty("id").GetString();
        var before = await Json(await admin.PostAsJsonAsync("/api/careers/", new { name = "Antes da edição", clubId = "pal", releaseId = rid }));
        var beforeId = before.GetProperty("id").GetString();
        Check((await other.PostAsJsonAsync($"/api/admin/releases/{rid}/clubs/pal", new { name = "X" })).StatusCode == HttpStatusCode.Forbidden, "Conta comum não edita clubes");
        Check((await admin.PostAsJsonAsync($"/api/admin/releases/{rid}/clubs/pal", new { name = "Palmeiras Editado", @short = "pae", color = "#112233", strength = 90 })).IsSuccessStatusCode, "Admin edita nome, sigla, cor e força do clube");
        Check((await admin.PostAsJsonAsync($"/api/admin/releases/{rid}/clubs/pal", new { color = "azul" })).StatusCode == HttpStatusCode.BadRequest, "Cor inválida é rejeitada");
        var clubs = await Json(await admin.GetAsync("/api/clubs?releaseId=" + rid));
        var pal = clubs.EnumerateArray().Single(c => c.GetProperty("id").GetString() == "pal");
        Check(pal.GetProperty("name").GetString() == "Palmeiras Editado" && pal.GetProperty("short").GetString() == "PAE" && pal.GetProperty("strength").GetInt32() == 90, "Catálogo público mostra o clube editado");
        var after = await Json(await admin.PostAsJsonAsync("/api/careers/", new { name = "Depois da edição", clubId = "pal", releaseId = rid }));
        string ClubName(JsonElement career) => career.GetProperty("state").GetProperty("clubs").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "pal").GetProperty("name").GetString()!;
        Check(ClubName(await Json(await admin.GetAsync("/api/careers/" + after.GetProperty("id").GetString()))) == "Palmeiras Editado", "Carreira nova usa o clube editado");
        Check(ClubName(await Json(await admin.GetAsync("/api/careers/" + beforeId))) != "Palmeiras Editado", "Carreira existente não muda");

        var detail = await Json(await admin.GetAsync("/api/admin/catalogs/" + rid));
        var first = detail.GetProperty("catalog").GetProperty("players").EnumerateArray().First(p => p.GetProperty("clubId").GetString() == "pal");
        var pid = first.GetProperty("player").GetProperty("id").GetInt32();
        Check((await admin.PostAsJsonAsync($"/api/admin/releases/{rid}/players/{pid}", new { name = "Craque Editado", rating = 91, age = 22, value = 5000000, wage = 90000 })).IsSuccessStatusCode, "Admin edita nome, overall e dados do jogador");
        Check((await admin.PostAsJsonAsync($"/api/admin/releases/{rid}/players/{pid}", new { rating = 200 })).StatusCode == HttpStatusCode.BadRequest, "Overall fora de 1 a 99 é rejeitado");
        var edited = (await Json(await admin.GetAsync("/api/admin/catalogs/" + rid))).GetProperty("catalog").GetProperty("players").EnumerateArray().Single(p => p.GetProperty("player").GetProperty("id").GetInt32() == pid).GetProperty("player");
        Check(edited.GetProperty("name").GetString() == "Craque Editado" && edited.GetProperty("rating").GetInt32() == 91, "Edição do jogador foi gravada");
        var added = await Json(await admin.PostAsJsonAsync($"/api/admin/releases/{rid}/players", new { clubId = "pal", name = "Reforço Novo", position = "ATA", age = 19, rating = 70, value = 1000000, wage = 20000 }));
        var newId = added.GetProperty("id").GetInt32();
        Check(newId > pid, "Admin adiciona jogador ao clube");
        Check((await admin.PostAsJsonAsync($"/api/admin/releases/{rid}/players", new { clubId = "nao-existe", name = "Fantasma", position = "ATA", age = 19, rating = 70, value = 1, wage = 1 })).StatusCode == HttpStatusCode.BadRequest, "Jogador em clube inexistente é rejeitado");
        Check((await admin.DeleteAsync($"/api/admin/releases/{rid}/players/{newId}")).IsSuccessStatusCode, "Admin remove jogador");
        Check((await admin.DeleteAsync($"/api/admin/releases/{rid}/players/{newId}")).StatusCode == HttpStatusCode.NotFound, "Jogador já removido não existe mais");

        // Escudo.
        Check((await other.PostAsJsonAsync("/api/admin/clubs/pal/logo", new { dataUri = Png })).StatusCode == HttpStatusCode.Forbidden, "Conta comum não envia escudo");
        Check((await admin.PostAsJsonAsync("/api/admin/clubs/pal/logo", new { dataUri = "data:text/html;base64,PGI+" })).StatusCode == HttpStatusCode.BadRequest, "Escudo que não é imagem permitida é rejeitado");
        Check((await admin.PostAsJsonAsync("/api/admin/clubs/pal/logo", new { dataUri = Png })).IsSuccessStatusCode, "Admin envia o escudo");
        var logos = await Json(await other.GetAsync("/api/logos"));
        Check(logos.EnumerateArray().Any(l => l.GetProperty("clubId").GetString() == "pal"), "Lista pública de escudos inclui o clube");
        var image = await other.GetAsync("/api/logos/pal");
        Check(image.IsSuccessStatusCode && image.Content.Headers.ContentType?.MediaType == "image/png", "Escudo é servido como imagem");
        Check((await admin.DeleteAsync("/api/admin/clubs/pal/logo")).IsSuccessStatusCode && (await other.GetAsync("/api/logos/pal")).StatusCode == HttpStatusCode.NotFound, "Admin remove o escudo");

        // Séries: trocar de série e substituir times.
        var catalogs = await Json(await admin.GetAsync("/api/catalogs"));
        string ReleaseOf(string version) => catalogs.EnumerateArray().Single(r => r.GetProperty("version").GetString() == version).GetProperty("id").GetString()!;
        async Task<JsonElement> Directory() => await Json(await other.GetAsync("/api/directory"));
        string SeriesOf(JsonElement dir, string id) => dir.GetProperty("clubs").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id).GetProperty("series").GetString()!;
        async Task<JsonElement> Clubs(string version) => await Json(await admin.GetAsync("/api/clubs?releaseId=" + ReleaseOf(version)));
        var dir = await Directory();
        var bClub = dir.GetProperty("clubs").EnumerateArray().First(c => c.GetProperty("series").GetString() == "B").GetProperty("id").GetString()!;
        Check(SeriesOf(dir, "pal") == "A", "Diretório informa a série do clube");
        Check((await other.PostAsJsonAsync("/api/admin/clubs/swap", new { clubA = "pal", clubB = bClub })).StatusCode == HttpStatusCode.Forbidden, "Conta comum não troca times de série");
        Check((await admin.PostAsJsonAsync("/api/admin/clubs/swap", new { clubA = "pal", clubB = "pal" })).StatusCode == HttpStatusCode.BadRequest, "Trocar um clube com ele mesmo é rejeitado");
        Check((await admin.PostAsJsonAsync("/api/admin/clubs/swap", new { clubA = "pal", clubB = bClub })).IsSuccessStatusCode, "Admin troca dois times de série");
        dir = await Directory();
        Check(SeriesOf(dir, "pal") == "B" && SeriesOf(dir, bClub) == "A", "Diretório reflete a troca de série");
        foreach (var version in new[] { Game.Candidate2026Version, Game.Clubs2026Version, Game.SerieB2026Version })
        {
            var list = await Clubs(version);
            Check(list.GetArrayLength() == 20, "Versão " + version + " continua com vinte clubes");
        }
        var moved = await Json(await admin.PostAsJsonAsync("/api/careers/", new { name = "Time que mudou de série", clubId = "pal", releaseId = ReleaseOf(Game.SerieB2026Version) }));
        Check(moved.TryGetProperty("id", out _), "Nova carreira com o time na série nova");
        Check((await admin.PostAsJsonAsync("/api/admin/clubs/swap", new { clubA = "pal", clubB = bClub })).IsSuccessStatusCode && SeriesOf(await Directory(), "pal") == "A", "Troca de volta restaura as séries");

        var cClub = dir.GetProperty("clubs").EnumerateArray().First(c => c.GetProperty("series").GetString() == "C").GetProperty("id").GetString()!;
        var newClub = new { outClubId = cClub, name = "Time Novo FC", @short = "TNV", color = "#123456", strength = 72, city = "Cidade Nova", uf = "SP" };
        Check((await admin.PostAsJsonAsync("/api/admin/series/C/replace", newClub with { uf = "SAO" })).StatusCode == HttpStatusCode.BadRequest, "Estado inválido é rejeitado");
        Check((await admin.PostAsJsonAsync("/api/admin/series/C/replace", newClub with { @short = "" })).StatusCode == HttpStatusCode.BadRequest, "Sigla vazia é rejeitada");
        var replaced = await admin.PostAsJsonAsync("/api/admin/series/C/replace", newClub);
        Check(replaced.IsSuccessStatusCode, "Admin cria um time novo substituindo outro da série");
        dir = await Directory();
        var created = dir.GetProperty("clubs").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "time-novo-fc");
        Check(created.GetProperty("series").GetString() == "C" && created.GetProperty("uf").GetString() == "SP" && created.GetProperty("city").GetString() == "Cidade Nova", "Time novo aparece no diretório com cidade e estado");
        Check(SeriesOf(dir, cClub) == "D" && dir.GetProperty("clubs").GetArrayLength() == 157, "Time substituído passa para a Série D");
        Check((await Clubs(Game.SerieC2026Version)).GetArrayLength() == 20, "Série C continua com vinte clubes");
        var created2 = await Json(await admin.PostAsJsonAsync("/api/careers/", new { name = "Time criado pelo admin", clubId = "time-novo-fc", releaseId = ReleaseOf(Game.SerieC2026Version) }));
        Check(created2.TryGetProperty("id", out _), "Carreira nova com o time criado pelo admin");
        Check((await admin.PostAsJsonAsync("/api/admin/series/C/replace", newClub with { outClubId = "time-novo-fc" })).StatusCode == HttpStatusCode.BadRequest, "ID repetido em uma série é rejeitado");
        Check((await admin.PostAsJsonAsync("/api/admin/series/C/replace", newClub with { outClubId = "pal", name = "Outro Time" })).StatusCode == HttpStatusCode.BadRequest, "Time que sai precisa estar na série informada");
    }
}
