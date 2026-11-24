namespace Brasileirao;

public static class TransferMarket
{
    public static bool Open(TransferOffer o) => o.Status is "pending" or "agreed" or "countered";
    public static TransferWindow Window(GameState g) => g.Round switch
    {
        >= 1 and <= 6 => new(true, "Primeira janela", 6, 20, "Transferências entre clubes: rodadas 1–6 e 20–23. Agentes livres e renovações: toda a temporada. Regras demonstrativas."),
        >= 20 and <= 23 => new(true, "Segunda janela", 23, null, "Transferências entre clubes: rodadas 1–6 e 20–23. Agentes livres e renovações: toda a temporada. Regras demonstrativas."),
        < 20 => new(false, "Janela fechada", null, 20, "Próxima janela na rodada 20. Agentes livres e renovações continuam disponíveis."),
        _ => new(false, "Janela fechada", null, null, "Próxima janela no início da temporada seguinte. Agentes livres e renovações continuam disponíveis.")
    };
    public static void Upgrade(GameState g)
    {
        if (g.ManagementVersion >= 1) return;
        // Saves 0.1 possuíam IDs repetidos entre mercado e adversários. Preserva o
        // elenco controlado e os snapshots já disputados; só ajusta IDs adversários.
        var used = g.Squad.Concat(g.Market).Select(p => p.Id).ToHashSet();
        int next = Math.Max(20_000, used.DefaultIfEmpty(0).Max() + 1);
        foreach (var club in g.Clubs.Where(c => c.Id != g.ClubId))
        {
            if (!g.OpponentSquads.TryGetValue(club.Id, out var squad))
                g.OpponentSquads[club.Id] = squad = Game.Players(g.Clubs.IndexOf(club) * 100, club.Strength);
            foreach (var p in squad) { if (!used.Add(p.Id)) { while (used.Contains(next)) next++; p.Id = next++; used.Add(p.Id); } }
            g.ClubBalances.TryAdd(club.Id, (long)((30_000_000L + Math.Max(0, club.Strength - 60) * 2_000_000L) * Game.EconomyFactor(g.DataVersion)));
        }
        int index = 0;
        foreach (var p in g.Squad)
        {
            // Os dois goleiros iniciais não vencem juntos.
            int term = p.Position == "GOL" ? 1 + (index++ % 2) : index++ % 3;
            g.Contracts[p.Id] = new() { PlayerId = p.Id, ClubId = g.ClubId, StartSeason = g.Season, StartRound = 1, EndSeason = g.Season + term, Wage = p.Wage, ReleaseClause = (long)p.Value * 2 };
        }
        foreach (var (club, squad) in g.OpponentSquads)
            foreach (var p in squad) g.Contracts[p.Id] = new() { PlayerId = p.Id, ClubId = club, StartSeason = g.Season, StartRound = 1, EndSeason = g.Season + 2, Wage = p.Wage, ReleaseClause = (long)p.Value * 2 };
        g.ManagementVersion = 1;
    }
    public static (Player Player, string Club) Locate(GameState g, int id)
    {
        var p = g.Squad.Find(p => p.Id == id); if (p != null) return (p, g.ClubId);
        p = g.Market.Find(p => p.Id == id); if (p != null) return (p, "");
        foreach (var (club, squad) in g.OpponentSquads) { p = squad.Find(p => p.Id == id); if (p != null) return (p, club); }
        throw new GameRuleException("Jogador não está mais disponível nesta carreira.");
    }
    static int ContractRevision(GameState g, int player) => g.Contracts.TryGetValue(player, out var c) ? c.Revision : 0;
    static List<Player> Squad(GameState g, string club) => club == g.ClubId ? g.Squad : g.OpponentSquads[club];
    static long Balance(GameState g, string club) => club == g.ClubId ? g.Balance : g.ClubBalances.GetValueOrDefault(club);
    static void AdjustBalance(GameState g, string club, long amount) { if (club == g.ClubId) g.Balance += amount; else g.ClubBalances[club] = Balance(g, club) + amount; }
    public static long Bonus(int wage, string kind) => Math.Max(50_000L, (long)wage * (kind == "free" ? 6 : 4));
    static long AskingFee(GameState g, Player p, string club) => club == "" ? 0 : (long)Math.Ceiling(p.Value * (p.Starter ? 1.25m : 1.05m) / 50_000m) * 50_000;
    public static bool CanRelease(GameState g, string club, Player player)
    {
        if (g.Loans.Any(l => !l.Returned && l.PlayerId == player.Id)) return false;
        var squad = Squad(g, club);
        return squad.Count > 18 && (player.Position == "GOL" ? squad.Count(p => p.Position == "GOL") > 2 : squad.Count(p => p.Position != "GOL") > 16);
    }
    public static PlayerQuote Quote(GameState g, int id, string kind, int years)
    {
        var (p, club) = Locate(g, id);
        if (kind is not ("inbound" or "renewal")) throw new GameRuleException("Tipo de negociação inválido.");
        if (kind == "renewal" && club != g.ClubId || kind == "inbound" && club == g.ClubId) throw new GameRuleException("Vínculo incompatível com esta negociação.");
        int minimum = kind == "renewal" && g.Contracts.TryGetValue(id, out var contract) ? Math.Max(1, contract.EndSeason - g.Season + 2) : g.Round > g.SeasonRounds ? 2 : 1;
        if (years is < 1 or > 5) throw new GameRuleException("Escolha um contrato de uma a cinco temporadas.");
        string reason = "";
        if (g.Loans.Any(l => !l.Returned && l.PlayerId == id)) reason = "O atleta tem empréstimo ativo; aguarde o retorno antes de negociar.";
        if (years < minimum) reason = kind == "renewal" ? "A renovação deve estender o vencimento atual. Selecione mais temporadas." : "A temporada já terminou. Escolha ao menos duas temporadas para cobrir o próximo ano.";
        if (minimum > 5) reason = "O contrato já cobre as cinco temporadas permitidas. Renove em uma temporada futura.";
        if (kind == "inbound" && AdvancedMarket.RegisteredCount(g, g.ClubId) >= 40) reason = "O elenco já atingiu o limite de 40 jogadores.";
        if (kind == "inbound" && club != "" && !Window(g).IsOpen) reason = "A janela de transferências entre clubes está fechada.";
        if (kind == "inbound" && club != "" && !CanRelease(g, club, p)) reason = "O clube não pode liberar o jogador sem comprometer o elenco mínimo.";
        var multiplier = (kind == "renewal" ? 1.10m : 1.08m) * (years >= 4 ? 1.05m : 1);
        multiplier*=1+((g.Life?.Minds.GetValueOrDefault(id)?.Ambition??60)-60)/600m;
        var demand = (int)Math.Min(10_000_000, Math.Ceiling(Math.Max(2_000, p.Wage * multiplier) / 1_000m) * 1_000);
        var fee = kind == "renewal" ? 0 : AskingFee(g, p, club);
        return new(id, p.Name, club, kind, fee, demand, Bonus(demand, club == "" ? "free" : kind), years, minimum, reason == "", reason,ClubLife.AgentFee(g,id,fee,demand),ClubLife.AgentName(g,id));
    }
    static void ValidateTerms(long fee, int wage, int years)
    {
        if (fee is < 0 or > 5_000_000_000L || wage is < 1_000 or > 10_000_000 || years is < 1 or > 5)
            throw new GameRuleException("Confira a taxa (0 a 5 bilhões), salário por rodada (mil a 10 milhões) e duração (1 a 5 temporadas).");
    }
    public static TransferOffer Propose(GameState g, int id, string kind, long fee, int wage, int years, int installments = 1, long releaseClause = 0)
    {
        ValidateTerms(fee, wage, years);
        AdvancedMarket.ValidateTerms(installments, releaseClause, kind, fee);
        var q = Quote(g, id, kind, years); if (!q.Available) throw new GameRuleException(q.Reason);
        if ((q.FromClub == "" || kind == "renewal") && fee != 0) throw new GameRuleException("Agentes livres e renovações não têm taxa de transferência.");
        if (g.Offers.Any(o => o.PlayerId == id && Open(o))) throw new GameRuleException("Já existe uma negociação aberta por este jogador. Conclua ou encerre a proposta.");
        var bonus = Bonus(wage, q.FromClub == "" ? "free" : kind);
        if (g.Balance < AdvancedMarket.Upfront(fee, installments) + bonus+ClubLife.AgentFee(g,id,fee,wage)) throw new GameRuleException("Saldo insuficiente para taxa e luvas da proposta.");
        var next = g.Round + 1;
        bool immediate = kind == "renewal" || q.FromClub == "" || g.Round >= g.SeasonRounds || !Window(new() { Round = next }).IsOpen;
        var offer = new TransferOffer { AgentCommission=ClubLife.AgentFee(g,id,fee,wage),AgentName=ClubLife.AgentName(g,id),Installments = installments, ReleaseClause = releaseClause, PlayerId = id, PlayerName = q.PlayerName, Kind = kind, FromClub = q.FromClub, ToClub = g.ClubId, Fee = fee, Wage = wage, Years = years, SigningBonus = bonus, Season = g.Season, CreatedRound = g.Round, ResponseRound = immediate ? g.Round : next, ExpiresRound = Math.Min(g.SeasonRounds + 1, g.Round + 3), ContractRevision = ContractRevision(g, id), Message = "Proposta enviada. A resposta chegará ao avançar uma rodada." };
        g.Offers.Insert(0, offer);
        if (immediate) Respond(g, offer);
        g.News.Insert(0, $"Negociação: proposta por {q.PlayerName}. {offer.Message}");
        return offer;
    }
    static void Respond(GameState g, TransferOffer offer)
    {
        var q = Quote(g, offer.PlayerId, offer.Kind, offer.Years);
        if (!q.Available) { offer.Status = "expired"; offer.Message = q.Reason; return; }
        if (offer.Fee < q.Fee * .8m || offer.Wage < q.Wage * .8m) { offer.Status = "rejected"; offer.Message = "A proposta ficou abaixo das exigências do clube ou do atleta."; return; }
        if (offer.Fee < q.Fee || offer.Wage < q.Wage)
        {
            offer.Status = "countered"; offer.CounterFee = Math.Max(offer.Fee, q.Fee); offer.CounterWage = Math.Max(offer.Wage, q.Wage);
            offer.SigningBonus = Bonus(offer.CounterWage.Value, offer.FromClub == "" ? "free" : offer.Kind);
            offer.AgentCommission=ClubLife.AgentFee(g,offer.PlayerId,offer.CounterFee.Value,offer.CounterWage.Value);
            offer.Message = "Contraproposta recebida. Confira taxa, salário e luvas antes de aceitar.";
        }
        else { offer.Status = "agreed"; offer.Message = "Clube e jogador aceitaram os termos. A confirmação final ainda depende de você."; }
    }
    public static void List(GameState g, int id, bool listed)
    {
        var p = g.Squad.Find(p => p.Id == id) ?? throw new GameRuleException("Jogador não pertence ao seu elenco.");
        if (g.Loans.Any(l => !l.Returned && l.PlayerId == id)) throw new GameRuleException("Atleta emprestado não pode ser colocado à venda pelo destino.");
        p.TransferListed = listed;
        g.News.Insert(0, listed ? $"{p.Name} foi colocado na lista de transferências." : $"{p.Name} foi retirado da lista de transferências.");
    }
    public static void Withdraw(GameState g, Guid id)
    {
        var offer = g.Offers.Find(o => o.Id == id) ?? throw new GameRuleException("Proposta não encontrada.");
        if (!Open(offer)) throw new GameRuleException("Essa negociação já foi encerrada.");
        offer.Status = "withdrawn"; offer.Message = "Negociação encerrada pelo treinador. Nenhum valor foi movimentado.";
    }
    public static TransferOffer Accept(GameState g, Guid id) => AcceptCore(g, id, false);
    public static TransferOffer AcceptRelease(GameState g, TransferOffer offer)
    {
        // AcceptCore validates before financial mutation; remove transient offer on rejection.
        g.Offers.Insert(0, offer);
        try { return AcceptCore(g, offer.Id, true); } catch { g.Offers.Remove(offer); throw; }
    }
    static TransferOffer AcceptCore(GameState g, Guid id, bool release)
    {
        var o = g.Offers.Find(o => o.Id == id) ?? throw new GameRuleException("Proposta não encontrada.");
        if (o.Status is not ("agreed" or "countered")) throw new GameRuleException("Aguarde a resposta ou escolha uma negociação ainda disponível.");
        if (o.Season != g.Season || g.Round > o.ExpiresRound) throw new GameRuleException("A proposta expirou.");
        var (p, club) = Locate(g, o.PlayerId);
        if (g.Loans.Any(l => !l.Returned && l.PlayerId == p.Id)) throw new GameRuleException("Atleta com empréstimo ativo não pode ser transferido.");
        if (club != o.FromClub || ContractRevision(g, p.Id) != o.ContractRevision) throw new GameRuleException("O vínculo do jogador mudou. Envie uma nova proposta.");
        long fee = o.CounterFee ?? o.Fee; int wage = o.CounterWage ?? o.Wage;
        ValidateTerms(fee, wage, o.Years);
        if (o.Kind != "renewal" && club != "" && !Window(g).IsOpen) throw new GameRuleException("A janela fechou antes da conclusão da transferência.");
        if (o.Kind != "renewal" && club != "" && !CanRelease(g, club, p)) throw new GameRuleException("O clube de origem precisa manter o elenco mínimo.");
        if (o.ToClub != g.ClubId && (!g.OpponentSquads.ContainsKey(o.ToClub) || g.OpponentSquads[o.ToClub].Count + g.Loans.Count(l => !l.Returned && l.OwnerClub == o.ToClub) >= 40)) throw new GameRuleException("O clube interessado não pode registrar outro jogador.");
        if (o.Kind != "outbound")
        {
            var q = Quote(g, p.Id, o.Kind, o.Years); if (!q.Available) throw new GameRuleException(q.Reason);
            if ((!release && fee < q.Fee) || wage < q.Wage) throw new GameRuleException("As condições do jogador mudaram. Envie uma nova proposta.");
        }
        long bonus = Bonus(wage, club == "" ? "free" : o.Kind);
        AdvancedMarket.ValidateTerms(o.Installments, o.ReleaseClause, o.Kind, fee);
        long upfront = AdvancedMarket.Upfront(fee, o.Installments);
        long commission=ClubLife.AgentFee(g,p.Id,fee,wage);
        if(o.ToClub==g.ClubId&&g.Life!=null){ClubLife.EnsureEmployed(g);if(fee>g.Life.Board.TransferBudget)throw new GameRuleException("A transferência excede o orçamento aprovado pela diretoria.");if(g.Squad.Sum(p=>(long)p.Wage)-(o.Kind=="renewal"?p.Wage:0)+wage+g.Life.Staff.Sum(s=>(long)s.Wage)>g.Life.Board.WageBudget)throw new GameRuleException("A contratação excede o teto salarial da diretoria.");}
        if (Balance(g, o.ToClub) < upfront + bonus+commission) throw new GameRuleException(o.ToClub == g.ClubId ? "Seu caixa não comporta a taxa e as luvas. Reforce as finanças antes de confirmar." : "O clube comprador não tem saldo para concluir a operação.");
        // Todas as verificações precedem a primeira mutação financeira/de elenco.
        AdjustBalance(g, o.ToClub, -upfront - bonus-commission);
        o.AgentCommission=commission;o.AgentName=ClubLife.AgentName(g,p.Id);
        if(o.ToClub==g.ClubId&&g.Life!=null){g.Life.Board.TransferBudget-=fee;g.Ledger.Add(new(g.Round,"Comissão empresarial: "+o.AgentName,-commission,g.Season));}
        if (o.Kind != "renewal")
        {
            if (club == "") g.Market.Remove(p); else { Squad(g, club).Remove(p); AdjustBalance(g, club, upfront); }
            p.Starter = false; p.TransferListed = false; Squad(g, o.ToClub).Add(p);
            if (club != "" && club != g.ClubId) AutoLineup(Squad(g, club));
        }
        p.Wage = wage;
        var contract = new PlayerContract { PlayerId = p.Id, ClubId = o.ToClub, StartSeason = g.Season, StartRound = g.Round, EndSeason = g.Season + o.Years - 1, Wage = wage, SigningBonus = bonus, Revision = o.ContractRevision + 1, ReleaseClause = o.ReleaseClause };
        g.Contracts[p.Id] = contract;
        AdvancedMarket.SchedulePayments(g, o, fee, upfront);
        if (o.ToClub == g.ClubId) { if (fee > 0) g.Ledger.Add(new(g.Round, "Transferência: " + p.Name, -upfront, g.Season)); g.Ledger.Add(new(g.Round, (o.Kind == "renewal" ? "Luvas de renovação: " : "Luvas de contratação: ") + p.Name, -bonus, g.Season)); }
        else g.Ledger.Add(new(g.Round, "Venda para " + g.Clubs.Concat(g.Divisions?.OtherClubs ?? []).Concat(g.Life?.ExtraClubs??[]).First(c => c.Id == o.ToClub).Name + ": " + p.Name, upfront, g.Season));
        g.Transfers.Insert(0, new(g.Season, g.Round, p.Id, p.Name, club, o.ToClub, fee, bonus, wage, contract.EndSeason, o.Kind));
        o.Fee = fee; o.Wage = wage; o.SigningBonus = bonus; o.Status = "completed"; o.Message = o.Kind == "renewal" ? "Contrato renovado e luvas pagas." : "Transferência concluída e contrato registrado.";
        foreach (var other in g.Offers.Where(x => x.Id != o.Id && x.PlayerId == p.Id && Open(x))) { other.Status = "expired"; other.Message = "O vínculo do atleta foi alterado em outra negociação."; }
        g.News.Insert(0, $"{p.Name}: {o.Message} Vencimento ao fim de {contract.EndSeason}.");
        return o;
    }
    public static void AutoLineup(List<Player> squad)
    {
        var selected = squad.Where(p => Sports.Eligible(p) && p.Position == "GOL").OrderByDescending(p => p.Rating).Take(1)
            .Concat(squad.Where(p => Sports.Eligible(p) && p.Position != "GOL").OrderByDescending(p => p.Rating).Take(10)).ToHashSet();
        foreach (var p in squad) p.Starter = selected.Contains(p);
    }
    public static IEnumerable<MarketCandidate> Candidates(GameState g)
    {
        foreach (var p in g.Market) { var q = Quote(g, p.Id, "inbound", 3); yield return new(p, "", 0, q.Wage, null, q.Available, q.Reason); }
        foreach (var (club, squad) in g.OpponentSquads)
            foreach (var p in squad) { var q = Quote(g, p.Id, "inbound", 3); yield return new(p, club, q.Fee, q.Wage, g.Contracts.GetValueOrDefault(p.Id)?.EndSeason, q.Available, q.Reason); }
    }
    public static void AfterRound(GameState g)
    {
        foreach (var (club, squad) in g.OpponentSquads) g.ClubBalances[club] = Balance(g, club) + 2_100_000L - squad.Sum(p => (long)p.Wage);
        foreach (var o in g.Offers.Where(Open).ToArray())
        {
            if (o.Season != g.Season || g.Round > o.ExpiresRound || (o.Kind != "outbound" && g.Round > g.SeasonRounds && o.Season + o.Years - 1 <= g.Season) || (o.Kind != "renewal" && o.FromClub != "" && !Window(g).IsOpen))
            { o.Status = "expired"; o.Message = "Prazo ou janela de transferências encerrados."; continue; }
            if (o.Status == "pending" && g.Round >= o.ResponseRound) { Respond(g, o); g.News.Insert(0, $"Resposta sobre {o.PlayerName}: {o.Message}"); }
        }
        if (!Window(g).IsOpen) return;
        foreach (var p in g.Squad.Where(p => p.TransferListed).ToArray())
        {
            if (!CanRelease(g, g.ClubId, p) || g.Offers.Any(o => o.PlayerId == p.Id && (Open(o) || o.Season == g.Season && g.Round - o.CreatedRound < 3))) continue;
            long fee = (long)(p.Value * (.85m + (p.Id % 4) * .05m) / 50_000) * 50_000;
            int wage = (int)Math.Ceiling(Math.Max(2000, p.Wage * 1.08m) / 1000) * 1000;
            var buyer = g.Clubs.Where(c => c.Id != g.ClubId && Squad(g, c.Id).Count < 40 && Balance(g, c.Id) >= fee + Bonus(wage, "outbound"))
                .OrderBy(c => c.Id, StringComparer.Ordinal).ElementAtOrDefault((p.Id + g.Round) % Math.Max(1, g.Clubs.Count - 1))
                ?? g.Clubs.FirstOrDefault(c => c.Id != g.ClubId && Squad(g, c.Id).Count < 40 && Balance(g, c.Id) >= fee + Bonus(wage, "outbound"));
            if (buyer == null) continue;
            var offer = new TransferOffer { Kind = "outbound", PlayerId = p.Id, PlayerName = p.Name, FromClub = g.ClubId, ToClub = buyer.Id, Fee = fee, Wage = wage, Years = 3, SigningBonus = Bonus(wage, "outbound"), Season = g.Season, CreatedRound = g.Round, ResponseRound = g.Round, ExpiresRound = Math.Min(g.SeasonRounds + 1, g.Round + 3), ContractRevision = ContractRevision(g, p.Id), Status = "agreed", Message = "Proposta de compra recebida. O comprador aguarda sua decisão." };
            g.Offers.Insert(0, offer); g.News.Insert(0, $"{buyer.Name} apresentou uma proposta por {p.Name}.");
        }
    }
    public static void NextSeason(GameState g)
    {
        if (g.Round <= g.SeasonRounds) throw new GameRuleException($"Conclua as {g.SeasonRounds} rodadas primeiro.");
        if (g.Season >= 9999) throw new GameRuleException("O calendário suporta temporadas até o ano 9999.");
        if(ClubCompetitions.NextDue(g)!=null)throw new GameRuleException("Conclua as partidas eliminatórias pendentes antes da próxima temporada.");
        Divisions.Complete(g);
        AdvancedMarket.ReturnLoans(g);
        var standings = Game.Table(g).Select(x => System.Text.Json.JsonSerializer.SerializeToElement(x, Game.Json)).Select(x => new SeasonStanding(x.GetProperty("club").GetProperty("id").GetString()!, x.GetProperty("points").GetInt32(), x.GetProperty("played").GetInt32(), x.GetProperty("wins").GetInt32(), x.GetProperty("draws").GetInt32(), x.GetProperty("losses").GetInt32(), x.GetProperty("goals").GetInt32(), x.GetProperty("against").GetInt32(), x.GetProperty("difference").GetInt32())).ToList();
        g.PastSeasons.Add(new() { Divisions = g.Divisions, Clubs = [.. g.Clubs], Season = g.Season, Champion = standings[0].ClubId, Table = standings, Fixtures = g.Fixtures, PlayerStatsFromRound = g.PlayerStatsFromRound, PlayerStats = g.PlayerStats.Values.Where(p => g.Clubs.Any(c => c.Id == p.ClubId)).ToList() });
        g.History.Add($"Temporada {g.Season}: campeão {g.Clubs.Find(c => c.Id == standings[0].ClubId)!.Name}.");
        foreach (var o in g.Offers.Where(Open)) { o.Status = "expired"; o.Message = "Temporada encerrada."; }
        foreach (var p in g.Squad.ToArray())
        {
            var contract = g.Contracts[p.Id];
            if (contract.EndSeason > g.Season) continue;
            g.Squad.Remove(p); p.Starter = false; p.TransferListed = false; g.Market.Add(p); contract.Active = false; contract.Revision++;
            g.News.Insert(0, $"{p.Name} deixou o clube ao término do contrato, sem taxa de transferência.");
            g.Transfers.Insert(0, new(g.Season, g.SeasonRounds + 1, p.Id, p.Name, g.ClubId, "", 0, 0, 0, g.Season, "expired"));
        }
        foreach (var (club, squad) in g.OpponentSquads)
            foreach (var p in squad) { var contract = g.Contracts[p.Id]; if (contract.EndSeason <= g.Season) { contract.EndSeason = g.Season + 3; contract.Revision++; } }
        SeriesMovement.Apply(g, standings);
        Divisions.NextSeason(g);
        g.Season++; g.Round = 1; g.Fixtures = SeasonCalendar.Generate(g.Clubs, g.Season, g.LotterySeed, g.Divisions?.ManagedDivision ?? "A", g.LeagueStart, SeriesFormats.SingleRound(g)); g.CalendarSource = SeasonCalendar.Source; g.CalendarVersion = 2;
        AdvancedMarket.Settle(g);
        foreach (var p in g.Squad.Concat(g.Market).Concat(g.OpponentSquads.Values.SelectMany(s => s))) p.YellowCards = 0;
        g.PlayerStats.Clear(); g.PlayerStatsFromRound = 1; g.Substitutions.Clear();
        foreach (var p in g.Squad.Concat(g.Market).Concat(g.OpponentSquads.Values.SelectMany(p => p))) { p.Age++; p.Fitness = 100; }
        AutoLineup(g.Squad);
        Sports.RefreshBench(g);
        Youth.NewSeason(g);
        ClubLife.NewSeason(g);
    }
}
