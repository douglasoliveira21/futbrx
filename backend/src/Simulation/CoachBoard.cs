namespace Brasileirao;

public static partial class CoachCareer
{
    static void Evaluate(GameState g,CoachPerson p,Fixture fixture)
    {
        var w=World(g);var b=w.Boards[p.ClubId];var contract=Active(g,p.Id);if(contract==null)return;
        bool isPlayer=p.Id==w.PlayerId;var club=Clubs(g).First(c=>c.Id==p.ClubId);var last=p.Results[^1];var opponent=Clubs(g).FirstOrDefault(c=>c.Id==(fixture.Home==p.ClubId?fixture.Away:fixture.Home));int result=Math.Sign(last.Scored-last.Against),effective=result;
        if(effective==0&&fixture.HomePenalties.HasValue)effective=Math.Sign((fixture.Home==p.ClubId?fixture.HomePenalties!.Value:fixture.AwayPenalties!.Value)-(fixture.Home==p.ClubId?fixture.AwayPenalties!.Value:fixture.HomePenalties!.Value));
        var recent=p.Results.Where(r=>r.ContractId==contract.Id).TakeLast(20).ToList();
        double expected=Math.Clamp(.5+(club.Strength-(opponent?.Strength??club.Strength))*.018,.18,.82),earned=result>0?1:result==0?.4:0;
        double Performance(int n){var rows=recent.TakeLast(n).ToList();return rows.Count==0?.5:rows.Sum(r=>r.Points)/(rows.Count*3.0);}
        int place=Position(g,p.ClubId),target=contract.TargetPosition;bool derby=isPlayer&&g.Life!.Fans.RivalId==opponent?.Id;
        double morale=isPlayer?g.Squad.Select(x=>g.Life!.Minds[x.Id].Morale).DefaultIfEmpty(60).Average():60+b.Confidence*.1;
        double relationship=isPlayer?g.Squad.Select(x=>g.Life!.Minds[x.Id].Relationship).DefaultIfEmpty(55).Average():55;
        var factors=new Dictionary<string,double>{
            ["Resultado versus força do adversário"]=(earned-expected)*7,
            ["Últimos 5 jogos"]=(Performance(5)-.5)*1.5,
            ["Últimos 10 jogos"]=(Performance(10)-.5),
            ["Últimos 20 jogos"]=(Performance(20)-.5)*.5,
            ["Posição versus meta"]=last.League&&recent.Count>=4?Math.Clamp((target-place)*.25,-2,2):0,
            ["Futebol e defesa"]=Math.Clamp((last.Scored-last.Against)*.3,-1,1),
            ["Gestão financeira"]=Cash(g,p.ClubId)<0?-1.5:Cash(g,p.ClubId)>10_000_000?.25:0,
            ["Utilização da base"]=Math.Min(1,last.YouthMinutes/180.0)*b.AcademyValue/100,
            ["Moral e relacionamento"]=(morale-60)/70+(relationship-55)/100,
            ["Torcida e clássico"]=derby?effective*2:isPlayer?(g.Life!.Fans.Support-60)/70:0,
            ["Evolução da temporada"]=b.PreviousPosition>0&&place>0?Math.Clamp((b.PreviousPosition-place)*.1,-.5,.5):0,
            ["Eliminação"]=!last.League&&effective<0?-(1+b.TitleDemand/40.0):0
        };
        int old=b.Confidence;b.Confidence=Math.Clamp((int)Math.Round(old+factors.Values.Sum()),0,100);b.Assessments.Add(new(w.Date,b.Confidence,$"{last.Scored} × {last.Against}; {place}º, meta {target}º. "+ConfidenceLabel(w,b.Confidence),factors));if(b.Assessments.Count>40)b.Assessments.RemoveAt(0);
        p.Reputation+=(earned-expected)*.9+(last.YouthMinutes>90?.08:0)+(!last.League&&effective<0?-.25:0)+Math.Min(.08,recent.Count*.002);SnapshotReputation(g,p,"Avaliação após partida");
        bool failed=false;
        if(b.Ultimatum is {Resolved:false} u){u.GamesLeft--;u.Progress=u.Kind switch{"wins"=>u.Progress+(effective>0?1:0),"relegation"=>place is >0 and <=16?1:0,"defense"=>u.Progress+(last.Against<=1?1:0),"morale"=>(int)morale,"qualification"=>!last.League&&effective>0?1:u.Progress,_=>u.Progress};if(u.Progress>=u.Required){u.Success=true;u.Resolved=true;b.Confidence=Math.Min(100,b.Confidence+8);Emit(g,"Ultimato",p,p.ClubId,"Ultimato cumprido por "+p.Name,u.Description);}else if(u.GamesLeft<=0){u.Resolved=true;failed=true;}}
        bool protectedHistory=p.Titles.Count>=2||recent.Count>=15&&Performance(20)>.65;
        int threshold=Math.Clamp(w.Rules.DismissalThreshold+(50-b.Patience)/3+(50-b.LossTolerance)/6,5,45);
        int chance=b.Confidence<w.Rules.CriticalThreshold?w.Rules.CriticalDismissalChance:b.Confidence<threshold?w.Rules.BaseDismissalChance+(threshold-b.Confidence)*2:b.Confidence<45&&b.Patience<35&&recent.TakeLast(3).All(r=>r.Points==0)?15:0;
        if(failed)chance+=60;if(protectedHistory)chance-=20;
        if(Cash(g,p.ClubId)<contract.Wage*contract.SeveranceMonths*4)chance-=10;
        if(!last.League&&effective<0&&b.TitleDemand>80&&b.Confidence<40)chance+=20;
        bool fire=chance>0&&Draw(g,$"fire:{last.Key}:{p.Id}")<Math.Clamp(chance,0,98);
        if(fire){EndContract(g,p,"Demissão",failed?"Ultimato não cumprido. A diretoria encerrou o contrato.":"Crise de confiança: "+b.Assessments[^1].Reason);return;}
        int ultimatumThreshold=Math.Clamp(w.Rules.UltimatumThreshold+(50-b.Patience)/3,15,50);
        if(b.Confidence<ultimatumThreshold&&(b.Ultimatum==null||b.Ultimatum.Resolved&&recent.Count%8==0)){
            int kind=Draw(g,"ultimatum:"+last.Key+":"+p.Id,4);string type=place>16?"relegation":morale<45?"morale":g.Life!.Competitions.SelectMany(c=>c.Matches).Any(m=>m.Winner==""&&m.Fixture.Round<=g.Round+3&&(m.Fixture.Home==p.ClubId||m.Fixture.Away==p.ClubId))?"qualification":kind==0?"defense":"wins";int games=Math.Clamp(w.Rules.UltimatumGames,2,8),required=type=="wins"?Math.Clamp(w.Rules.UltimatumWins,1,games):type=="defense"?games-1:type=="morale"?60:1;
            b.Ultimatum=new(){Kind=type,GamesLeft=games,Required=required,Description=type switch{"wins"=>$"Vencer {required} dos próximos {games} jogos.","relegation"=>$"Sair da zona de rebaixamento em até {games} jogos.","defense"=>$"Sofrer no máximo um gol em {required} dos próximos {games} jogos.","morale"=>$"Recuperar a moral média do elenco para 60 em {games} jogos.",_=>$"Classificar-se na próxima partida eliminatória em até {games} jogos."}};
            Emit(g,"Ultimato",p,p.ClubId,"Diretoria dá ultimato a "+p.Name,b.Ultimatum.Description);
        }
        if(!isPlayer&&b.Confidence<50){p.Formation=new[]{"4-4-2","4-3-3","3-5-2","4-2-3-1"}[Draw(g,"tactics:"+last.Key+":"+p.Id,4)];p.Style=last.Against>2?"Defensiva":last.Scored==0?"Ofensiva":b.PreferredStyle;}
        if(isPlayer){g.Life!.Board.Confidence=b.Confidence;g.Life.Board.Assessment=b.Assessments[^1].Reason;g.Life.Board.TargetPosition=contract.TargetPosition;}
    }
    public static void SettleRound(GameState g)
    {
        var w=World(g);if(!w.PaidRounds.Add(g.Season+":"+g.Round))return;
        foreach(var p in w.People.Where(p=>p.ClubId!="").ToList()){
            var c=Active(g,p.Id);if(c==null)continue;Money(g,c.ClubId,"Salário semanal do treinador "+p.Name,-c.Wage);
            // AI clubs keep operating when the user leaves them.
            if(c.ClubId!=g.ClubId&&g.OpponentSquads.TryGetValue(c.ClubId,out var squad)){
                var f=g.Fixtures.Concat(g.Divisions?.OtherFixtures??[]).FirstOrDefault(f=>f.Round==g.Round&&(f.Home==c.ClubId||f.Away==c.ClubId));if(f==null)continue;
                long gate=f.Home==c.ClubId?1_700_000:0;Money(g,c.ClubId,"Operação semanal sob comando da IA",1_000_000+gate-squad.Sum(p=>(long)p.Wage)-180_000);
            }
        }
    }
    public static void AdvanceFree(GameState g,int days)
    {
        if(IsEmployed(g))throw new GameRuleException("Acompanhe as partidas do seu clube para avançar enquanto empregado.");
        if(World(g).Dismissals.Any(d=>!d.Acknowledged))throw new GameRuleException("Leia e confirme o comunicado de saída primeiro.");
        if(days is not (1 or 7))throw new GameRuleException("Avance um dia ou sete dias.");
        var w=World(g);var end=w.Date.AddDays(days);
        while(w.Date<end){var date=w.Date.AddDays(1);w.Date=date;
            if(g.Round>g.SeasonRounds&&date.Year>g.Season){ClubCompetitions.Advance(g,true);TransferMarket.NextSeason(g);}
            if(g.Round<=g.SeasonRounds&&FixtureDate(g)<=date){Game.Advance(g);}
            Tick(g,Max(date,w.Date));
        }
    }
}
