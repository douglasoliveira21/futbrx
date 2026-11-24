using System.Security.Cryptography;
using System.Text;
namespace Brasileirao;

public static class ClubLife
{
    public static readonly string[] Roles=["Auxiliar","Preparador","Analista","Fisioterapeuta","Olheiro","Coordenador da base"];
    public static int Seed(GameState g,string key)=>BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(g.LotterySeed+":"+key)),0)&int.MaxValue;
    static IEnumerable<Player> Players(GameState g)=>g.Squad.Concat(g.Academy).Concat(g.Market).Concat(g.OpponentSquads.Values.SelectMany(s=>s)).Concat(g.YouthProspects.Select(p=>p.Player)).Concat(g.Life?.Trials.Select(p=>p.Player)??[]);
    public static void Upgrade(GameState g)
    {
        if(g.Life==null){
            g.Life=new(){Season=g.Season};
            ResetBoard(g);
            g.Life.Fans.RivalId=g.Clubs.Where(c=>c.Id!=g.ClubId).OrderBy(c=>Seed(g,"rival:"+g.ClubId+":"+c.Id)).First().Id;
            g.Life.Staff=Roles.Select((r,i)=>new StaffMember("initial-"+i,new[]{"André Santos","Marcos Leal","Caio Ribeiro","Renata Alves","João Duarte","Ana Moraes"}[i],r,55,6000)).ToList();
            ClubCompetitions.NewSeason(g);
        }
        foreach(var p in Players(g).DistinctBy(p=>p.Id)){
            var random=new Random(Seed(g,"mind:"+p.Id));
            g.Life.Minds.TryAdd(p.Id,new(){Ambition=random.Next(40,96),Leadership=random.Next(25,96),Personality=new[]{"Equilibrado","Competitivo","Temperamental","Resiliente"}[random.Next(4)],Morale=random.Next(55,76)});
            g.Life.HiddenPotential.TryAdd(p.Id,Math.Clamp(p.Rating+(p.Age<=20?random.Next(8,26):p.Age<=25?random.Next(2,12):0),p.Rating,99));
            g.Life.HiddenPotential[p.Id]=Math.Max(g.Life.HiddenPotential[p.Id],p.Rating);
            g.Life.Medical.TryAdd(p.Id,new(){PreviousDays=p.InjuryDays});
        }
        if(!g.Squad.Any(p=>p.Id==g.Life.Board.CaptainId))g.Life.Board.CaptainId=g.Squad.MaxBy(p=>g.Life.Minds[p.Id].Leadership)?.Id??-1;
        foreach(var offer in g.Offers.Where(TransferMarket.Open)){offer.AgentName=AgentName(g,offer.PlayerId);if(offer.ToClub==g.ClubId)offer.AgentCommission=AgentFee(g,offer.PlayerId,offer.CounterFee??offer.Fee,offer.CounterWage??offer.Wage);}
        if(g.Life.CalendarVersion!=g.CalendarVersion){
            foreach(var match in g.Life.Competitions.SelectMany(c=>c.Matches).Where(m=>m.Fixture.HomeGoals==null&&m.Fixture.Setup==null&&m.Id!=g.LiveMatch?.CompetitionMatchId)){
                var day=(g.Fixtures.FirstOrDefault(f=>f.Round==match.Fixture.Round)?.Date??SeasonCalendar.DateOfRound(g.Season,match.Fixture.Round)).AddDays(-3);match.Fixture.Date=day;match.Fixture.Kickoff=new(day.Year,day.Month,day.Day,20,0,0,TimeSpan.FromHours(-3));
            }
            g.Life.CalendarVersion=g.CalendarVersion;
        }
        Economy(g);
        CoachCareer.Upgrade(g);
    }
    // Série B and C clubs are measured against their own league: the four strongest aim for promotion.
    static int BoardTarget(GameState g)
    {
        var club=g.Clubs.Find(c=>c.Id==g.ClubId)!;
        if(g.SerieD!=null)return 4; // the four best of each group go through to the final phase
        if(g.DataVersion.StartsWith("serie-b-2026",StringComparison.Ordinal)||g.DataVersion.StartsWith("serie-c-2026",StringComparison.Ordinal))
        {
            var rank=g.Clubs.OrderByDescending(c=>c.Strength).ThenBy(c=>c.Id,StringComparer.Ordinal).ToList().FindIndex(c=>c.Id==club.Id);
            return rank<4?4:rank<10?10:16;
        }
        return club.Strength>=80?6:club.Strength>=76?10:16;
    }
    static void ResetBoard(GameState g){g.Life!.Board=new(){TargetPosition=BoardTarget(g),WageBudget=(long)(g.Squad.Sum(p=>(long)p.Wage)*1.3)+100_000,TransferBudget=Math.Max(0,g.Balance*3/4)};}
    public static ClubEconomy Economy(GameState g){var life=g.Life!;if(!life.Accounts.TryGetValue(g.ClubId,out var value))life.Accounts[g.ClubId]=value=new();return value;}
    public static int Quality(GameState g,string role)=>g.Life?.Staff.Find(s=>s.Role==role)?.Quality??0;
    public static string Category(Player p)=>p.Age<=14?"Sub-15":p.Age<=16?"Sub-17":"Sub-20";
    public static long AgentFee(GameState g,int playerId,long fee,int wage)=>g.Life==null?0:(long)Math.Ceiling((fee+wage*4m)*(3+Seed(g,"agent:"+playerId)%5)/100m);
    public static string AgentName(GameState g,int playerId)=>new[]{"Horizonte Sports","Linha de Jogo","Talento Gestão","Arena Carreiras"}[Seed(g,"agent:"+playerId)%4];
    public static void EnsureEmployed(GameState g){if(!CoachCareer.IsEmployed(g))throw new GameRuleException("Você está no mercado de treinadores. Assine uma proposta válida para voltar a controlar um clube.");}
    public static int MoraleBonus(GameState g,Player p){if(g.Life==null||!g.Life.Minds.TryGetValue(p.Id,out var mind))return 0;var captain=g.Life!.Minds.GetValueOrDefault(g.Life.Board.CaptainId);return Math.Clamp((mind.Morale-60)/10+(mind.Relationship-55)/20+(captain?.Leadership>=70?1:0),-5,5);}

    static int Minutes(GameState g,int playerId)=>(g.PlayerStats.GetValueOrDefault(playerId)?.Minutes??0)+(g.Life?.Competitions.SelectMany(c=>c.Matches).SelectMany(m=>m.Fixture.PlayerStats).Where(s=>s.PlayerId==playerId).Sum(s=>s.Minutes)??0);
    public static int RelapseRisk(GameState g,Player p){var plan=g.Life!.Medical[p.Id];return p.InjuryDays>0?0:g.Round-plan.ClearedRound<=3?Math.Clamp(18+(75-p.Fitness)/3+(g.WeeklyTraining.Intensity=="Alta"?8:0)-Quality(g,"Fisioterapeuta")/10-(plan.Treatment=="Conservador"?6:0),3,35):0;}
    static void Money(GameState g,string text,long amount){g.Balance+=amount;g.Ledger.Add(new(g.Round,text,amount,g.Season));}
    static void Story(GameState g,string kind,string title,string text){g.Life!.Press.Insert(0,new(){Season=g.Season,Round=g.Round,Kind=kind,Title=title,Text=text});if(g.Life.Press.Count>80)g.Life.Press.RemoveRange(80,g.Life.Press.Count-80);g.News.Insert(0,title+" "+text);if(g.News.Count>150)g.News.RemoveRange(150,g.News.Count-150);}
    public static IEnumerable<StaffMember> Candidates(GameState g)=>Roles.SelectMany((r,i)=>new[]{new StaffMember("staff-"+i+"-70","Especialista "+(i+1),r,70,12_000+i*1_000),new StaffMember("staff-"+i+"-88","Referência "+(i+1),r,88,24_000+i*1_500)});
    public static void BeforeRound(GameState g)
    {
        Upgrade(g);
        foreach(var p in g.Squad){var plan=g.Life!.Medical[p.Id];if(g.Life.Minds[p.Id].Morale<60)g.Life.Minds[p.Id].Morale=Math.Min(60,g.Life.Minds[p.Id].Morale+Quality(g,"Auxiliar")/25);
            if(p.InjuryDays>0&&plan.Treatment=="Intensivo")p.InjuryDays=Math.Max(1,p.InjuryDays-2);
            if(p.InjuryDays==0)p.Fitness=Math.Min(100,p.Fitness+Quality(g,"Preparador")/25);
        }
    }
    public static int Travel(GameState g,Fixture fixture,bool continental=false)
    {
        var previous=g.Fixtures.Concat(g.Life!.Competitions.SelectMany(c=>c.Matches).Select(m=>m.Fixture)).Where(f=>f.HomeGoals!=null&&(f.Home==g.ClubId||f.Away==g.ClubId)&&f.Date<fixture.Date).OrderByDescending(f=>f.Date).FirstOrDefault();
        if(previous?.Date is {} date&&fixture.Date is {} next){int days=next.DayNumber-date.DayNumber;if(days<6)foreach(var p in g.Squad.Where(p=>p.InjuryDays==0))p.Fitness=Math.Max(25,p.Fitness-(6-days)*2);}
        if(fixture.Home==g.ClubId)return 0;
        int km=200+Seed(g,"travel:"+fixture.Home+":"+fixture.Away)%2200+(continental?2500:0);
        int fatigue=Math.Clamp(km/550+1-Quality(g,"Preparador")/35,1,8);
        foreach(var p in g.Squad.Where(p=>p.InjuryDays==0))p.Fitness=Math.Max(25,p.Fitness-fatigue);
        return km;
    }
    public static void CupGate(GameState g,Fixture fixture){if(fixture.Home!=g.ClubId)return;var economy=Economy(g);int sold=(int)(economy.Capacity*Math.Clamp(.40+g.Life!.Fans.Popularity/160.0-(economy.TicketPrice-55)/250.0,.25,1));economy.Attendance=sold;economy.LastGate=(long)sold*economy.TicketPrice;Money(g,"Bilheteria · "+fixture.CompetitionName+" · "+sold+" ingressos",economy.LastGate);}
    public static void MatchReaction(GameState g,Fixture fixture,bool league)
    {
        var life=g.Life!;bool home=fixture.Home==g.ClubId;int scored=(home?fixture.HomeGoals:fixture.AwayGoals)??0,against=(home?fixture.AwayGoals:fixture.HomeGoals)??0;
        var opponent=home?fixture.Away:fixture.Home;bool derby=opponent==life.Fans.RivalId;int result=Math.Sign(scored-against),weight=derby?2:1;if(result==0&&fixture.HomePenalties.HasValue)result=Math.Sign((home?fixture.HomePenalties!.Value:fixture.AwayPenalties!.Value)-(home?fixture.AwayPenalties!.Value:fixture.HomePenalties!.Value));
        life.Fans.Support=Math.Clamp(life.Fans.Support+result*4*weight,0,100);life.Fans.Popularity=Math.Clamp(life.Fans.Popularity+result*weight,10,100);life.Fans.LossStreak=result<0?life.Fans.LossStreak+1:0;
        life.Fans.Mood=life.Fans.LossStreak>=4||life.Fans.Support<30?"Protestos e cobrança":life.Fans.Support>=75?"Arquibancada em festa":"Apoiando a equipe";
        foreach(var p in g.Squad){var mind=life.Minds[p.Id];int played=fixture.PlayerStats.FirstOrDefault(s=>s.PlayerId==p.Id)?.Minutes??0;mind.Morale=Math.Clamp(mind.Morale+result*3*weight+(played==0&&p.InjuryDays==0&&mind.Ambition>75?-2:0)+(p.Id==life.Board.CaptainId?1:0),0,100);}
        if(life.Coaches==null)life.Board.Confidence=Math.Clamp(life.Board.Confidence+(league?result*4:result*2),0,100);
        Story(g,derby?"Rivalidade":"Resultado",$"{scored} × {against}: repercussão da partida",life.Fans.Mood+". "+(derby?"O confronto com o rival desta carreira aumentou a pressão.":"O elenco avalia a próxima partida."));
    }
    public static void AfterRound(GameState g)
    {
        Upgrade(g);var life=g.Life!;if(life.LastSettledSeason==g.Season&&life.LastSettledRound==g.Round)return;
        life.LastSettledSeason=g.Season;life.LastSettledRound=g.Round;
        var f=g.Fixtures.SingleOrDefault(f=>f.Round==g.Round&&(f.Home==g.ClubId||f.Away==g.ClubId));if(f!=null)MatchReaction(g,f,true);
        var economy=Economy(g);var standings=LeagueRules.Table(g.Clubs,g.Fixtures,g.LotterySeed+":"+g.Season);int place=standings.FindIndex(t=>t.Club.Id==g.ClubId)+1;
        int attendance=f!=null&&f.Home==g.ClubId?(int)(economy.Capacity*Math.Clamp(.40+life.Fans.Popularity/160.0-(economy.TicketPrice-55)/250.0,.25,1)):0;
        economy.Attendance=attendance;economy.LastGate=(long)attendance*economy.TicketPrice;
        if(attendance>0)Money(g,"Bilheteria · "+attendance+" ingressos",economy.LastGate);
        Money(g,"Patrocínio "+economy.Sponsor,economy.SponsorWeekly);
        Money(g,"Folha salarial",-g.Squad.Sum(p=>(long)p.Wage));
        Money(g,"Comissão técnica e operação",-life.Staff.Sum(s=>(long)s.Wage)-150_000);
        if(economy.DebtRounds>0){var paid=Math.Min(economy.Debt,economy.Instalment);Money(g,"Parcela da dívida",-paid);economy.Debt-=paid;economy.DebtRounds--;}
        if(g.Balance<0){life.Board.Confidence=Math.Max(0,life.Board.Confidence-5);Story(g,"Finanças","Caixa no vermelho","A diretoria exige redução de despesas.");}
        if(g.Squad.Sum(p=>(long)p.Wage)+life.Staff.Sum(s=>(long)s.Wage)>life.Board.WageBudget)life.Board.Confidence=Math.Max(0,life.Board.Confidence-2);
        foreach(var p in g.Squad.Concat(g.Academy)){
            var medical=life.Medical[p.Id];if(medical.PreviousDays>0&&p.InjuryDays==0)medical.ClearedRound=g.Round;medical.PreviousDays=p.InjuryDays;
            int risk=RelapseRisk(g,p);bool played=f!=null&&f.PlayerStats.Any(s=>s.PlayerId==p.Id&&s.Minutes>0);
            if(played&&risk>0&&new Random(Seed(g,$"relapse:{g.Season}:{g.Round}:{p.Id}")).Next(100)<risk){p.InjuryDays=7;p.InjuryName="Recaída muscular";medical.PreviousDays=7;medical.Relapses++;Story(g,"Médico","Recaída no elenco",p.Name+" volta ao tratamento por sete dias.");}
            var mind=life.Minds[p.Id];if(mind.PromiseDueRound>0&&g.Round>=mind.PromiseDueRound){bool kept=Minutes(g,p.Id)-mind.MinutesAtPromise>=mind.PromisedMinutes;mind.Relationship=Math.Clamp(mind.Relationship+(kept?10:-18),0,100);mind.Morale=Math.Clamp(mind.Morale+(kept?8:-12),0,100);mind.PromiseDueRound=0;Story(g,"Vestiário",p.Name+": promessa "+(kept?"cumprida":"descumprida"),"A avaliação do jogador altera a relação com o treinador.");}
        }
        foreach(var report in life.Reports.Where(r=>!r.Complete&&r.Season==g.Season&&r.DueRound<=g.Round)){report.Complete=true;Story(g,"Olheiros","Relatório de observação concluído","Confira as estimativas de potencial no painel de olheiros.");}
        // Contextual evaluations and dismissals belong to CoachCareer, after all results settle.
        if(g.Round==g.SeasonRounds){Money(g,"Premiação da liga",Math.Max(500_000,(21-place)*500_000L));if(place>economy.SponsorTarget)Money(g,"Meta de patrocínio não cumprida",-economy.SponsorWeekly);}
        if(g.AutoReplaceUnavailable)Sports.RepairSelection(g.Squad,true);Sports.RefreshBench(g);
        foreach(var oldPress in life.Press.Where(s=>s.Kind=="Coletiva"&&s.Answer==""))oldPress.Answer="Sem declaração";
        Story(g,"Coletiva","Coletiva após a rodada "+g.Round,"Como você avalia o momento da equipe?");
        var offer=g.Offers.FirstOrDefault(o=>TransferMarket.Open(o));if(offer!=null)Story(g,"Rumor","Mercado acompanha "+offer.PlayerName,"Há negociação registrada. O interesse ainda depende dos termos e da confirmação das partes.");
    }
    static Player Own(GameState g,int id)=>g.Squad.Find(p=>p.Id==id)??throw new GameRuleException("Jogador não pertence ao seu elenco.");
    public static object Act(GameState g,string type,int playerId,string value,long amount)
    {
        Upgrade(g);var life=g.Life!;if(type!="job")EnsureEmployed(g);
        switch(type){
            case "talk": {var p=Own(g,playerId);var mind=life.Minds[p.Id];if(mind.LastTalkRound==g.Round)throw new GameRuleException("Aguarde a próxima rodada para conversar novamente.");if(value is not ("Apoiar" or "Cobrar" or "Prometer espaço"))throw new GameRuleException("Conversa inválida.");if(value=="Prometer espaço"&&g.Round>35)throw new GameRuleException("Não há três rodadas para cumprir esta promessa.");mind.LastTalkRound=g.Round;int change=value=="Cobrar"?(mind.Personality=="Temperamental"?-7:2):5;mind.Morale=Math.Clamp(mind.Morale+change,0,100);mind.Relationship=Math.Clamp(mind.Relationship+(value=="Cobrar"?-3:4),0,100);if(value=="Prometer espaço"){if(mind.PromiseDueRound>0)throw new GameRuleException("Cumpra a promessa atual primeiro.");mind.PromiseDueRound=g.Round+3;mind.PromisedMinutes=120;mind.MinutesAtPromise=Minutes(g,p.Id);}Story(g,"Vestiário","Conversa com "+p.Name,value=="Prometer espaço"?"Você prometeu 120 minutos nas próximas três rodadas.":"O jogador reagiu à conversa de acordo com sua personalidade.");break;}
            case "captain": {var p=Own(g,playerId);life.Board.CaptainId=p.Id;Story(g,"Vestiário","Novo capitão",p.Name+" assumiu a liderança do grupo.");break;}
            case "treatment": {var p=Own(g,playerId);if(p.InjuryDays<=0)throw new GameRuleException("Atleta não está em tratamento.");if(value is not ("Padrão" or "Conservador" or "Intensivo"))throw new GameRuleException("Tratamento inválido.");var care=life.Medical[p.Id];if(care.LastTreatmentRound==g.Round)throw new GameRuleException("O plano desta rodada já foi definido.");long fee=value=="Intensivo"?150_000:50_000;Spend(g,"Tratamento de "+p.Name,fee);care.Treatment=value;care.LastTreatmentRound=g.Round;break;}
            case "staff": {var candidate=Candidates(g).FirstOrDefault(c=>c.Id==value)??throw new GameRuleException("Profissional indisponível.");var old=life.Staff.Find(s=>s.Role==candidate.Role);if(old?.Id==candidate.Id)throw new GameRuleException("Profissional já contratado.");if(g.Squad.Sum(p=>(long)p.Wage)+life.Staff.Sum(s=>(long)s.Wage)-(old?.Wage??0)+candidate.Wage>life.Board.WageBudget)throw new GameRuleException("A contratação excede o orçamento salarial.");Spend(g,"Contratação de "+candidate.Role,candidate.Wage*4L+(old?.Wage??0)*2L);life.Staff.RemoveAll(s=>s.Role==candidate.Role);life.Staff.Add(candidate);break;}
            case "interview": {if(value is not ("Proteger elenco" or "Cobrar reação" or "Assumir responsabilidade"))throw new GameRuleException("Resposta inválida.");var story=life.Press.FirstOrDefault(s=>s.Kind=="Coletiva"&&s.Answer==""&&s.Season==g.Season)??throw new GameRuleException("Não há coletiva aguardando resposta.");if(life.LastInterviewRound==g.Round)throw new GameRuleException("Você já respondeu nesta rodada.");life.LastInterviewRound=g.Round;story.Answer=value;foreach(var p in g.Squad)life.Minds[p.Id].Morale=Math.Clamp(life.Minds[p.Id].Morale+(value=="Cobrar reação"?-2:3),0,100);life.Board.Confidence=Math.Clamp(life.Board.Confidence+(value=="Cobrar reação"?2:-1),0,100);life.Fans.Support=Math.Clamp(life.Fans.Support+2,0,100);break;}
            case "ticket": if(amount is <20 or >150)throw new GameRuleException("Preço deve ficar entre R$ 20 e R$ 150.");Economy(g).TicketPrice=(int)amount;break;
            case "sponsor": {var economy=Economy(g);if(economy.SponsorSignedSeason==g.Season)throw new GameRuleException("O patrocínio desta temporada já foi escolhido.");if(value is not ("Regional" or "Nacional" or "Ambicioso"))throw new GameRuleException("Patrocinador inválido.");economy.Sponsor=value;economy.SponsorWeekly=value=="Regional"?1_000_000:value=="Nacional"?1_400_000:1_800_000;economy.SponsorTarget=value=="Regional"?16:value=="Nacional"?10:6;economy.SponsorSignedSeason=g.Season;break;}
            case "loan": {if(amount is <1_000_000 or >20_000_000)throw new GameRuleException("Crédito deve ficar entre R$ 1 milhão e R$ 20 milhões.");var economy=Economy(g);if(economy.Debt>0)throw new GameRuleException("Quite a dívida atual antes de pedir novo crédito.");economy.Debt=(long)Math.Ceiling(amount*1.12m);economy.DebtRounds=12;economy.Instalment=(long)Math.Ceiling(economy.Debt/12m);Money(g,"Crédito contratado · 12 parcelas, custo total de 12%",amount);break;}
            case "funding": {var board=life.Board;if(board.LastFundingSeason==g.Season||board.Confidence<70||board.FundingReserve<5_000_000)throw new GameRuleException("Aporte exige confiança de 70, reserva e apenas um pedido por temporada.");board.FundingReserve-=5_000_000;board.TransferBudget+=5_000_000;board.LastFundingSeason=g.Season;Money(g,"Aporte aprovado pela diretoria",5_000_000);break;}
            case "scout": {var player=TransferMarket.Locate(g,playerId).Player;if(g.Squad.Any(p=>p.Id==playerId))throw new GameRuleException("Escolha um atleta de outro clube ou agente livre.");if(life.Reports.Any(r=>r.PlayerId==playerId&&r.Season==g.Season))throw new GameRuleException("Este atleta já está em observação nesta temporada.");if(g.Round>36)throw new GameRuleException("Inicie observações até a rodada 36.");Spend(g,"Relatório de olheiro: "+player.Name,120_000);life.Reports.Add(new(){PlayerId=playerId,Season=g.Season,DueRound=g.Round+2,Quality=Quality(g,"Olheiro")});break;}
            case "trial": {if(value is not ("Sub-15" or "Sub-17" or "Sub-20"))throw new GameRuleException("Categoria inválida.");if(g.Round-life.LastTrialRound<4)throw new GameRuleException("Espere quatro rodadas entre peneiras.");Spend(g,"Peneira "+value,180_000);life.LastTrialRound=g.Round;life.Trials.Clear();for(int i=0;i<4;i++)life.Trials.Add(new(Youth.GenerateForCategory(g,value,i),"Peneira "+value,100_000));Upgrade(g);break;}
            case "recruit": {var prospect=life.Trials.Find(p=>p.Player.Id==playerId)??throw new GameRuleException("Jovem não está na peneira.");if(g.Round-life.LastTrialRound>4)throw new GameRuleException("A peneira expirou.");if(g.Academy.Count>=Youth.Capacity)throw new GameRuleException("A base atingiu sua capacidade.");Spend(g,"Captação na peneira: "+prospect.Player.Name,prospect.SigningFee);life.Trials.Remove(prospect);g.Academy.Add(prospect.Player);Training.Upgrade(g);break;}
            case "postpone": if(amount<1||amount>g.SeasonRounds)throw new GameRuleException("Rodada inválida.");ClubCompetitions.Postpone(g,(int)amount);break;
            case "job": throw new GameRuleException("Mudanças de clube exigem uma proposta oficial aceita no mercado de treinadores.");
            default: throw new GameRuleException("Ação de gestão inválida.");
        }
        return new {message="Decisão registrada na carreira."};
    }
    static void Spend(GameState g,string description,long fee){if(g.Balance<fee)throw new GameRuleException("Caixa insuficiente para esta decisão.");Money(g,description,-fee);}
    public static PotentialEstimate PotentialRange(GameState g,Player p,int quality){int error=Math.Clamp((100-quality)/5,2,16),actual=g.Life!.HiddenPotential[p.Id];int estimate=actual+Seed(g,"estimate:"+p.Id+":"+quality)%(error+1)-error/2;return new(Math.Max(p.Rating,estimate-error),Math.Min(99,estimate+error));}
    public static object View(GameState g)
    {
        Upgrade(g);var life=g.Life!;
        return new {life.Board,life.Fans,economy=Economy(g),life.Staff,candidates=Candidates(g),life.Press,players=g.Squad.Select(p=>new{p.Id,p.Name,p.Position,p.Age,p.Fitness,p.InjuryDays,p.InjuryName,mind=life.Minds[p.Id],medical=life.Medical[p.Id],relapseRisk=RelapseRisk(g,p)}),academy=g.Academy.Select(p=>new{player=p,category=Category(p),potential=PotentialRange(g,p,Quality(g,"Coordenador da base"))}),trials=life.Trials.Select(p=>new{p.Player,p.SigningFee,category=Category(p.Player),potential=PotentialRange(g,p.Player,Quality(g,"Olheiro"))}),life.LastTrialRound,reports=life.Reports.Select(r=>new{r.PlayerId,r.DueRound,r.Season,r.Complete,player=Players(g).FirstOrDefault(p=>p.Id==r.PlayerId) is {} p?new {p.Id,p.Name,p.Position,p.Age,p.Rating,p.Value,potential=r.Complete?(object)PotentialRange(g,p,r.Quality):null}:null}),scoutCandidates=g.Market.Concat(g.OpponentSquads.Values.SelectMany(s=>s)).Select(p=>new{p.Id,p.Name,p.Position,p.Rating}).Take(500),life.Competitions,life.ExtraClubs,life.Honours,agents=g.Squad.Select(p=>new{p.Id,p.Name,agent=AgentName(g,p.Id),commission=AgentFee(g,p.Id,0,p.Wage),wageDemand=TransferMarket.Quote(g,p.Id,"renewal",Math.Min(5,Math.Max(3,g.Contracts[p.Id].EndSeason-g.Season+2))).Wage}),nextCup=ClubCompetitions.NextDue(g),calendar=ClubCompetitions.Calendar(g)};
    }
    public static void NewSeason(GameState g){Upgrade(g);var life=g.Life!;life.Season=g.Season;life.LastInterviewRound=-1;life.LastTrialRound=-10;life.Reports.Clear();life.Trials.Clear();life.LastSettledRound=0;life.Board.Warnings=0;life.Board.Confidence=Math.Clamp(life.Board.Confidence+10,35,100);life.Board.TransferBudget=Math.Max(0,g.Balance*3/4);life.Board.WageBudget=Math.Max(life.Board.WageBudget,g.Squad.Sum(p=>(long)p.Wage)*13/10+100_000);foreach(var mind in life.Minds.Values){mind.LastTalkRound=-1;mind.PromiseDueRound=0;}ClubCompetitions.NewSeason(g);CoachCareer.NewSeason(g);}
}
