using System.Text.Json;
namespace Brasileirao;

public static partial class CoachCareer
{
    public static CoachBalance Defaults { get; set; } = new();
    static CoachWorld World(GameState g)=>g.Life!.Coaches!;
    public static CoachPerson Player(GameState g)=>World(g).People.Single(p=>p.Id==World(g).PlayerId);
    public static bool IsEmployed(GameState g)=>g.Life?.Coaches is not {} w ? g.Life?.Board.Dismissed!=true : w.People.Single(p=>p.Id==w.PlayerId).ClubId!="";
    public static IEnumerable<Club> Clubs(GameState g)=>g.Clubs.Concat(g.Divisions?.OtherClubs??[]).Concat(g.Life?.ExtraClubs??[]).DistinctBy(c=>c.Id);
    static string ClubName(GameState g,string club)=>Clubs(g).FirstOrDefault(c=>c.Id==club)?.Name??club;
    static string NextId(GameState g,string prefix)=>prefix+"-"+(++World(g).Sequence);
    static int Draw(GameState g,string key,int max=100)=>ClubLife.Seed(g,"coach:"+key)%max;
    public static CoachPerson? At(GameState g,string club)=>g.Life?.Coaches?.People.FirstOrDefault(p=>p.ClubId==club&&!p.Retired);
    public static CoachContract? Active(GameState g,string coach)=>World(g).Contracts.SingleOrDefault(c=>c.CoachId==coach&&c.Status=="Ativo");
    static long Cash(GameState g,string club)=>club==g.ClubId?g.Balance:g.ClubBalances.GetValueOrDefault(club,30_000_000);
    static void Money(GameState g,string club,string label,long amount){if(club==g.ClubId){g.Balance+=amount;g.Ledger.Add(new(g.Round,label,amount,g.Season));}else{g.ClubBalances[club]=Cash(g,club)+amount;if(World(g).ClubStorage.TryGetValue(club,out var s))s.Ledger.Add(new(g.Round,label,amount,g.Season));}}
    static long MaxWage(GameState g,string club)=>Math.Clamp(Math.Max(0,Cash(g,club))/150,15_000,250_000);
    static int Target(Club c)=>c.Strength>=80?4:c.Strength>=76?10:16;
    static DateOnly FixtureDate(GameState g)=>g.Fixtures.Where(f=>f.Round==Math.Min(38,g.Round)).Min(f=>f.Date)??new(g.Season,1,20);
    public static void Upgrade(GameState g)
    {
        if(g.Life==null)return;
        bool fresh=g.Life.Coaches==null;
        g.Life.Coaches??=new(){Date=FixtureDate(g).AddDays(-1),Rules=JsonSerializer.Deserialize<CoachBalance>(JsonSerializer.Serialize(Defaults,Game.Json),Game.Json)!};
        var w=World(g);
        if(fresh){
            var player=new CoachPerson{Id=w.PlayerId,Name="Treinador",BirthDate=new(g.Season-35,5,15),ClubId=g.ClubId,Reputation=30,Formation=g.Formation,Style=g.Mentality};w.People.Add(player);
            InitialContract(g,player,g.ClubId,g.Life.Board.TargetPosition);
            Active(g,player.Id)!.Start=(g.Fixtures.Min(f=>f.Date)??w.Date).AddDays(-1);
            foreach(var f in g.Fixtures.Concat(g.Divisions?.OtherFixtures??[]).Where(f=>f.HomeGoals!=null)){w.ProcessedMatches.Add(MatchKey(g,f));w.Date=Max(w.Date,f.Date??w.Date);}
            foreach(var m in g.Life.Competitions.SelectMany(c=>c.Matches).Where(m=>m.Winner!=""))w.ProcessedMatches.Add(MatchKey(g,m.Fixture));
            // Existing completed matches belong to this tenure, without re-awarding money/reputation.
            foreach(var f in g.Fixtures.Where(f=>f.HomeGoals!=null&&(f.Home==g.ClubId||f.Away==g.ClubId)))RecordResult(g,player,f,MatchKey(g,f),true);
            foreach(var f in g.Life.Competitions.SelectMany(c=>c.Matches).Where(m=>m.Winner!="").Select(m=>m.Fixture).Where(f=>f.Home==g.ClubId||f.Away==g.ClubId))RecordResult(g,player,f,MatchKey(g,f),false);
            foreach(var cup in g.Life.Competitions.Where(c=>c.Champion!="")){w.AwardedTitles.Add($"{g.Season}:{cup.Id}");if(cup.Champion==g.ClubId)player.Titles.Add($"{g.Season}: {cup.Name} · {ClubName(g,g.ClubId)}");}
            Emit(g,"Contrato",player,g.ClubId,"Carreira de treinador iniciada","Contrato inicial preservado na migração. Próximas mudanças exigem proposta válida.");
            SnapshotReputation(g,player,"Início do histórico");
        }
        foreach(var club in Clubs(g)){
            if(!w.Boards.ContainsKey(club.Id))w.Boards[club.Id]=new(){ClubId=club.Id,Patience=25+Draw(g,"patience:"+club.Id,61),Ambition=Math.Clamp(club.Strength,40,95),TitleDemand=Math.Clamp((club.Strength-65)*5,15,95),LossTolerance=25+Draw(g,"loss:"+club.Id,60),ExperiencePreference=Draw(g,"experience:"+club.Id),YouthPreference=Draw(g,"young:"+club.Id),AcademyValue=25+Draw(g,"academy:"+club.Id,70),PreferredStyle=new[]{"Equilibrada","Ofensiva","Defensiva"}[Draw(g,"style:"+club.Id,3)],TargetPosition=Target(club),Confidence=club.Id==g.ClubId?g.Life.Board.Confidence:65};
            if(fresh&&club.Id!=g.ClubId){var npc=Generate(g,club.Id);w.People.Add(npc);InitialContract(g,npc,club.Id,Target(club));}
            // New divisions/continental opponents get their own simulated coaches too.
            else if(!fresh&&!w.Contracts.Any(c=>c.ClubId==club.Id)&&At(g,club.Id)==null){var npc=Generate(g,club.Id);w.People.Add(npc);InitialContract(g,npc,club.Id,Target(club));}
        }
        if(fresh){for(int i=0;i<8;i++)w.People.Add(Generate(g,"",i));w.LastTickDay=w.Date.DayNumber;if(g.Life.Board.Dismissed)EndContract(g,Player(g),"Demissão","Contrato encerrado pela diretoria antes da atualização.");}
    }
    static CoachPerson Generate(GameState g,string club,int index=0)
    {
        var w=World(g);int seed=Draw(g,"person:"+club+":"+w.Sequence+":"+index,int.MaxValue);var c=Clubs(g).FirstOrDefault(c=>c.Id==club);
        string[] first=["André","Bruno","Caio","Daniel","Eduardo","Fábio","Gustavo","Henrique","Igor","João","Leandro","Marcos","Paulo","Ricardo","Sérgio","Vítor"],last=["Andrade","Barreto","Campos","Duarte","Esteves","Freitas","Gomes","Lacerda","Moraes","Nogueira","Pacheco","Ramos","Siqueira","Teixeira","Vieira"];
        return new(){Id=NextId(g,"ai"),Name=first[seed%first.Length]+" "+last[(seed/17)%last.Length]+" "+last[(seed/131)%last.Length],ClubId=club,BirthDate=new(w.Date.Year-36-seed%27,1+seed%12,1+seed%27),Reputation=c==null?22+seed%49:Math.Clamp((c.Strength-60)*2+20+seed%12,25,85),Formation=new[]{"4-3-3","4-4-2","3-5-2","4-2-3-1"}[seed%4],Style=new[]{"Equilibrada","Ofensiva","Defensiva"}[seed%3],YouthAffinity=30+seed%65};
    }
    static void InitialContract(GameState g,CoachPerson coach,string club,int target){var w=World(g);w.Contracts.Add(new(){Id=NextId(g,"contract"),CoachId=coach.Id,ClubId=club,Start=w.Date,End=new(w.Date.Year+1,12,31),Wage=Math.Min(MaxWage(g,club),40_000+(long)coach.Reputation*500),TargetPosition=target,TransferBudget=Math.Max(0,Cash(g,club)*3/4)});}
    static void Emit(GameState g,string kind,CoachPerson coach,string club,string title,string text){var w=World(g);w.Timeline.Add(new(NextId(g,"event"),w.Date,kind,coach.Id,club,title,text));g.News.Insert(0,title+" "+text);if(g.News.Count>150)g.News.RemoveRange(150,g.News.Count-150);g.Life!.Press.Insert(0,new(){Season=g.Season,Round=g.Round,Kind="Treinadores",Title=title,Text=text});if(g.Life.Press.Count>80)g.Life.Press.RemoveRange(80,g.Life.Press.Count-80);}
    static void SnapshotReputation(GameState g,CoachPerson p,string reason){p.Reputation=Math.Clamp(p.Reputation,0,100);p.ReputationHistory.Add(new(World(g).Date,g.Season,Math.Round(p.Reputation,2),reason));}
    static string MatchKey(GameState g,Fixture f)=>f.CompetitionMatchId!=""?f.CompetitionMatchId:$"{g.Season}:league:{f.Round}:{f.Home}:{f.Away}";
    static void RecordResult(GameState g,CoachPerson p,Fixture f,string key,bool league){bool h=f.Home==p.ClubId;int scored=(h?f.HomeGoals:f.AwayGoals)??0,against=(h?f.AwayGoals:f.HomeGoals)??0;var squad=p.ClubId==g.ClubId?g.Squad:g.OpponentSquads.GetValueOrDefault(p.ClubId)??[];int youth=f.PlayerStats.Where(s=>s.ClubId==p.ClubId&&squad.Any(x=>x.Id==s.PlayerId&&x.Age<=20)).Sum(s=>s.Minutes);p.Results.Add(new(key,p.ClubId,f.Date??World(g).Date,g.Season,scored,against,league,scored>against?3:scored==against?1:0,youth,Active(g,p.Id)?.Id??""));}
    public static void ProcessCompleted(GameState g)
    {
        Upgrade(g);var w=World(g);
        var all=g.Fixtures.Concat(g.Divisions?.OtherFixtures??[]).Concat(g.Life!.Competitions.SelectMany(c=>c.Matches).Select(m=>m.Fixture)).Where(f=>f.HomeGoals!=null).OrderBy(f=>f.Date).ThenBy(f=>f.Home).ToList();
        foreach(var f in all){string key=MatchKey(g,f);if(!w.ProcessedMatches.Add(key))continue;Tick(g,Max(w.Date,f.Date??w.Date));foreach(var club in new[]{f.Home,f.Away}){var p=At(g,club);if(p==null)continue;RecordResult(g,p,f,key,f.CompetitionMatchId=="");Evaluate(g,p,f);}}
        foreach(var cup in g.Life.Competitions.Where(c=>c.Champion!=""))Award(g,$"{g.Season}:{cup.Id}",cup.Champion,cup.Name);
        if(g.Round==38&&g.Fixtures.All(f=>f.HomeGoals!=null)){var table=LeagueRules.Table(g.Clubs,g.Fixtures,g.LotterySeed+":"+g.Season);Award(g,$"{g.Season}:league",table[0].Club.Id,"Brasileirão · Série "+(g.Divisions?.ManagedDivision??"A"));foreach(var row in table){var b=w.Boards[row.Club.Id];b.PreviousPosition=table.IndexOf(row)+1;}var best=w.People.Where(p=>p.Results.Any(r=>r.Season==g.Season)).OrderByDescending(p=>p.ReputationHistory.Where(x=>x.Season==g.Season).Select(x=>x.Value).DefaultIfEmpty(p.Reputation).Last()-p.ReputationHistory.Where(x=>x.Season==g.Season).Select(x=>x.Value).DefaultIfEmpty(p.Reputation).First()).First();string award=$"Revelação da temporada {g.Season} · prêmio da simulação";if(!best.Awards.Contains(award)){best.Awards.Add(award);Emit(g,"Prêmio",best,best.ClubId,"Reconhecimento da temporada",best.Name+" recebeu "+award+".");}}
        Tick(g,w.Date);
        if(IsEmployed(g)){var p=Player(g);p.Formation=g.Formation;p.Style=g.Mentality;g.Life.Board.Confidence=w.Boards[p.ClubId].Confidence;}
    }
    static DateOnly Max(DateOnly a,DateOnly b)=>a>b?a:b;
    static void Award(GameState g,string key,string club,string title){var w=World(g);if(!w.AwardedTitles.Add(key))return;var p=At(g,club);if(p==null)return;p.Titles.Add($"{g.Season}: {title} · {ClubName(g,club)}");p.Reputation+=title.Contains("Estadual")?2:6;w.Boards[club].Confidence=Math.Min(100,w.Boards[club].Confidence+12);SnapshotReputation(g,p,"Título: "+title);Emit(g,"Título",p,club,p.Name+" conquista "+title,"O título entra no histórico e aumenta sua reputação.");}
    public static string ConfidenceLabel(CoachWorld w,int confidence){string[] labels=["Diretoria extremamente satisfeita","Diretoria satisfeita","Situação estável","Diretoria preocupada","Treinador pressionado","Risco muito alto de demissão","Demissão iminente"];for(int i=0;i<Math.Min(6,w.Rules.ConfidenceBands.Length);i++)if(confidence>=w.Rules.ConfidenceBands[i])return labels[i];return labels[6];}
    public static string ReputationLabel(double r)=>r>=90?"Treinador lendário":r>=75?"Treinador de elite":r>=60?"Treinador consolidado":r>=40?"Treinador em ascensão":r>=20?"Treinador regional":"Treinador desconhecido";
    static int Position(GameState g,string club){var clubs=g.Clubs.Any(c=>c.Id==club)?g.Clubs:g.Divisions?.OtherClubs??[];var fixtures=g.Clubs.Any(c=>c.Id==club)?g.Fixtures:g.Divisions?.OtherFixtures??[];return clubs.Count==0?0:LeagueRules.Table(clubs,fixtures,g.LotterySeed+":"+g.Season).FindIndex(r=>r.Club.Id==club)+1;}
    static void EndContract(GameState g,CoachPerson p,string status,string reason)
    {
        var w=World(g);var contract=Active(g,p.Id);if(contract==null)return;string club=p.ClubId;contract.Status=status;contract.Closed=w.Date;contract.EndReason=reason;long compensation=status=="Demissão"?Math.Min(contract.Wage*contract.SeveranceMonths*4,contract.Wage*Math.Max(0,(contract.End.DayNumber-w.Date.DayNumber+6)/7)):0;
        if(compensation>0)Money(g,club,"Rescisão do treinador "+p.Name,-compensation);
        var results=p.Results.Where(r=>r.ContractId==contract.Id).ToList();
        if(p.Id==w.PlayerId){w.Dismissals.Add(new(){Id=NextId(g,"notice"),CoachName=p.Name,ClubId=club,Date=w.Date,Days=Math.Max(0,w.Date.DayNumber-contract.Start.DayNumber),Played=results.Count,Wins=results.Count(r=>r.Scored>r.Against),Draws=results.Count(r=>r.Scored==r.Against),Losses=results.Count(r=>r.Scored<r.Against),Position=Position(g,club),Titles=p.Titles.Where(t=>t.Contains(ClubName(g,club))).ToList(),Reason=reason,Compensation=compensation,Reaction=g.Life!.Fans.Support<40?"Torcida e imprensa cobram uma reconstrução.":"A saída divide opiniões; o histórico de trabalho foi preservado."});g.Life!.Board.Dismissed=true;}
        p.ClubId="";p.Reputation=Math.Clamp(p.Reputation-(status=="Demissão"?2:0),0,100);SnapshotReputation(g,p,status);Emit(g,status,p,club,status+": "+p.Name+" deixa "+ClubName(g,club),reason);OpenVacancy(g,club,status);ExpireIncompatible(g);
    }
    static void OpenVacancy(GameState g,string club,string reason){var w=World(g);if(w.Vacancies.Any(v=>v.ClubId==club&&v.Status=="Aberta"))return;var b=w.Boards[club];b.VacancyVersion++;b.Ultimatum=null;b.Confidence=55;w.Vacancies.Add(new(){Id=NextId(g,"vacancy"),ClubId=club,Version=b.VacancyVersion,Opened=w.Date,Deadline=w.Date.AddDays(Math.Clamp(w.Rules.VacancySelectionDays,7,30)),Reason=reason});}
    static void SwitchClub(GameState g,string club)
    {
        if(club==g.ClubId){g.Life!.Board.Dismissed=false;return;}
        var w=World(g);var life=g.Life!;var old=g.ClubId;
        life.ClubAcademies[old]=g.Academy;life.ClubStaff[old]=life.Staff;w.ClubStorage[old]=new(){Board=life.Board,Fans=life.Fans,Ledger=g.Ledger};g.OpponentSquads[old]=g.Squad;g.ClubBalances[old]=g.Balance;
        g.Squad=g.OpponentSquads[club];g.OpponentSquads.Remove(club);g.ClubId=club;g.Balance=g.ClubBalances.GetValueOrDefault(club,30_000_000);g.Academy=life.ClubAcademies.GetValueOrDefault(club)??[];life.Staff=life.ClubStaff.GetValueOrDefault(club)??ClubLife.Roles.Select((r,i)=>new StaffMember("initial-"+i,"Equipe técnica "+(i+1),r,55,6000)).ToList();
        var stored=w.ClubStorage.GetValueOrDefault(club);life.Board=stored?.Board??new();life.Fans=stored?.Fans??new(){RivalId=old};g.Ledger=stored?.Ledger??[];life.Board.Dismissed=false;life.Board.CaptainId=-1;life.Board.Warnings=0;life.LastSettledRound=0;life.LastSettledSeason=0;life.Trials.Clear();life.Reports.Clear();life.LastInterviewRound=-1;life.LastTrialRound=-10;g.Scouting=null;g.YouthProspects.Clear();g.WeeklyTraining=new();g.LastTrainingRound=0;g.Substitutions.Clear();g.BenchIds.Clear();g.AutoSubstitute=true;g.AutoReplaceUnavailable=true;
        foreach(var o in g.Offers.Where(TransferMarket.Open)){o.Status="withdrawn";o.Message="Mudança de treinador: a negociação anterior foi encerrada.";}
        if(g.Divisions?.OtherClubs.Any(c=>c.Id==club)==true){(g.Clubs,g.Divisions.OtherClubs)=(g.Divisions.OtherClubs,g.Clubs);(g.Fixtures,g.Divisions.OtherFixtures)=(g.Divisions.OtherFixtures,g.Fixtures);g.Divisions.ManagedDivision=g.Divisions.ManagedDivision=="A"?"B":"A";}
        g.TacticalPlan=new();Sports.RepairSelection(g.Squad,false);Sports.RefreshBench(g);ClubLife.Upgrade(g);
    }
    public static void NewSeason(GameState g){Upgrade(g);var w=World(g);w.Date=Max(w.Date,new(g.Season,1,1));foreach(var b in w.Boards.Values){b.Ultimatum=null;var club=Clubs(g).FirstOrDefault(c=>c.Id==b.ClubId);if(club!=null)b.TargetPosition=Target(club);}Tick(g,w.Date);}
}
