namespace Brasileirao;

public static class AdvancedMarket
{
    static List<Player> Squad(GameState g, string club) => club == g.ClubId ? g.Squad : g.OpponentSquads.GetValueOrDefault(club) ?? throw new GameRuleException("Clube indisponível.");
    static long Cash(GameState g, string club) => club == g.ClubId ? g.Balance : g.ClubBalances.GetValueOrDefault(club);
    static void Pay(GameState g, string club, long amount, string text)
    {
        if (club == g.ClubId) { g.Balance += amount; g.Ledger.Add(new(g.Round, text, amount, g.Season)); }
        else g.ClubBalances[club] = Cash(g, club) + amount;
    }
    public static long Upfront(long fee, int installments) => (fee + installments - 1) / installments;
    public static int RegisteredCount(GameState g, string club) => Squad(g, club).Count + g.Loans.Count(l => !l.Returned && l.OwnerClub == club);
    public static void ValidateTerms(int installments, long clause, string kind, long fee)
    {
        if (installments is < 1 or > 24 || clause is < 0 or > 5_000_000_000L) throw new GameRuleException("Use 1–24 parcelas e multa entre zero e 5 bilhões.");
        if ((kind == "renewal" || fee == 0) && installments != 1) throw new GameRuleException("Renovações e agentes livres não têm taxa para parcelar.");
    }
    public static void SchedulePayments(GameState g, TransferOffer o, long fee, long upfront)
    {
        long remaining = fee - upfront;
        for (int i = 1; i < o.Installments; i++)
        {
            long amount = remaining / (o.Installments - i); remaining -= amount;
            int ordinal = g.Round - 1 + i;
            g.TransferPayments.Add(new() { PlayerId = o.PlayerId, Buyer = o.ToClub, Seller = o.FromClub, DueSeason = g.Season + ordinal / g.SeasonRounds, DueRound = ordinal % g.SeasonRounds + 1, Amount = amount });
        }
    }
    public static void Settle(GameState g)
    {
        foreach (var p in g.TransferPayments.Where(p => !p.Paid && (p.DueSeason < g.Season || p.DueSeason == g.Season && p.DueRound <= g.Round)))
        {
            Pay(g, p.Buyer, -p.Amount, "Parcela de transferência: " + p.PlayerId);
            if (p.Seller != "") Pay(g, p.Seller, p.Amount, "Recebimento de parcela: " + p.PlayerId);
            p.Paid = true;
        }
    }
    public static LoanDeal Borrow(GameState g, int playerId, long fee)
    {
        var (p, owner) = TransferMarket.Locate(g, playerId);
        if (owner == "" || owner == g.ClubId || !TransferMarket.Window(g).IsOpen || g.Round > g.SeasonRounds) throw new GameRuleException("Empréstimo exige outro clube e janela aberta.");
        if (g.Loans.Any(l => !l.Returned && l.PlayerId == p.Id) || g.Offers.Any(o => o.PlayerId == p.Id && o.Status is "pending" or "agreed" or "countered")) throw new GameRuleException("O atleta já tem empréstimo ou negociação em andamento.");
        if (RegisteredCount(g, g.ClubId) >= 40 || !TransferMarket.CanRelease(g, owner, p)) throw new GameRuleException("Confira o limite do destino e o elenco mínimo da origem.");
        long required = LoanFee(p);
        if (fee != required || g.Balance < required) throw new GameRuleException("Taxa de empréstimo alterada ou caixa insuficiente. Recarregue os termos.");
        var loan = new LoanDeal { PlayerId = p.Id, PlayerName = p.Name, OwnerClub = owner, BorrowerClub = g.ClubId, EndSeason = g.Season, Fee = fee };
        Pay(g, g.ClubId, -fee, "Taxa de empréstimo: " + p.Name); Pay(g, owner, fee, "Empréstimo: " + p.Name);
        Squad(g, owner).Remove(p); p.Starter = false; p.TransferListed = false; g.Squad.Add(p); g.Loans.Add(loan);
        TransferMarket.AutoLineup(Squad(g, owner)); Sports.RefreshBench(g);
        g.News.Insert(0, $"{p.Name} chega por empréstimo até o fim de {g.Season}. O clube assume o salário integral de R$ {p.Wage:N0} por rodada.");
        return loan;
    }
    public static long LoanFee(Player p) => Math.Max(50_000, p.Value / 20);
    public static LoanDeal Lend(GameState g, int playerId, string borrowerClub, long fee)
    {
        var p = g.Squad.Find(p => p.Id == playerId) ?? throw new GameRuleException("Atleta ausente do seu elenco.");
        if (!TransferMarket.Window(g).IsOpen || g.Round > g.SeasonRounds || borrowerClub == g.ClubId || !g.OpponentSquads.ContainsKey(borrowerClub)) throw new GameRuleException("Escolha outro clube durante a janela.");
        if (!TransferMarket.CanRelease(g, g.ClubId, p) || RegisteredCount(g, borrowerClub) >= 40) throw new GameRuleException("Confira o elenco mínimo da origem e as vagas reservadas do destino.");
        if (g.Loans.Any(l => !l.Returned && l.PlayerId == p.Id) || g.Offers.Any(o => o.PlayerId == p.Id && TransferMarket.Open(o))) throw new GameRuleException("Atleta com vínculo temporário ou negociação em andamento.");
        if (fee != LoanFee(p) || Cash(g, borrowerClub) < fee) throw new GameRuleException("Taxa alterada ou clube de destino sem saldo.");
        var loan = new LoanDeal { PlayerId = p.Id, PlayerName = p.Name, OwnerClub = g.ClubId, BorrowerClub = borrowerClub, EndSeason = g.Season, Fee = fee };
        Pay(g, borrowerClub, -fee, "Taxa de empréstimo: " + p.Name); Pay(g, g.ClubId, fee, "Empréstimo de saída: " + p.Name);
        g.Squad.Remove(p); p.Starter = false; p.TransferListed = false; Squad(g, borrowerClub).Add(p); g.Loans.Add(loan);
        TransferMarket.AutoLineup(g.Squad); Sports.RefreshBench(g); g.Substitutions.Clear();
        g.News.Insert(0, p.Name + " foi emprestado até o fim da temporada. O destino assume integralmente o salário; sua vaga de retorno fica reservada.");
        return loan;
    }
    public static void ReturnLoans(GameState g)
    {
        foreach (var l in g.Loans.Where(l => !l.Returned && l.EndSeason <= g.Season))
        {
            var borrower = Squad(g, l.BorrowerClub); var p = borrower.Find(p => p.Id == l.PlayerId) ?? throw new GameRuleException("Empréstimo sem atleta no clube de destino.");
            borrower.Remove(p); p.Starter = false; Squad(g, l.OwnerClub).Add(p); l.Returned = true;
            TransferMarket.AutoLineup(Squad(g, l.OwnerClub));
            g.News.Insert(0, p.Name + " retornou ao clube de origem ao fim do empréstimo.");
        }
    }
    public static TransferOffer Release(GameState g, int playerId, long expectedClause)
    {
        var (p, owner) = TransferMarket.Locate(g, playerId);
        if (!g.Contracts.TryGetValue(p.Id, out var contract) || !contract.Active || contract.ClubId != owner || contract.ReleaseClause <= 0 || contract.ReleaseClause != expectedClause) throw new GameRuleException("Multa rescisória indisponível ou alterada.");
        var quote = TransferMarket.Quote(g, playerId, "inbound", 3);
        if (!quote.Available) throw new GameRuleException(quote.Reason);
        if (g.Offers.Any(o => o.PlayerId == playerId && o.Status is "pending" or "agreed" or "countered")) throw new GameRuleException("Encerre a negociação atual antes de acionar a multa.");
        var offer = new TransferOffer { PlayerId = p.Id, PlayerName = p.Name, FromClub = owner, ToClub = g.ClubId, Fee = expectedClause, Wage = quote.Wage, Years = 3, SigningBonus = quote.SigningBonus, Season = g.Season, CreatedRound = g.Round, ExpiresRound = g.Round, ContractRevision = contract.Revision, Status = "agreed", Message = "Multa rescisória acionada." };
        // A multa substitui somente a exigência financeira do clube; salário e elegibilidade continuam validados.
        return TransferMarket.AcceptRelease(g, offer);
    }
}
