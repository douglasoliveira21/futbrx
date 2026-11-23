namespace Brasileirao;

public static class ClubCompetitions
{
    public static void NewSeason(GameState g)
    {
        var life=g.Life!;
        foreach(var c in life.Competitions.Where(c=>c.Champion!=""))life.Honours.Add($"{life.Season-1}: {c.Name} — {Name(g,c.Champion)}");
        life.CupHistory.AddRange(life.Competitions.Select(c=>new ArchivedCompetition(g.Season-1,c)));
        life.Competitions.Clear();
        if(life.ExtraClubs.Count==0)life.ExtraClubs=[new("demo-andes","Andes FC","AND","#cc6b38",76),new("demo-plata","Atlético del Plata","PLA","#72a7e8",79),new("demo-pacifico","Unión del Pacífico","PAC","#e1c56b",74),new("demo-sur","Deportivo Sur","SUR","#7bbd96",75)];
        var usedIds=g.Squad.Concat(g.Market).Concat(g.Academy).Concat(g.OpponentSquads.Values.SelectMany(s=>s)).Select(p=>p.Id).Concat(g.Contracts.Keys).ToHashSet();
        foreach(var c in life.ExtraClubs){if(!g.OpponentSquads.ContainsKey(c.Id)){int offset=500_000+life.ExtraClubs.IndexOf(c)*100;g.OpponentSquads[c.Id]=Game.Players(offset);foreach(var p in g.OpponentSquads[c.Id]){while(!usedIds.Add(p.Id))p.Id++;g.Contracts[p.Id]=new(){PlayerId=p.Id,ClubId=c.Id,StartSeason=g.Season,EndSeason=g.Season+3,Wage=p.Wage};}g.ClubBalances.TryAdd(c.Id,30_000_000);}}
        List<string> Field(int count)=>new[]{g.ClubId}.Concat(g.Clubs.Where(c=>c.Id!=g.ClubId).OrderBy(c=>ClubLife.Seed(g,"entry:"+g.Season+":"+c.Id)).Take(count-1).Select(c=>c.Id)).ToList();
        if(g.Round<=2)Add(g,"state","Estadual · circuito do clube",Field(4),[2,4],1_500_000);
        if(g.Round<=6)Add(g,"copa","Copa do Brasil",Field(16),[6,14,24,34],12_000_000);
        if(g.Round<=10){var last=g.PastSeasons.LastOrDefault();bool elite=last!=null?last.Table.FindIndex(t=>t.ClubId==g.ClubId) is >=0 and <6:g.Clubs.Find(c=>c.Id==g.ClubId)!.Strength>=78;var domestic=Field(4);var participants=domestic.SelectMany((id,i)=>new[]{id,life.ExtraClubs[i].Id}).ToList();Add(g,"continental",elite?"Libertadores":"Sul-Americana",participants,[10,22,36],elite?18_000_000:8_000_000);}
    }
    static string Name(GameState g,string club)=>g.Clubs.Concat(g.Life!.ExtraClubs).FirstOrDefault(c=>c.Id==club)?.Name??club;
    static void Add(GameState g,string id,string name,List<string> participants,List<int> rounds,long prize)
    {
        var competition=new ClubCompetition{Id=id,Name=name,Rounds=rounds,Prize=prize,Format=id=="state"?"Circuito estadual demonstrativo de quatro clubes do catálogo. Mata-mata em jogo único; empate nos pênaltis. Não reproduz o regulamento oficial.":id=="continental"?"Circuito continental demonstrativo: oito clubes, incluindo quatro adversários fictícios internacionais. Jogo único e pênaltis. Qualificação inicial por força do clube; próximas temporadas por colocação.":"Copa gerada com dezesseis clubes do catálogo, jogo único e pênaltis; formato simplificado."};
        g.Life!.Competitions.Add(competition);CreateStage(g,competition,participants);
    }
    static void CreateStage(GameState g,ClubCompetition competition,List<string> participants)
    {
        for(int i=0;i<participants.Count;i+=2){var home=participants[i];var away=participants[i+1];if(ClubLife.Seed(g,$"venue:{g.Season}:{competition.Id}:{competition.Stage}:{i}")%2==0)(home,away)=(away,home);int round=competition.Rounds[competition.Stage];var league=g.Fixtures.First(f=>f.Round==round);var date=(league.Date??new DateOnly(g.Season,3,1)).AddDays(-3);var matchId=$"{g.Season}-{competition.Id}-{competition.Stage}-{i}";competition.Matches.Add(new(){Id=matchId,Stage=competition.Stage,Fixture=new(){CompetitionMatchId=matchId,CompetitionName=competition.Name,Round=round,Home=home,Away=away,Date=date,Kickoff=new(date.Year,date.Month,date.Day,20,0,0,TimeSpan.FromHours(-3)),Seed=ClubLife.Seed(g,$"cup:{g.Season}:{competition.Id}:{competition.Stage}:{i}")}});}
    }
    public static CupMatch? NextDue(GameState g)=>g.Life?.Competitions.SelectMany(c=>c.Matches).Where(m=>m.Winner==""&&m.Fixture.Round<=g.Round&&(m.Fixture.Home==g.ClubId||m.Fixture.Away==g.ClubId)).OrderBy(m=>m.Fixture.Date).FirstOrDefault();
    public static string CompetitionName(GameState g,CupMatch match)=>g.Life!.Competitions.First(c=>c.Matches.Contains(match)).Name;
    public static void Advance(GameState g,bool includeManaged=false)
    {
        if(g.Life==null)return;
        foreach(var competition in g.Life.Competitions){
            for(int pass=0;pass<6&&competition.Champion=="";pass++){
                int stage=competition.Stage;
                foreach(var match in competition.Matches.Where(m=>m.Stage==stage&&m.Winner==""&&m.Fixture.Round<=g.Round).ToList()){
                    if(!includeManaged&&(match.Fixture.Home==g.ClubId||match.Fixture.Away==g.ClubId))continue;
                    if(includeManaged)Sports.RepairSelection(g.Squad,true);
                    match.Fixture.Setup=new(MatchSimulation.Version,Sports.BuildTeam(g,match.Fixture.Home),Sports.BuildTeam(g,match.Fixture.Away),Knockout:true);
                    Game.ApplyResult(match.Fixture,MatchSimulation.Run(match.Fixture.Setup,match.Fixture.Seed,false));Complete(g,match);
                }
                Progress(g,competition);if(competition.Stage==stage)break;
            }
        }
    }

