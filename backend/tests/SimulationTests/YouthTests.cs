using Brasileirao;
using System.Text.Json;
public static class YouthTests
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static void Reject(Action action, string text) { try { action(); throw new Exception("Aceitou: " + text); } catch (GameRuleException) { Console.WriteLine("PASS: " + text); } }
    static string Snapshot(GameState g) => JsonSerializer.Serialize(g, Game.Json);
    public static void Run()
    {
        var g = Game.New("pal"); var ids = g.Squad.Concat(g.Market).Concat(g.Academy).Concat(g.OpponentSquads.Values.SelectMany(s => s)).Select(p => p.Id).ToArray();
        Check(g.Academy.Count == 6 && g.Academy.All(p => p.Age >= 16 && p.Age <= 19) && ids.Distinct().Count() == ids.Length, "Base inicial tem seis jovens e IDs distintos dos profissionais");
        var initial = Snapshot(g); Youth.Upgrade(g); Check(Snapshot(g) == initial, "Upgrade da base não duplica atletas nem altera caixa");
        var p = g.Academy[0]; Training.SetFocus(g, p.Id, "Goleiro"); long openingBalance = g.Balance; long wages = g.Squad.Sum(p => (long)p.Wage); Game.Advance(g);
        Check(g.Balance == openingBalance + g.Ledger.Where(l=>l.Round==1&&l.Season==g.Season).Sum(l=>l.Amount), "Saldo da rodada inclui folha e manutenção da base");
        Check(g.TrainingReports[0].Players.Any(r => r.PlayerId == p.Id && r.Group == "Base" && r.Experience.Values.Sum() > 0), "Jovens treinam e aparecem no relatório como Base");
        Check(g.Ledger.Any(l => l.Description == "Manutenção das categorias de base" && l.Amount == -6 * Youth.WeeklyCost && l.Round == 1), "Manutenção da base é lançada na rodada correta");
        var balance = g.Balance; var profile = SnapshotProfile(g, p.Id); Youth.Promote(g, p.Id);
        Check(g.Squad.Contains(p) && !g.Academy.Contains(p) && g.Balance == balance - Youth.PromotionFee, "Promoção move o atleta e debita apenas as luvas");
        Check(g.Contracts[p.Id].Active && g.Contracts[p.Id].Wage == p.Wage && g.Contracts[p.Id].EndSeason == g.Season + 2 && SnapshotProfile(g, p.Id) == profile, "Promoção cria contrato e preserva atributos e XP");
        var promoted = Snapshot(g); Reject(() => Youth.Promote(g, p.Id), "Não permite promover o mesmo atleta duas vezes"); Check(Snapshot(g) == promoted, "Dupla promoção não cobra novamente");
        var before = g.Balance; Youth.StartSearch(g, "Nordeste", "ATA"); Check(g.Balance == before - Youth.SearchFee && g.YouthProspects.Count == 0, "Observação cobra taxa sem revelar candidatos antecipadamente");
        var searching = Snapshot(g); Reject(() => Youth.StartSearch(g, "Sul", "GOL"), "Só permite uma observação em andamento"); Check(Snapshot(g) == searching, "Observação duplicada não cobra nem substitui missão");
        Game.Advance(g); Check(g.YouthProspects.Count == 0, "Primeira rodada de observação ainda não entrega relatório"); Game.Advance(g);
        Check(g.Scouting!.Status == "completed" && g.YouthProspects.Count == 6 && g.YouthProspects.All(x => x.Player.Position == "ATA" && x.Region == "Nordeste"), "Duas rodadas entregam seis jovens da posição selecionada");
        var candidate = g.YouthProspects[0]; before = g.Balance; Youth.Recruit(g, candidate.Player.Id);
        Check(g.Academy.Contains(candidate.Player) && g.YouthProspects.Count == 5 && g.Balance == before - candidate.SigningFee && g.Development.ContainsKey(candidate.Player.Id), "Captação debita taxa e integra jovem ao desenvolvimento");
        var recruited = Snapshot(g); Reject(() => Youth.Recruit(g, candidate.Player.Id), "Candidato já incorporado não pode ser cobrado novamente"); Check(Snapshot(g) == recruited, "Dupla captação não altera o save");
        var noMoney = Game.New("pal"); noMoney.Balance = 0; var insufficient = Snapshot(noMoney); Reject(() => Youth.StartSearch(noMoney, "Sul", "Todas"), "Sem saldo não inicia observação"); Reject(() => Youth.Promote(noMoney, noMoney.Academy[0].Id), "Sem saldo não promove"); Check(Snapshot(noMoney) == insufficient, "Falhas financeiras não aplicam alterações parciais");
        Reject(() => Youth.StartSearch(noMoney, "Exterior", "Todas"), "Região inválida é rejeitada"); Reject(() => Youth.StartSearch(noMoney, "Sul", "XYZ"), "Posição inválida é rejeitada"); noMoney.Round = 37; Reject(() => Youth.StartSearch(noMoney, "Sul", "Todas"), "Observações encerram após a rodada 36");
        var capped = Game.Read(new() { State = Snapshot(g) }); while (capped.Academy.Count < Youth.Capacity) capped.Academy.Add(Game.Players(30000 + capped.Academy.Count * 100)[0]); var full = Snapshot(capped); Reject(() => Youth.Recruit(capped, capped.YouthProspects[0].Player.Id), "Captação respeita limite de vagas"); Check(Snapshot(capped) == full, "Base cheia não cobra captação");
        var proFull = Game.New("pal"); while (proFull.Squad.Count < 40) proFull.Squad.Add(Game.Players(40000 + proFull.Squad.Count * 100)[0]); var fullPro = Snapshot(proFull); Reject(() => Youth.Promote(proFull, proFull.Academy[0].Id), "Promoção respeita limite profissional de quarenta"); Check(Snapshot(proFull) == fullPro, "Elenco cheio não cobra promoção");
        int expiredId = g.YouthProspects[0].Player.Id; g.Round = g.Scouting.ExpiresRound + 1; Youth.AfterRound(g); Check(g.Scouting.Status == "expired" && g.YouthProspects.Count == 0, "Relatório expira e remove candidatos antigos"); Reject(() => Youth.Recruit(g, expiredId), "Candidato expirado não pode ser incorporado");
        var annual = Game.New("pal"); var graduate = annual.Academy[0]; graduate.Age = 20; annual.Development[graduate.Id].Experience["Passe"] = 77; annual.Round = 39; var cash = annual.Balance; annual.Life!.Competitions.Clear();TransferMarket.NextSeason(annual);
        Check(!annual.Academy.Contains(graduate) && annual.Market.Contains(graduate) && graduate.Age == 21 && annual.Balance == cash, "Jovem acima da idade sai como livre sem receita fictícia");
        Check(annual.Academy.Count == 8 && annual.Academy.All(x => x.Age <= 20) && annual.Development[graduate.Id].Experience["Passe"] == 77, "Virada inclui três ingressantes e conserva desenvolvimento dos que saem");
        var saved = Game.Read(new() { State = Snapshot(annual) }); Check(saved.Academy.Count == annual.Academy.Count && saved.YouthVersion == 1, "Base é preservada ao recarregar o save");
        Console.WriteLine("Testes de formação, captação e promoção passaram.");
    }
    static string SnapshotProfile(GameState g, int id) => JsonSerializer.Serialize(g.Development[id], Game.Json);
}
