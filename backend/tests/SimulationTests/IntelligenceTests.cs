using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Brasileirao;

public static class IntelligenceTests
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
    static Dictionary<string,int> Skills(int value)=>Training.Attributes.Concat(FootballSkills.Detailed).ToDictionary(k=>k,_=>value);
    static FootballContext Scene(TacticalPlan? plan=null,string mentality="Equilibrada")
    {
        var setup=Legacy(PositionalV9.Version);var teams=new[]{setup.Home with{Plan=plan??new(),Players=setup.Home.Players.Select(p=>p with{Skills=Skills(80)}).ToList()},setup.Away with{Plan=new(),ComputerControlled=true,Players=setup.Away.Players.Select(p=>p with{Skills=Skills(75)}).ToList()}};
        var players=teams.SelectMany((t,team)=>t.Players.Select((p,i)=>{var point=Tactics.AdvancedSlots(t.Formation,team==0?1:-1)[i];return new FootballObservation(team*11+i,p,team,i,point.X,point.Z,team==0?Math.PI/2:-Math.PI/2,0,0,true);})).ToArray();
        players[6]=players[6] with{X=0,Z=0};players[7]=players[7] with{X=15,Z=-10};players[8]=players[8] with{X=16,Z=10};players[9]=players[9] with{X=32,Z=0};players[12]=players[12] with{X=8,Z=0};players[13]=players[13] with{X=28,Z=24};players[14]=players[14] with{X=35,Z=-22};players[15]=players[15] with{X=40,Z=15};
        teams[0]=teams[0] with{Mentality=mentality};return new(0,2700,5400,0,6,0,.24,0,teams,players,null,0,0);
    }
    public static void Run()
    {
        Check(FormationCatalog.Templates.Select(t=>t.Id).Distinct().Count()==FormationCatalog.Templates.Count,"Catálogo não repete identificadores");
        foreach(var template in FormationCatalog.Templates){var positions=FormationCatalog.Positions(template);Check(positions.Count==11&&positions.All(p=>Math.Abs(p.X)<=1&&Math.Abs(p.Z)<=1),template.Label+": onze posições válidas");}
        Check(!FormationCatalog.Positions(FormationCatalog.Templates.Single(t=>t.Id=="433-offensive")).SequenceEqual(FormationCatalog.Positions(FormationCatalog.Templates.Single(t=>t.Id=="433-defensive"))),"Variações ofensiva e defensiva têm coordenadas distintas");
        foreach(var version in new[]{PositionalV7.Version,PositionalV8.Version})Check(Fingerprint(MatchSimulation.Run(Legacy(version),1729,true))=="F31F797E74B477755965FF73BED72100563C0297B2FFEC13A6541CC0515972F4","Replay "+version+" mantém exatamente frames, eventos, estatísticas e placar anteriores");
        var g=Game.New("pal");var player=g.Squad[3];int passing=g.Development[player.Id].Attributes["Passe"];Training.Upgrade(g);Check(g.Development[player.Id].Attributes["Passe"]==passing&&FootballSkills.Detailed.All(k=>g.Development[player.Id].Attributes.ContainsKey(k)),"Habilidades detalhadas estendem o perfil existente sem duplicar passe ou apagar treino");
        var preset=TacticalPlans.Save(g,"","Plano de teste","3241","Ofensiva",new());var materialized=TacticalPlans.Materialize(g,preset);Check(materialized.Positions?.Count==11,"Plano salvo aplica as onze posições ao elenco atual");
        var savedCareer=new Career();Game.Write(savedCareer,g);var restoredCareer=Game.Read(savedCareer);Check(TacticalPlans.ForClub(restoredCareer).Single().Id==preset.Id,"Planos táticos sobrevivem à serialização da carreira");
        TacticalPlans.Delete(g,preset.Id);Check(TacticalPlans.ForClub(g).Count==0,"Exclusão remove apenas o plano do clube atual");
        var brain=new FootballBrain(73);var c=Scene();brain.Update(c);var owner=c.Players[6];
        var custom=new TacticalPlan(Positions:new(){{c.Players[8].Player.Id,new(-.5,.8)}});
        Tactics.ValidatePlan(custom,c.Players.Where(p=>p.Team==0).Select(p=>p.Player.Id));
        var customScene=Scene(custom);var customBrain=new FootballBrain(73);customBrain.Update(customScene);
        Check(Math.Abs(customBrain.Target(customScene.Players[8]).Z-brain.Target(c.Players[8]).Z)>3,"Posição personalizada altera a referência usada pelo movimento real");
        bool invalid=false;try{Tactics.ValidatePlan(custom with{Positions=new(){{c.Players[8].Player.Id,new(1.1,0)}}},c.Players.Select(p=>p.Player.Id));}catch(GameRuleException){invalid=true;}Check(invalid,"Editor rejeita coordenadas fora do campo no servidor");
        Check(brain.PassingRisk(owner,20,0)>brain.PassingRisk(owner,15,-12),"Percepção identifica interceptação no corredor e valoriza o passe livre");
        var hidden=c.Players.ToArray();hidden[12]=hidden[12] with{X=-45,Z=0};brain.Update(c with{Players=hidden});Check(!brain.Visible(hidden[6]).Any(x=>x.Index==12),"Adversário distante atrás do jogador não é visto com conhecimento perfeito");hidden[12]=hidden[12] with{X=-5};brain.Update(c with{Players=hidden});Check(brain.Visible(hidden[6]).Any(x=>x.Index==12),"Pressão próxima é percebida mesmo pelas costas");
        var weak=c.Players.ToArray();weak[6]=owner with{Player=owner.Player with{Skills=Skills(20)}};brain.Update(c with{Players=weak});int low=brain.Choices(6).Count(x=>x.Kind is not("dribble" or "shield" or "shot"));brain.Update(c);int high=brain.Choices(6).Count(x=>x.Kind is not("dribble" or "shield" or "shot"));Check(high>low,"Visão de jogo amplia opções identificadas, sem mudar a execução do passe");
        var option=brain.Choices(6).First(x=>x.Recipient==7);double good=brain.PassAccuracy(owner,option);brain.Update(c with{Players=weak});Check(good>brain.PassAccuracy(weak[6],option),"Passe e técnica afetam execução separadamente da escolha");
        var highPress=Scene();highPress.Teams[1]=highPress.Teams[1] with{Plan=new(Pressure:"Alta")};brain.Update(highPress);Check(brain.PressingCount==2,"Pressão alta designa somente dois jogadores e preserva cobertura");var lowPress=Scene();lowPress.Teams[1]=lowPress.Teams[1] with{Plan=new(Pressure:"Baixa")};brain.Update(lowPress);Check(brain.PressingCount<=1,"Bloco baixo não envia toda a equipe para a bola");
        double width(TacticalPlan plan){var b=new FootballBrain(1);var scene=Scene(plan);b.Update(scene);return scene.Players.Where(p=>p.Team==0&&p.Slot>0&&p.Index!=6).Average(p=>Math.Abs(b.Target(p).Z));}
        Check(width(new(Width:"Aberta"))>width(new(Width:"Fechada")),"Amplitude muda a ocupação dos corredores");
        var movement=new FootballBrain(2);movement.Update(c);Check(Distance(movement.Target(c.Players[9]).X,movement.Target(c.Players[9]).Z,c.Players[9].X,c.Players[9].Z)>1,"Atacante sem bola busca espaço e não permanece imóvel");
        var fatigueHigh=new FootballBrain(1);var fatigueLow=new FootballBrain(1);var hc=Scene(new(Pressure:"Alta"));var lc=Scene(new(Pressure:"Baixa"));fatigueHigh.Update(hc);fatigueLow.Update(lc);fatigueHigh.Update(hc with{Second=5200});fatigueLow.Update(lc with{Second=5200});Check(fatigueHigh.Fitness(hc.Players[7])<fatigueLow.Fitness(lc.Players[7])&&fatigueHigh.TopSpeed(hc.Players[7],true)<movement.TopSpeed(c.Players[7],true),"Pressão e resistência alteram desgaste e velocidade ao longo do jogo");
        var give=new FootballBrain(7);give.Update(c);give.PassArrived(7,6,0);Check(give.Choices(6).Any(x=>x.Recipient==7&&x.Kind=="give_go"),"Apoio e recepção recente criam oportunidade de tabelinha sem roteiro fixo");
        var keeper=new FootballBrain(9);keeper.Update(c with{BallX=38,BallZ=14});var keeperTarget=keeper.Target(c.Players[11]);Check(Math.Abs(keeperTarget.Z)<4&&Math.Abs(keeperTarget.X)>45,"Goleiro fecha o ângulo sem abandonar a área ou teleportar");
        Check(Tactics.AdvancedSlots("4-2-3-1",1).Count==11&&Tactics.AdvancedSlots("4-2-3-1",1).Count(p=>p.X>20)==1,"4-2-3-1 possui quatro linhas e um atacante central");Check(Tactics.AdvancedSlots("4-4-2",1,10).Count==10,"Estrutura se recompõe com dez jogadores após expulsão");
        var coachBrain=new FootballBrain(4);var late=Scene() with{Second=4300,HomeGoals=0,AwayGoals=1};coachBrain.Update(late);var change=coachBrain.Coach(1,4300,[],0);Check(change?.Mentality=="Defensiva"&&change.Plan.Pressure=="Baixa","Treinador adversário protege vantagem no fim");Check(coachBrain.Coach(1,4400,[],0)==null,"Treinador respeita intervalo entre ajustes");
        var tiredPlayers=late.Players.ToArray();tiredPlayers[12]=tiredPlayers[12] with{Player=tiredPlayers[12].Player with{Fitness=30}};var subBrain=new FootballBrain(4);subBrain.Update(late with{Players=tiredPlayers});var sub=subBrain.Coach(1,4300,[new(9999,"Reserva do teste",tiredPlayers[12].Player.Position,80,95)],0);Check(sub?.OutPlayer==tiredPlayers[12].Player.Id&&sub.InPlayer==9999,"Treinador escolhe reserva do setor desgastado respeitando o banco disponível");
        var clearPlayers=c.Players.ToArray();
        clearPlayers[6]=clearPlayers[6] with { X=40, Z=0, Player=clearPlayers[6].Player with { Position="ATA", Skills=Skills(85) } };
        for(int i=12;i<clearPlayers.Length;i++) clearPlayers[i]=clearPlayers[i] with { X=-35, Z=28 };
        clearPlayers[11]=clearPlayers[11] with { X=51, Z=0 };
        var clearBrain=new FootballBrain(5);
        clearBrain.Update(c with { Players=clearPlayers, BallX=40, BallZ=0 });
        Check(clearBrain.Decide(6).Kind=="shot","Atacante livre reconhece finalização favorável");
        var shotPlayers=c.Players.ToArray();shotPlayers[6]=shotPlayers[6] with{X=45,Z=0,Player=shotPlayers[6].Player with{Skills=Skills(95)}};shotPlayers[11]=shotPlayers[11] with{X=51,Z=3,Player=shotPlayers[11].Player with{Skills=Skills(5)}};var shotBrain=new FootballBrain(2);shotBrain.Update(c with{Players=shotPlayers,BallX=45});Check(Enumerable.Range(0,20).All(seed=>shotBrain.Shoot(6,false,false,new Random(seed)).Result!="save"),"Goleiro sem tempo de reação não executa defesa fisicamente inalcançável");
        var setup=Legacy(PositionalV9.Version) with{Home=c.Teams[0],Away=c.Teams[1]};var watch=System.Diagnostics.Stopwatch.StartNew();var match=MatchSimulation.Run(setup,1729,true);watch.Stop();var summary=MatchSimulation.Run(setup,1729,false);
        Check(match.HomeGoals==summary.HomeGoals&&match.AwayGoals==summary.AwayGoals&&JsonSerializer.Serialize(match.Events,Game.Json)==JsonSerializer.Serialize(summary.Events,Game.Json),"Resumo e transmissão usam as mesmas decisões determinísticas");Check(match.Events.Any(e=>e.Type=="play_pattern")&&match.Events.Any(e=>e.Type is "goal" or "shot"),"Jogadas emergentes e finalizações chegam aos eventos reais do simulador");Check(match.Frames.All(f=>f.Players.All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Z)))&&match.Frames.Count>2000,"Inteligência produz posições finitas durante a partida inteira");
        var instructions=new List<MatchInstruction>{new(1200,"home","tactics","4-2-3-1","Ofensiva",Plan:new("Direto","Alta","Aberta",false,true,true))};var altered=MatchSimulation.Run(setup with{Instructions=instructions},1729,true);
        Check(JsonSerializer.Serialize(match.Frames.Where(f=>f.Second<1200),Game.Json)==JsonSerializer.Serialize(altered.Frames.Where(f=>f.Second<1200),Game.Json),"Instrução futura não altera nenhum frame já simulado");Check(JsonSerializer.Serialize(match.Frames.TakeLast(100),Game.Json)!=JsonSerializer.Serialize(altered.Frames.TakeLast(100),Game.Json),"Mudanças táticas produzem diferenças reais após sua aplicação");
        Console.WriteLine($"{checks} verificações da inteligência passaram. Partida com {match.Frames.Count} frames em {watch.ElapsedMilliseconds} ms; {match.HomeGoals} × {match.AwayGoals}; {match.Events.Count(e=>e.Type=="play_pattern")} jogadas identificadas.");
    }
    static double Distance(double ax,double az,double bx,double bz)=>FootballBrain.Distance(ax,az,bx,bz);
    public static MatchSetup Legacy(string version)=>new(version,Tactics.Team("home",Game.Players(0),"4-3-3","Equilibrada"),Tactics.Team("away",Game.Players(100),"4-4-2","Defensiva"));
    public static string Fingerprint(MatchReplay r)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{r.HomeGoals,r.AwayGoals,r.Duration,r.Events,r.Frames,r.PlayerStats,r.HomePenalties,r.AwayPenalties},Game.Json))));
    public static void Baseline(){foreach(var version in new[]{PositionalV7.Version,PositionalV8.Version})Console.WriteLine(version+" "+Fingerprint(MatchSimulation.Run(Legacy(version),1729,true)));}
}