    public static void Complete(GameState g,CupMatch match)
    {
        if(match.Winner!="")return;
        var f=match.Fixture;if(f.HomeGoals==null||f.AwayGoals==null)throw new GameRuleException("Partida eliminatória ainda não concluída.");
        if(f.HomeGoals==f.AwayGoals&&(f.HomePenalties==null||f.HomePenalties==f.AwayPenalties))throw new GameRuleException("Empate eliminatório exige decisão nos pênaltis.");
        match.Winner=f.HomeGoals!=f.AwayGoals?(f.HomeGoals>f.AwayGoals?f.Home:f.Away):(f.HomePenalties>f.AwayPenalties?f.Home:f.Away);
        Progress(g,g.Life!.Competitions.First(c=>c.Matches.Contains(match)));
    }
    static void Progress(GameState g,ClubCompetition competition)
    {
        if(competition.Champion!="")return;
        var current=competition.Matches.Where(m=>m.Stage==competition.Stage).ToList();if(current.Any(m=>m.Winner==""))return;
        if(current.Count==1){competition.Champion=current[0].Winner;g.News.Insert(0,competition.Name+": campeão "+Name(g,competition.Champion)+".");if(competition.Champion==g.ClubId){g.Balance+=competition.Prize;g.Ledger.Add(new(g.Round,"Título: "+competition.Name,competition.Prize,g.Season));g.Life!.Board.Confidence=Math.Min(100,g.Life.Board.Confidence+12);}return;}
        competition.Stage++;CreateStage(g,competition,current.Select(m=>m.Winner).ToList());
    }
    public static object Calendar(GameState g)=>new {
        notice="Datas e deslocamentos estimados pelo jogo. Pausas de seleções geradas, sem compromisso com a tabela oficial.",
        breaks=new[]{8,16,24,32}.Select(r=>new{round=r,label="Pausa de seleções · sete dias",date=g.Fixtures.First(f=>f.Round==r).Date}),
        trips=g.Fixtures.Where(f=>f.Away==g.ClubId&&f.Round>=g.Round).Take(6).Select(f=>new{f.Round,f.Date,destination=Name(g,f.Home),kilometres=200+ClubLife.Seed(g,"travel:"+f.Home+":"+f.Away)%2200}),
        postponed=g.Fixtures.Where(f=>f.Rescheduled).Select(f=>new{f.Round,f.Date,f.Home,f.Away})
    };
    public static void Postpone(GameState g,int round)
    {
        if(round<g.Round||round>38)throw new GameRuleException("Escolha uma rodada futura.");var f=g.Fixtures.Single(f=>f.Round==round&&(f.Home==g.ClubId||f.Away==g.ClubId));if(f.Setup!=null||f.HomeGoals!=null||f.Rescheduled)throw new GameRuleException("Este jogo já foi preparado, disputado ou remarcado.");
        f.Rescheduled=true;f.Date=f.Date!.Value.AddDays(2);f.Kickoff=f.Kickoff!.Value.AddDays(2);f.Events.Add(new(0,"postponed",g.ClubId,0,"Partida remarcada em dois dias por solicitação logística.",0));g.News.Insert(0,"Calendário: partida da rodada "+round+" remarcada em dois dias. O tempo de recuperação até o próximo compromisso será menor.");
    }
}
