using Brasileirao;
using System.Text.Json;
public static class AdvancedTests
{
    static void Check(bool yes, string text) { if (!yes) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static void Reject(Action action, string text) { try { action(); } catch (GameRuleException) { Console.WriteLine("PASS: " + text); return; } throw new Exception(text); }
    public static void Run()
    {
        Check(MatchRules.IsOffside(40, 30, 1, [48, 35, 20]), "Impedimento no toque além da bola e do penúltimo adversário");
        Check(!MatchRules.IsOffside(35, 30, 1, [48, 35, 20]) && !MatchRules.IsOffside(-2, -5, 1, [5, -4]), "Atleta em linha ou no próprio campo não está impedido");
        Check(MatchRules.IsOffside(-40, -30, -1, [-48, -35, -20]), "Impedimento funciona com lados invertidos");
        var separated = MatchRules.Separate(0, 0, 0, 0, .52);
        Check(separated.Bx - separated.Ax > 1.03, "Colisão resolve centros coincidentes sem NaN");
        Check(MatchRules.InPenaltyArea(40, 10, 1) && !MatchRules.InPenaltyArea(35, 10, 1) && !MatchRules.InPenaltyArea(40, 22, 1), "Pênalti exige posição dentro da área correta");
        Check(MatchRules.AddedTime([new(1, "injury", "pal", 1, "", 10), new(2, "substitution", "pal", 2, "", 100)], 0, 2700) == 120, "Acréscimos arredondam interrupções para minutos inteiros");

        var halfPenalty = MatchRules.EnsurePenaltyTime(2758, 2760, 5460); var finalPenalty = MatchRules.EnsurePenaltyTime(5518, 2760, 5520);
        Check(halfPenalty.HalfEnd == 2780 && halfPenalty.Duration == 5480, "Pênalti no fim do primeiro tempo adia o intervalo e preserva 45 minutos do segundo");
        Check(finalPenalty.Duration == 5540, "Pênalti nos acréscimos finais tem tempo para completar a cobrança");
        var clubs = new List<Club> { new("a", "A", "A", "#000000", 70), new("b", "B", "B", "#000000", 70) };
        var fairplay = new Fixture { Home = "a", Away = "b", HomeGoals = 0, AwayGoals = 0, Events = [new(1, "red_card", "a", 1, "")] };
        Check(LeagueRules.Table(clubs, [fairplay], "persisted")[0].Club.Id == "b", "Empate sem confronto completo considera menos cartões vermelhos");
        Check(JsonSerializer.Serialize(LeagueRules.Table(clubs, [], "persisted")) == JsonSerializer.Serialize(LeagueRules.Table(clubs, [], "persisted")), "Sorteio de desempate é estável para o mesmo save");
        var g = Game.New("pal"); var source = g.OpponentSquads.First(); var player = source.Value.First(p => p.Position != "GOL" && !p.Starter);
        long original = g.Balance, seller = g.ClubBalances[source.Key]; int count = g.Squad.Count;
        var loan = AdvancedMarket.Borrow(g, player.Id, AdvancedMarket.LoanFee(player));
        Check(g.Squad.Contains(player) && !source.Value.Contains(player) && g.Contracts[player.Id].ClubId == source.Key, "Empréstimo muda registro esportivo e preserva propriedade");
        Check(g.Balance + g.ClubBalances[source.Key] == original + seller, "Taxa de empréstimo conserva dinheiro entre clubes");
        Reject(() => AdvancedMarket.Borrow(g, player.Id, loan.Fee), "Bloqueia empréstimo duplicado");
        Check(!TransferMarket.Quote(g, player.Id, "renewal", 3).Available, "Emprestado não pode ser renovado como propriedade do destino");
        Reject(() => TransferMarket.List(g, player.Id, true), "Clube de destino não pode vender atleta emprestado");
        AdvancedMarket.ReturnLoans(g); AdvancedMarket.ReturnLoans(g);
        Check(g.Squad.Count == count && source.Value.Count(p => p.Id == player.Id) == 1 && loan.Returned, "Retorno de empréstimo é idempotente");

        g = Game.New("pal"); player = g.Squad.First(p => !p.Starter && p.Position != "GOL");
        var lent = AdvancedMarket.Lend(g, player.Id, g.Clubs.First(c => c.Id != g.ClubId).Id, AdvancedMarket.LoanFee(player));
        Check(!g.Squad.Contains(player) && g.Contracts[player.Id].ClubId == g.ClubId && AdvancedMarket.RegisteredCount(g, g.ClubId) == 23, "Empréstimo de saída preserva propriedade e reserva vaga de retorno");
        AdvancedMarket.ReturnLoans(g); Check(g.Squad.Contains(player) && lent.Returned, "Atleta cedido retorna ao elenco proprietário");
        g = Game.New("pal"); g.Balance = 500_000_000; g.Round = 23;
        player = g.OpponentSquads.First().Value.First(p => p.Position != "GOL" && !p.Starter);
        var quote = TransferMarket.Quote(g, player.Id, "inbound", 3);
        var offer = TransferMarket.Propose(g, player.Id, "inbound", quote.Fee, quote.Wage, 3, 24, 123_000_000);
        original = g.Balance; TransferMarket.Accept(g, offer.Id);
        Check(g.Balance == original - AdvancedMarket.Upfront(offer.Fee, 24) - offer.SigningBonus - offer.AgentCommission && g.TransferPayments.Count == 23, "Compra parcelada debita somente entrada e luvas e registra 23 obrigações");
        Check(g.TransferPayments.Sum(p => p.Amount) + AdvancedMarket.Upfront(offer.Fee, 24) == offer.Fee && g.TransferPayments.Any(p => p.DueSeason == 2027), "Parcelas conservam centavos e atravessam temporadas");
        Check(g.Contracts[player.Id].ReleaseClause == 123_000_000, "Nova multa fica registrada no contrato");
        g.Round++; AdvancedMarket.Settle(g); original = g.Balance; AdvancedMarket.Settle(g);
        Check(g.Balance == original && g.TransferPayments.Count(p => p.Paid) == 1, "Parcela vencida é liquidada uma única vez");

        g = Game.New("pal"); g.Balance = 500_000_000; player = g.OpponentSquads.First().Value.First(p => p.Position != "GOL" && !p.Starter);
        long clause = g.Contracts[player.Id].ReleaseClause; original = g.Balance;
        Reject(() => AdvancedMarket.Release(g, player.Id, clause + 1), "Multa desatualizada é rejeitada antes do débito");
        Check(g.Balance == original, "Rejeição da multa não movimenta caixa");
        AdvancedMarket.Release(g, player.Id, clause);
        Check(g.Squad.Contains(player) && g.Transfers[0].Fee == clause, "Pagamento da multa registra transferência pela taxa contratual");

        var rows = Calendar2026.Data.Fixtures;
        Check(rows.Count == 380 && rows.GroupBy(f => f.Round).All(r => r.Count() == 10) && rows.Any(f => f.Round == 1 && f.Home == "cam" && f.Away == "pal"), "Calendário CC0 tem 380 partidas e abertura Atlético-MG × Palmeiras");
        var catalog = Game.DemoCatalog(); var replacements = new Dictionary<string, string> { { "for", "cap" }, { "cea", "ctb" }, { "spo", "cha" }, { "juv", "rem" } };
        catalog.Clubs = catalog.Clubs.Select(c => replacements.TryGetValue(c.Id, out var id) ? c with { Id = id } : c).ToList();
        catalog.Players = catalog.Players.Select(p => replacements.TryGetValue(p.ClubId, out var id) ? p with { ClubId = id } : p).ToList();
        var scheduled = Game.New("pal", catalog);
        Check(scheduled.Fixtures.All(f => f.Date != null) && scheduled.CalendarSource == SeasonCalendar.Source, "Participantes de 2026 também recebem calendário gerado pelo jogo");
        Reject(() => Calendar2026.Apply(Game.New("pal")), "Calendário real não se aplica a participantes incompatíveis");

        g = Game.New("pal"); Divisions.EnableDemo(g); Game.Advance(g);
        Check(g.Fixtures.Count(f => f.HomeGoals != null) == 10 && g.Divisions!.OtherFixtures.Count(f => f.HomeGoals != null) == 10, "Avanço disputa dez jogos de cada divisão");
        foreach (var f in g.Fixtures) { f.HomeGoals = f.Home == g.ClubId ? 0 : 2; f.AwayGoals = f.Away == g.ClubId ? 0 : 2; }
        foreach (var f in g.Divisions!.OtherFixtures) { f.HomeGoals = 1; f.AwayGoals = 0; }
        g.Round = 39; Divisions.Complete(g); var promoted = g.Divisions.Promoted.ToArray(); var past = g.Fixtures[0].Setup;
        Check(promoted.Length == 4 && g.Divisions.Relegated.Count == 4 && g.Divisions.Playoffs.Count == 4, "Série B define dois acessos diretos e dois por quatro jogos de playoffs");
        Divisions.Complete(g); Check(g.Divisions.Playoffs.Count == 4, "Reabrir definição não duplica playoffs");
        g.Life!.Competitions.Clear(); TransferMarket.NextSeason(g);
        Check(g.Clubs.Count == 20 && g.Divisions!.OtherClubs.Count == 20 && g.Divisions.ManagedDivision == "B" && g.Clubs.Any(c => c.Id == g.ClubId), "Clube rebaixado continua gerenciável na Série B com vinte participantes");
        Check(g.PastSeasons[0].Divisions!.Promoted.SequenceEqual(promoted) && g.PastSeasons[0].Fixtures[0].Setup == past, "Virada preserva playoffs, acessos e snapshots antigos");
        Console.WriteLine("Gestão avançada, regras e divisões verificadas.");
    }
}
