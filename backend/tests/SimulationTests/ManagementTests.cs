using System.Text.Json;
using Brasileirao;

public static class ManagementTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    static void Reject(Action action, string message) { try { action(); throw new Exception("Operação inválida aceita: " + message); } catch (GameRuleException) { Console.WriteLine("PASS: " + message); } }
    static string Snapshot(GameState g) => JsonSerializer.Serialize(g, Game.Json);
    public static void Run()
    {
        var g = Game.New("pal");
        Check(g.ManagementVersion == 1 && g.Contracts.Count == g.Squad.Count+g.OpponentSquads.Values.Sum(s=>s.Count) && g.ClubBalances.Count == g.OpponentSquads.Count, "Contratos e caixa de 20 clubes inicializados por carreira");
        var free = g.Market[0]; var balance = g.Balance; var q = TransferMarket.Quote(g, free.Id, "inbound", 3);
        Check(q.Fee == 0 && q.SigningBonus > 0, "Agente livre não cobra taxa, mas negocia luvas e salário");
        var offer = TransferMarket.Propose(g, free.Id, "inbound", 0, q.Wage, 3);
        Check(offer.Status == "agreed" && g.Balance == balance && g.Market.Contains(free), "Proposta aceita não movimenta dinheiro antes da confirmação");
        TransferMarket.Accept(g, offer.Id);
        Check(g.Squad.Contains(free) && !g.Market.Contains(free) && g.Balance == balance - offer.SigningBonus - offer.AgentCommission && g.Contracts[free.Id].Wage == offer.Wage, "Contratação debita luvas uma vez e registra vínculo");
        var after = Snapshot(g); Reject(() => TransferMarket.Accept(g, offer.Id), "Dupla confirmação é recusada"); Check(Snapshot(g) == after, "Dupla confirmação não altera caixa ou elenco");
        var lastFixture = g.Fixtures[0]; Game.Simulate(g, lastFixture); var replay = JsonSerializer.Serialize(MatchSimulation.Run(lastFixture.Setup!, lastFixture.Seed, true));
        var p = g.Squad.First(p => p.Position == "MEI"); var currentEnd = g.Contracts[p.Id].EndSeason;
        int years = Math.Max(2, currentEnd - g.Season + 2); q = TransferMarket.Quote(g, p.Id, "renewal", years);
        balance = g.Balance; var renewal = TransferMarket.Propose(g, p.Id, "renewal", 0, q.Wage, years); TransferMarket.Accept(g, renewal.Id);
        Check(g.Contracts[p.Id].EndSeason > currentEnd && g.Balance == balance - renewal.SigningBonus - renewal.AgentCommission, "Renovação estende vencimento, atualiza salário e paga luvas");
        Check(JsonSerializer.Serialize(MatchSimulation.Run(lastFixture.Setup!, lastFixture.Seed, true)) == replay, "Renovação não altera replay já disputado");
        var invalid = Snapshot(g); Reject(() => TransferMarket.Propose(g, p.Id, "renewal", 0, 100_000, 1), "Renovação não encurta contrato"); Check(Snapshot(g) == invalid, "Proposta inválida não modifica o save");
        var another = g.Market.First(); q = TransferMarket.Quote(g, another.Id, "inbound", 3);
        var counter = TransferMarket.Propose(g, another.Id, "inbound", 0, (int)(q.Wage * .9m), 3);
        Check(counter.Status == "countered" && counter.CounterWage == q.Wage, "Oferta salarial intermediária recebe contraproposta");
        balance = g.Balance; TransferMarket.Accept(g, counter.Id); Check(g.Balance == balance - TransferMarket.Bonus(q.Wage, "free") - counter.AgentCommission && another.Wage == q.Wage, "Contraproposta usa salário e luvas autorizados pelo servidor");
        var rejectedPlayer = g.Market.First(); q = TransferMarket.Quote(g, rejectedPlayer.Id, "inbound", 3); balance = g.Balance;
        var rejected = TransferMarket.Propose(g, rejectedPlayer.Id, "inbound", 0, 1000, 3);
        Check(rejected.Status == "rejected" && g.Balance == balance, "Proposta muito baixa é recusada sem cobrança");
        var foreign = g.OpponentSquads.First(); var target = foreign.Value.First(p => p.Position != "GOL" && !p.Starter);
        q = TransferMarket.Quote(g, target.Id, "inbound", 3); balance = g.Balance; var sellerBalance = g.ClubBalances[foreign.Key];
        var pending = TransferMarket.Propose(g, target.Id, "inbound", q.Fee, q.Wage, 3);
        Check(pending.Status == "pending", "Negociação com clube aguarda resposta da rodada");
        Reject(() => TransferMarket.Accept(g, pending.Id), "Proposta sem resposta não pode ser concluída");
        Game.Advance(g); Check(pending.Status == "agreed", "Clube responde ao avançar a rodada");
        balance = g.Balance; sellerBalance = g.ClubBalances[foreign.Key]; TransferMarket.Accept(g, pending.Id);
        Check(!foreign.Value.Contains(target) && g.Squad.Contains(target) && g.Balance == balance - q.Fee - pending.SigningBonus - pending.AgentCommission && g.ClubBalances[foreign.Key] == sellerBalance + q.Fee, "Transferência move atleta e conserva a taxa entre os caixas dos clubes");
        var sale = g.Squad.First(p => !p.Starter && p.Position != "GOL"); TransferMarket.List(g, sale.Id, true); Game.Advance(g);
        var outbound = g.Offers.First(o => o.PlayerId == sale.Id && o.Kind == "outbound" && o.Status == "agreed");
        balance = g.Balance; var buyer = g.ClubBalances[outbound.ToClub]; TransferMarket.Accept(g, outbound.Id);
        Check(!g.Squad.Contains(sale) && g.OpponentSquads[outbound.ToClub].Contains(sale) && g.Balance == balance + outbound.Fee && g.ClubBalances[outbound.ToClub] == buyer - outbound.Fee - outbound.SigningBonus - outbound.AgentCommission, "Venda tem comprador real e não cria dinheiro sem contraparte");
        g.Round = 7; var available = g.OpponentSquads.First().Value.First(p => p.Position != "GOL"); q = TransferMarket.Quote(g, available.Id, "inbound", 3);
        Check(!q.Available, "Janela fechada bloqueia negociação entre clubes");
        Reject(() => TransferMarket.Propose(g, available.Id, "inbound", q.Fee, q.Wage, 3), "Servidor aplica o fechamento da janela");
        var lateFree = g.Market.First(); q = TransferMarket.Quote(g, lateFree.Id, "inbound", 3); Check(q.Available, "Agentes livres continuam negociáveis com janela fechada");
        var late = TransferMarket.Propose(g, lateFree.Id, "inbound", 0, q.Wage, 3); balance = g.Balance; g.Balance = 0; var noMoney = Snapshot(g);
        Reject(() => TransferMarket.Accept(g, late.Id), "Saldo é revalidado no fechamento"); Check(Snapshot(g) == noMoney, "Falta de saldo não executa transferência parcial"); g.Balance = balance;
        TransferMarket.Withdraw(g, late.Id); Check(late.Status == "withdrawn", "Proposta pode ser encerrada sem custo");
        var keeperTeam = Game.New("pal"); var keeper = keeperTeam.Squad.First(p => p.Position == "GOL"); Check(!TransferMarket.CanRelease(keeperTeam, keeperTeam.ClubId, keeper), "Elenco mantém ao menos dois goleiros");
        var nearClose = Game.New("pal"); nearClose.Round = 6; var candidate = nearClose.OpponentSquads.First().Value.First(p => p.Position != "GOL" && !p.Starter); q = TransferMarket.Quote(nearClose, candidate.Id, "inbound", 3);
        var atClose = TransferMarket.Propose(nearClose, candidate.Id, "inbound", q.Fee, q.Wage, 3); Check(atClose.Status == "agreed", "Última rodada da janela recebe resposta imediata");
        Game.Advance(nearClose); Check(atClose.Status == "expired", "Proposta não concluída expira quando a janela fecha");
        var offseason = Game.New("pal"); offseason.Round = 39; var offseasonPlayer = offseason.Market[0];
        Check(!TransferMarket.Quote(offseason, offseasonPlayer.Id, "inbound", 1).Available && TransferMarket.Quote(offseason, offseasonPlayer.Id, "inbound", 2).Available, "Contrato no intervalo entre temporadas precisa cobrir o ano seguinte");
        var end = Game.New("pal"); for (int i = 0; i < 38; i++) Game.Advance(end);
        var expiring = end.Squad.Where(p => end.Contracts[p.Id].EndSeason == end.Season).ToArray(); var oldFixtures = JsonSerializer.Serialize(end.Fixtures); balance = end.Balance; var ledgerBefore = end.Ledger.Count;
        end.Life!.Competitions.Clear(); TransferMarket.NextSeason(end);
        Check(end.Season == 2027 && end.Round == 1 && end.PastSeasons.Count == 1 && end.PastSeasons[0].Table.All(t => t.Played == 38), "Nova temporada arquiva classificação completa");
        Check(JsonSerializer.Serialize(end.PastSeasons[0].Fixtures) == oldFixtures && end.Fixtures.All(f => f.HomeGoals == null), "Partidas antigas preservadas e novo calendário independente");
        Check(expiring.All(p => !end.Squad.Contains(p) && end.Market.Contains(p) && !end.Contracts[p.Id].Active) && end.Ledger.Skip(ledgerBefore).All(l => l.Amount <= 0) && end.Balance == balance + end.Ledger.Skip(ledgerBefore).Sum(l => l.Amount), "Vencimentos liberam atletas sem receita fictícia ou reinício de caixa");
        var legacy = Game.New("pal"); legacy.ManagementVersion = 0; legacy.Contracts.Clear(); legacy.ClubBalances.Clear(); legacy.OpponentSquads.Clear(); legacy.Market = Game.Players(100).Take(12).ToList(); var oldCash = legacy.Balance;
        var career = new Career { State = Snapshot(legacy) }; var upgraded = Game.Read(career);
        var all = upgraded.Squad.Concat(upgraded.Market).Concat(upgraded.OpponentSquads.Values.SelectMany(s => s)).ToArray();
        Check(all.Select(p => p.Id).Distinct().Count() == all.Length && upgraded.Balance == oldCash, "Save legado ganha contratos sem duplicar IDs nem alterar caixa");
        Console.WriteLine("Testes de contratos, transferências e virada de temporada passaram.");
    }
}
