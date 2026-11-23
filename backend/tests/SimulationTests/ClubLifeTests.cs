using Brasileirao;
using System.Text.Json;
public static class ClubLifeTests
{
    static int checks;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS: "+text);}
    static void Reject(Action action,string text){try{action();}catch(GameRuleException){Check(true,text);return;}throw new Exception(text);}
    static Fixture Result(GameState g,int own,int other){var f=g.Fixtures.Single(f=>f.Round==g.Round&&(f.Home==g.ClubId||f.Away==g.ClubId));f.HomeGoals=f.Home==g.ClubId?own:other;f.AwayGoals=f.Away==g.ClubId?own:other;return f;}
    public static void Run()
    {
        var g=Game.New("pal");var life=g.Life!;var p=g.Squad.First(p=>p.Position!="GOL");
        Check(g.Clubs.Count==20&&life.ExtraClubs.Count==4&&life.Competitions.Count==3,"Liga mantém vinte clubes e copas têm adversários separados");
        var publicView=JsonSerializer.Serialize(ClubLife.View(g),Game.Json);
        Check(!publicView.Contains("hiddenPotential",StringComparison.OrdinalIgnoreCase),"Potencial exato não vaza pela projeção pública");
        Check(life.Board.CaptainId>=0&&life.Minds.Count>=g.Squad.Count,"Personalidades e capitão inicial são persistentes");
        int relationship=life.Minds[p.Id].Relationship;ClubLife.Act(g,"talk",p.Id,"Prometer espaço",0);
        Check(life.Minds[p.Id].Relationship>relationship&&life.Minds[p.Id].PromiseDueRound==4,"Promessa registra prazo e relação com o treinador");
        Reject(()=>ClubLife.Act(g,"talk",p.Id,"Apoiar",0),"Não permite repetir conversa para acumular moral");
        ClubLife.Act(g,"loan",0,"",5_000_000);Check(ClubLife.Economy(g).Debt==5_600_000&&ClubLife.Economy(g).DebtRounds==12,"Crédito registra principal e custo total nas doze parcelas");
        ClubLife.Act(g,"sponsor",0,"Nacional",0);Reject(()=>ClubLife.Act(g,"sponsor",0,"Regional",0),"Patrocínio não pode ser trocado repetidamente na temporada");
        long before=g.Balance;int ledger=g.Ledger.Count;Result(g,2,0);ClubLife.AfterRound(g);
        Check(g.Balance-before==g.Ledger.Skip(ledger).Sum(l=>l.Amount),"Todas as receitas e despesas da rodada conferem com o caixa");
        Check(ClubLife.Economy(g).Debt<5_600_000&&life.Fans.Support>65,"Rodada paga dívida e resultado muda apoio da torcida");
        before=g.Balance;ClubLife.AfterRound(g);Check(g.Balance==before,"Liquidação da rodada é idempotente");
        ClubLife.Act(g,"interview",0,"Proteger elenco",0);Reject(()=>ClubLife.Act(g,"interview",0,"Cobrar reação",0),"Coletiva só permite uma resposta por rodada");
        p.InjuryDays=14;p.InjuryName="Lesão muscular";life.Medical[p.Id].PreviousDays=14;ClubLife.Act(g,"treatment",p.Id,"Intensivo",0);ClubLife.BeforeRound(g);
        Check(p.InjuryDays==12,"Tratamento intensivo abrevia o período de recuperação");
        var staff=ClubLife.Candidates(g).First(c=>c.Role=="Analista"&&c.Quality==88);ClubLife.Act(g,"staff",0,staff.Id,0);Check(ClubLife.Quality(g,"Analista")==88,"Contratação substitui especialista e altera sua qualidade");
        ClubLife.Act(g,"trial",0,"Sub-15",0);Check(life.Trials.Count==4&&life.Trials.All(t=>t.Player.Age is >=12 and <=14),"Peneira gera quatro atletas da categoria escolhida");
        var prospect=life.Trials[0].Player;ClubLife.Act(g,"recruit",prospect.Id,"",0);before=g.Balance;
        Reject(()=>Youth.Promote(g,prospect.Id),"Atleta Sub-15 não pode ser promovido prematuramente");Check(g.Balance==before,"Promoção inválida não cobra taxa");
        ClubLife.Act(g,"scout",g.Market[0].Id,"",0);Check(life.Reports[0].DueRound==3&&!life.Reports[0].Complete,"Olheiro não entrega potencial antes de concluir a observação");
        g.Round=3;Result(g,1,1);ClubLife.AfterRound(g);Check(life.Reports[0].Complete,"Relatório chega no prazo previsto");
        g.Round=4;Result(g,0,4);life.Board.Confidence=0;life.Board.Warnings=1;ClubLife.AfterRound(g);
        Check(life.Minds[p.Id].PromiseDueRound==0,"Promessas vencidas são avaliadas ao concluir a rodada");
        Reject(()=>ClubLife.Act(g,"job",0,"fla",0),"Mudança de clube depende de proposta oficial no mercado de técnicos");
        var calendar=Game.New("flu");var date8=calendar.Fixtures.First(f=>f.Round==8).Date!.Value;var date9=calendar.Fixtures.First(f=>f.Round==9).Date!.Value;
        Check(date9.DayNumber-date8.DayNumber>=13,"Calendário gerado reserva pausa de seleções");
        var match=calendar.Fixtures.Single(f=>f.Round==5&&(f.Home==calendar.ClubId||f.Away==calendar.ClubId));var date=match.Date;ClubCompetitions.Postpone(calendar,5);Check(match.Date==date!.Value.AddDays(2)&&match.Rescheduled,"Remarcação altera data e preserva registro");
        calendar.CalendarVersion=1;calendar.Life!.CalendarVersion=1;SeasonCalendar.Upgrade(calendar);ClubLife.Upgrade(calendar);
        Check(calendar.Life.Competitions.SelectMany(c=>c.Matches).All(m=>m.Fixture.Date==calendar.Fixtures.First(f=>f.Round==m.Fixture.Round).Date!.Value.AddDays(-3)),"Migração alinha datas futuras das copas às pausas geradas da liga");
        Reject(()=>ClubCompetitions.Postpone(calendar,5),"Uma remarcação não pode ser repetida indefinidamente");
        calendar.Round=2;var now=DateTimeOffset.UtcNow;var live=LiveBroadcast.Start(calendar,now);
        Check(live.CompetitionMatchId!=""&&live.Fixture.Setup!.Knockout&&calendar.Round==2,"Partida de copa entra na transmissão antes da liga sem avançar rodada");
        using(var runtime=new LiveSimulation()){live.WatchedSeconds=1000;runtime.Advance(live);Check(live.HalfTimeAwaiting&&!live.EngineFinished,"Copa também exige pausa no intervalo");live.HalfTimeAccepted=true;live.HalfTimeAwaiting=false;live.WatchedSeconds=1000;runtime.Advance(live);}
        var finished=LiveBroadcast.Finish(calendar,live.Id);
        Check(finished.Round==2&&finished.Life!.Competitions.SelectMany(c=>c.Matches).Single(m=>m.Id==live.CompetitionMatchId).Winner!="","Finalizar copa registra resultado e mantém rodada da liga");
        var cupResult=finished.Life!.Competitions.SelectMany(c=>c.Matches).Single(m=>m.Id==live.CompetitionMatchId).Fixture;
        Check(cupResult.Home!=finished.ClubId||finished.Ledger.Any(l=>l.Description.StartsWith("Bilheteria · "+cupResult.CompetitionName)&&l.Amount>0),"Mandante de copa recebe bilheteria registrada no livro caixa");
        Check(finished.PlayerStats.Count==0,"Estatísticas da copa não contaminam a artilharia da liga");
        var setup=new MatchSetup(PositionalV8.Version,Sports.BuildTeam(finished,finished.ClubId),Sports.BuildTeam(finished,finished.Clubs.First(c=>c.Id!=finished.ClubId).Id),Knockout:true);
        MatchReplay? tied=null;int seed=0;for(int i=1;i<=20;i++){var replay=MatchSimulation.Run(setup,i,false);if(replay.HomePenalties!=null){tied=replay;seed=i;break;}}
        Check(tied!=null&&tied.HomeGoals==tied.AwayGoals&&tied.HomePenalties!=tied.AwayPenalties,"Empate eliminatório termina com vencedor nos pênaltis");
        var recorded=MatchSimulation.Run(setup,seed,true);Check(recorded.HomePenalties==tied!.HomePenalties&&recorded.AwayPenalties==tied.AwayPenalties&&recorded.Frames.Any(f=>f.Players.Any(p=>p.Action=="penalty_kick")),"Replay reproduz cobranças, animações e placar dos pênaltis");
        var regulation=tied.Events.First(e=>e.Type=="shootout_start").Second;
        using(var stream=new LiveSimulation()){
            var session=new LiveMatch{Incremental=true,HalfTimeAccepted=true,Fixture=new Fixture{Setup=setup,Seed=seed},WatchedSeconds=12+regulation/12.0};
            var partial=stream.Advance(session);Check(!session.EngineFinished&&partial.Events.Any(e=>e.Type=="shootout_start")&&!partial.Events.Any(e=>e.Type=="shootout_goal"),"Disputa ao vivo começa sem revelar resultados das cobranças");
            session.WatchedSeconds+=.5;partial=stream.Advance(session);Check(partial.Events.All(e=>e.Second<=session.SimulatedSecond)&&partial.Frames.All(f=>f.Second<=session.SimulatedSecond),"Cobranças só liberam eventos e posições já alcançados pelo relógio");
        }
        var restored=JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(finished,Game.Json),Game.Json)!;
        Check(restored.Life!.Competitions[0].Matches.Any(m=>m.Winner!="")&&restored.Life.Minds.Count==finished.Life!.Minds.Count,"Save conserva mata-mata, personalidades e resultados");
        var old=setup with{EngineVersion=PositionalV7.Version,Knockout=false};Check(JsonSerializer.Serialize(MatchSimulation.Run(old,42,false),Game.Json)==JsonSerializer.Serialize(PositionalV7.Run(old,42,false),Game.Json),"Motor V7 permanece congelado para partidas salvas");
        Console.WriteLine($"{checks} verificações dos sistemas de clube passaram.");
    }
}
