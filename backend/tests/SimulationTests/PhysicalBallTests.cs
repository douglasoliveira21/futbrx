using Brasileirao;
using System.Text.Json;

public static class PhysicalBallTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }

    static bool Run(BallPhysics ball, int direction, int steps=240)
    {
        for(int i=0;i<steps;i++)
        {
            double before=ball.X;
            ball.Step(1.0/120);
            if(ball.CrossedGoalLine(before,direction)) return true;
        }
        return false;
    }

    public static void Execute()
    {
        Check(AerialPlay.Jump(-.1,80,80)==0 && AerialPlay.Jump(1,80,80)==0,"Salto tem preparação e término limitados no tempo");
        Check(AerialPlay.HeadHeight(.4,90,90)>AerialPlay.HeadHeight(.4,20,20),"Força e agilidade influenciam o alcance aéreo");
        Check(!AerialPlay.Reachable(3.5,AerialPlay.HeadHeight(.4,99,99)),"Bola fisicamente fora de alcance não pode ser cabeceada");
        var irregular=new OffsideSnapshot(true,false);
        foreach(var touch in new[]{OpponentBallPlay.Deflection,OpponentBallPlay.DeliberateSave,OpponentBallPlay.GoalFrame})
            Check(irregular.AfterOpponentContact(touch).IsOffence(true),"Desvio, defesa ou trave preserva posição irregular: "+touch);
        Check(!irregular.AfterOpponentContact(OpponentBallPlay.DeliberatePlay).IsOffence(true),"Jogada deliberada permite nova avaliação de impedimento");
        Check(irregular.Interferes(true,false,false)&&irregular.Interferes(false,true,false)&&!irregular.Interferes(false,false,false),"Disputa e obstrução são participação; posição passiva não é infração");
        foreach(var target in new[]{new Position3(8,.11,0),new Position3(30,.11,0),new Position3(-12,.11,0),new Position3(0,.11,25)})
        {
            var delivery=BallDelivery.Ground(new(0,.11,0),target,15);
            delivery.Step(.2);
            Check(delivery.X*target.X+delivery.Z*target.Z>0,"Passe rasteiro aplica impulso na direção pretendida, inclusive para trás e em virada");
        }
        var loft=BallDelivery.Launch(new(0,.11,0),new(25,1.8,10),1.6,4);
        loft.Step(.2);
        Check(loft.Y>BallPhysics.Radius && loft.Spin>0,"Lançamento elevado utiliza gravidade e rotação física");
        var resting=new BallPhysics(0,BallPhysics.Radius,0,.02,0,.01);
        resting.Step(2);
        Check(resting.VX==0 && resting.VZ==0 && resting.VY==0 && resting.Y==BallPhysics.Radius,"Atrito encerra rolamento lento e elimina microquiques de repouso");
        var rolling120=new BallPhysics(0,BallPhysics.Radius,0,5,0,0);
        var rolling240=new BallPhysics(0,BallPhysics.Radius,0,5,0,0);
        for(int i=0;i<120;i++)rolling120.Step(1.0/120);
        for(int i=0;i<240;i++)rolling240.Step(1.0/240);
        Check(Math.Abs(rolling120.VX-rolling240.VX)<.02,"Atrito mantém velocidade consistente com subpassos menores");
        Check(Run(new BallPhysics(48, .5, 0, 20, 1, 0),1), "Bola cruza a linha dentro do gol");
        Check(!Run(new BallPhysics(48, .5, 5, 20, 1, 0),1), "Chute passa ao lado");
        Check(!Run(new BallPhysics(48, 3.2, 0, 20, 0, 0),1), "Chute passa acima");
        var fastOut=new BallPhysics(52,.5,10,55,0,0);
        fastOut.AdvanceInPitch(1.0/6);
        Check(fastOut.X>52.5+BallPhysics.Radius && fastOut.X<53.1,"Saída rápida é interrompida no primeiro subpasso físico fora do campo");
        Check(BallPhysics.PlayerContactFraction(new(0,.5,0),new(8,.5,0),4,0,1.7) is >0 and <1,"Colisão contínua detecta defensor atravessado por um chute rápido");
        Check(BallPhysics.PlayerContactFraction(new(0,3,0),new(8,3,0),4,0,1.7)==null,"Bola acima da cabeça não colide com o corpo");
        Check(BallPhysics.PlayerContactFraction(new(3.47,.5,0),new(2,.5,0),4,0,1.7)==null,"Bola afastando-se após rebote não gera colisão duplicada");
        Check(BallPhysics.PlayerContactFraction(new(0,.5,0),new(8,.5,0),-4,0,1.7)==null,"Defensor atrás do início da trajetória não produz contato fantasma");
        Check(GoalkeeperHandling.CanHold(14,BallPhysics.Radius,.1,80),"Goleiro habilidoso pode encaixar bola rasteira controlável após contato");
        Check(!GoalkeeperHandling.CanHold(30,1,.1,80),"Contato com chute forte exige espalmada em vez de encaixe garantido");
        Check(!GoalkeeperHandling.CanHold(14,1,.8,80),"Contato distante do corpo produz espalmada");
        Check(!GoalkeeperHandling.CanHold(14,2.4,.1,80),"Goleiro não encaixa acima do alcance dos braços");
        var post = new BallPhysics(51, .5, 3.66, 18, 0, 0);
        Run(post,1,30);
        Check(post.FrameHit && post.VX < 0, "Trave produz rebote");
        var fastPost=new BallPhysics(52,.5,3.66,120,0,0);
        fastPost.Step(1.0/120);
        Check(fastPost.FrameHit && fastPost.VX<0,"Colisão contínua impede bola rápida de atravessar a trave entre subpassos");
        var insidePost=new BallPhysics(51,.5,3.51,20,0,0);
        Check(Run(insidePost,1) && insidePost.FrameHit,"Impacto no lado interno da trave desvia a bola para dentro do gol");
        var outsidePost=new BallPhysics(51,.5,3.81,20,0,0);
        Check(!Run(outsidePost,1) && outsidePost.FrameHit,"Impacto no lado externo da trave desvia a bola para fora");
        var bar = new BallPhysics(51, 2.44, 0, 18, 0, 0);
        Run(bar,1,30);
        Check(bar.FrameHit && bar.VX < 0, "Travessão produz rebote");
        var defenders = new[] { (X: 50.0, Yaw: 0.0), (X: 30.0, Yaw: 0.0) };
        Check(FootballDecisions.Offside(35,0,20,1,defenders), "Atacante além do penúltimo defensor está em posição irregular");
        Check(!FootballDecisions.Offside(25,0,20,1,defenders), "Atacante atrás da linha está em posição legal");
        Check(!FootballDecisions.Offside(-5,0,-12,1,defenders), "Atacante no próprio campo está em posição legal");
        Check(!FootballDecisions.Offside(30,0,20,1,defenders), "Atacante em linha com penúltimo defensor está em posição legal");
        var snapshot=OffsideSnapshot.Capture(35,0,20,1,defenders,false);
        Check(snapshot.IsOffence(true), "Posição irregular com participação é infração");
        Check(!snapshot.IsOffence(false), "Posição irregular sem participação não é infração");
        foreach (var restart in new[] { "lateral", "escanteio", "tiro de meta" })
            Check(!OffsideSnapshot.Capture(35,0,20,1,defenders,true).IsOffence(true), "Recepção direta de " + restart + " é isenta");
        Check(!OffsideSnapshot.Capture(-.05,0,-10,1,defenders,false).IsOffence(true), "Receptor no próprio campo não é impedido perto da linha central");
        var attacker = new BallContact(1,0,"chute",10,50,.2,0,"finalização");
        var defender = attacker with { PlayerId=2,Team=1,Type="desvio" };
        Check(BallRestarts.Resolve(53,0,attacker,1)==(BallRestart.GoalKick,1), "Último toque atacante concede tiro de meta");
        Check(BallRestarts.Resolve(53,0,defender,1)==(BallRestart.Corner,0), "Último toque defensor concede escanteio");
        Check(BallRestarts.Resolve(10,35,attacker,1)==(BallRestart.ThrowIn,1), "Saída lateral pertence ao adversário");
        Check(!BallRestarts.IsOut(52.55,0) && !BallRestarts.IsOut(0,34.05),"Bola parcialmente sobre a linha continua em jogo");
        Check(BallRestarts.IsOut(52.62,0) && BallRestarts.IsOut(0,34.12),"Somente a passagem completa da bola caracteriza saída");
        var a=IntelligenceTests.Legacy(PositionalV11.Version);
        var first=MatchSimulation.Run(a, 117, true);
        Check(first.Events.Any(e=>e.Type=="pass_attempt"),"IA executa passes pelo fluxo físico real do V11");
        Check(first.Frames.Any(f=>f.LastContact?.Origin=="jogada" && f.BallVelocity!=null),"Quadros de passes comuns carregam velocidade física autoritativa");
        Check(first.Frames.Any(f=>f.LastContact is {Type:"recepção",Origin:"physical_pass"}),"Recepção de passe ocorre mediante contato com a bola física");
        Check(first.Frames.Any(f=>f.LastContact is {Type:"interceptação",Origin:"physical_pass"}),"Adversário intercepta passe mediante contato com a trajetória");
        Check(first.Events.Any(e=>e.Type=="restart_taken"),"Partida completa executa cobrança física");
        Check(first.Events.Any(e=>e.Type=="fulltime"),"Partida com cobranças físicas chega ao fim");
        Check(first.Frames[^1].Diagnostics is {State:"finished",Restart:null} && first.Frames[^1].Diagnostics!.EventIndex==first.Events.Count-1,"Quadro final encerra a reposição pendente e referencia o apito final oficial");
        Check(first.Frames.Any(f=>f.LastContact!=null) && first.Frames.Any(f=>f.BallVelocity!=null),"V11 exporta contato real e velocidade física para o inspetor");
        Check(first.Frames.Where(f=>f.LastContact!=null).All(f=>f.LastContact!.Second<=f.Second && f.Players.Any(p=>p.Id==f.LastContact.PlayerId && p.Team==f.LastContact.Team)),"Contato exportado identifica jogador e equipe presentes e nunca vem do futuro");
        var contacts=first.Frames.Where(f=>f.LastContact!=null).Select(f=>f.LastContact!).DistinctBy(c=>c.Id).ToList();
        Check(contacts.All(c=>c.Id>0) && contacts.Zip(contacts.Skip(1)).All(p=>p.First.Id<p.Second.Id && p.First.Second<=p.Second.Second),"Registro central gera identificadores crescentes e contatos ordenados sem duplicação");
        Check(first.Frames.Where(f=>f.BallVelocity!=null).All(f=>double.IsFinite(f.BallVelocity!.X)&&double.IsFinite(f.BallVelocity.Y)&&double.IsFinite(f.BallVelocity.Z)),"Velocidades exportadas são finitas");
        var throwIn=first.Events.First(e=>e.Type=="throw_in");
        var throwTaken=first.Events.FirstOrDefault(e=>e.Type=="restart_taken" && e.Second>throwIn.Second && e.Second<=throwIn.Second+60 && e.Team==throwIn.Team && e.Text.Contains("ThrowIn"));
        Check(throwTaken!=null,"Lateral produz cobrança física pela equipe beneficiada antes de retomar o jogo");
        Check(first.Frames.Any(f=>f.Second>=throwIn.Second && f.Second<throwTaken!.Second && f.Diagnostics?.Restart=="ThrowIn"),"Preparação do lateral é exportada para os quadros da transmissão");
        Check(first.Frames.Any(f=>f.Second>=throwTaken!.Second && f.Second<throwTaken.Second+20 && f.BallVelocity!=null),"Cobrança de lateral aplica velocidade física à bola");
        foreach(var version in new[]{PositionalV7.Version,PositionalV8.Version})
        {
            var historical=MatchSimulation.Run(IntelligenceTests.Legacy(version),1729,true);
            Check(IntelligenceTests.Fingerprint(historical)=="F31F797E74B477755965FF73BED72100563C0297B2FFEC13A6541CC0515972F4",version+": preserva o hash histórico de eventos, quadros, placar e estatísticas");
            Check(historical.Frames.All(f=>f.LastContact==null && f.BallVelocity==null),version+": não habilita a instrumentação física do V11");
        }
        var repeat=MatchSimulation.Run(a, 117, true);
        var summary=MatchSimulation.Run(a, 117, false);
        Check(JsonSerializer.Serialize(first,Game.Json)==JsonSerializer.Serialize(repeat,Game.Json), "Mesma versão e semente reproduzem os mesmos eventos e quadros");
        Check(first.HomeGoals==summary.HomeGoals && first.AwayGoals==summary.AwayGoals && JsonSerializer.Serialize(first.Events,Game.Json)==JsonSerializer.Serialize(summary.Events,Game.Json), "Resumo e transmissão produzem os mesmos eventos");
        var validation=Game.New("pal");
        var validationSetup=new MatchSetup(PositionalV11.Version,
            Tactics.Team("fla",validation.OpponentSquads["fla"],"4-3-3","Equilibrada"),
            Tactics.Team("pal",validation.Squad,"4-4-2","Equilibrada"));
        var browserFixture=MatchSimulation.Run(validationSetup,117,true);
        Check(!browserFixture.Events.Zip(browserFixture.Events.Skip(1)).Any(pair=>pair.First.Type=="rebound" && pair.Second.Type=="rebound" && pair.First.Team==pair.Second.Team && pair.First.Player==pair.Second.Player && pair.Second.Second-pair.First.Second<=2),"Cenário do navegador não duplica espalmadas durante um mesmo contato persistente");
        // Fixed scenario matrix: physical passes change which restarts occur in a single seed.
        var aerialMatches=Enumerable.Range(117,8).Select(seed=>seed==117?browserFixture:MatchSimulation.Run(validationSetup,seed,true)).ToList();
        Check(aerialMatches.Any(r=>r.Events.Any(e=>e.Type=="header")),"Partidas completas geram cabeceio físico no fluxo real");
        Check(aerialMatches.Any(r=>r.Frames.Any(f=>f.BallVelocity!=null && f.LastContact?.Type=="cabeceio")),"Cabeceio produz impulso e contato nos quadros autoritativos");
        Check(aerialMatches.Any(r=>r.Events.Any(e=>e.Type=="aerial_challenge")&&r.Frames.Any(f=>f.Players.Any(p=>p.Action=="air_duel"))),"Disputa aérea produz evento e ação de animação na partida");
        var restartContacts=aerialMatches.SelectMany(replay=>replay.Frames.Select(f=>f.LastContact).OfType<BallContact>().Where(c=>c.Type=="cobrança").DistinctBy(c=>c.Id)).ToList();
        Check(restartContacts.Any(c=>c.Origin=="Corner") && restartContacts.Where(c=>c.Origin=="Corner").All(c=>Math.Pow(52.5-Math.Abs(c.X),2)+Math.Pow(34-Math.Abs(c.Z),2)<=1),"Escanteio físico é executado dentro do quarto de círculo, sem deriva do cobrador");
        Check(restartContacts.Any(c=>c.Origin=="GoalKick") && restartContacts.Where(c=>c.Origin=="GoalKick").All(c=>Math.Abs(c.X)>=47 && Math.Abs(c.X)<=52.5 && Math.Abs(c.Z)<=9.16),"Tiro de meta físico é executado dentro da área de meta");
        Check(restartContacts.Where(c=>c.Origin=="ThrowIn").All(c=>Math.Abs(Math.Abs(c.Z)-34)<.001 && c.Y>1.8),"Lateral físico parte das mãos sobre a linha, sem saída artificial antes de entrar em campo");
        // V12: a deflected ball keeps moving instead of hovering while players converge on it.
        static int LongestStill(MatchReplay replay)
        {
            int longest=0,run=0;
            foreach(var (a,b) in replay.Frames.Zip(replay.Frames.Skip(1)))
            {
                // Only live play counts: a ball held for a throw-in, or resting in the net while the goal is
                // checked, is expected to stay still.
                static bool Live(ReplayFrame f)=>f.Diagnostics is {State:"in_play",Restart:null} && Math.Abs(f.Ball.X)<52.5;
                bool still=Live(a) && Live(b) && a.Ball.Y>.5 &&Math.Abs(a.Ball.X-b.Ball.X)<1e-6 && Math.Abs(a.Ball.Y-b.Ball.Y)<1e-6 && Math.Abs(a.Ball.Z-b.Ball.Z)<1e-6;
                run=still?run+1:0;longest=Math.Max(longest,run);
            }
            return longest;
        }
        var legacyStill=LongestStill(browserFixture);
        var currentSetup=validationSetup with{EngineVersion=PositionalV12.Version};
        var current=Enumerable.Range(117,4).Select(seed=>MatchSimulation.Run(currentSetup,seed,true)).ToList();
        Check(current.All(r=>r.EngineVersion==PositionalV12.Version),"Replays positional-v12 continuam no próprio motor");
        Check(legacyStill>=3,"V11 permanece congelada, inclusive a bola suspensa da semente 117 ("+legacyStill+" quadros)");
        Check(current.All(r=>LongestStill(r)<3),"V12: bola desviada no ar não fica suspensa entre jogadores");
        // Two quick deflections (ball off one player into a neighbour) are legitimate; three or more
        // consecutive per-tick deflections were the V11 hovering symptom.
        static int LongestDeflectionChain(MatchReplay replay)
        {
            var rebounds=replay.Events.Where(e=>e.Type=="rebound").ToList();
            int longest=rebounds.Count>0?1:0,chain=1;
            for(int i=1;i<rebounds.Count;i++){chain=rebounds[i].Second-rebounds[i-1].Second<=2?chain+1:1;longest=Math.Max(longest,chain);}
            return longest;
        }
        Check(LongestDeflectionChain(browserFixture)>=3,"V11 mantém a cadeia de desvios registrada na semente 117");
        Check(current.All(r=>LongestDeflectionChain(r)<=2),"V12: não há cadeia de três ou mais desvios a cada passo");
        Check(current.All(r=>r.Events.Where(e=>e.Type=="rebound"&&e.Text.StartsWith("A bola desvia em")).All(e=>e.Player!=0)),"V12: desvio de passe não é narrado como finalização");
        Check(JsonSerializer.Serialize(current[0],Game.Json)==JsonSerializer.Serialize(MatchSimulation.Run(currentSetup,117,true),Game.Json),"V12 é determinística para a mesma semente");
        // V13: throw-ins are released at a human speed to a team-mate within throwing range.
        // Range is the drag-free landing distance (ball centre back to .24 m) from the release velocity.
        static List<(double Speed,double Range)> ThrowIns(MatchReplay replay)=>replay.Frames
            .Where(f=>f.LastContact is {Type:"cobrança",Origin:"ThrowIn"} c && c.Second==f.Second && f.BallVelocity!=null)
            .Select(f=>{
                var v=f.BallVelocity!;var c=f.LastContact!;
                double flightTime=(v.Y+Math.Sqrt(v.Y*v.Y+2*9.81*(c.Y-.24)))/9.81;
                return (Math.Sqrt(v.X*v.X+v.Y*v.Y+v.Z*v.Z),flightTime*Math.Sqrt(v.X*v.X+v.Z*v.Z));
            }).ToList();
        var throwSetup=validationSetup with{EngineVersion=PositionalV13.Version};
        var throwMatches=Enumerable.Range(117,8).Select(seed=>MatchSimulation.Run(throwSetup,seed,true)).ToList();
        var legacyThrows=current.SelectMany(ThrowIns).ToList();
        var throws=throwMatches.SelectMany(ThrowIns).ToList();
        Check(throwMatches.All(r=>r.EngineVersion==PositionalV13.Version),"Replays positional-v13 continuam no próprio motor");
        Check(legacyThrows.Any(t=>t.Speed>25),"V12 permanece congelada com os laterais de 1 s de voo");
        Check(throws.Count>=10,"Partidas V13 têm laterais físicos suficientes para medir ("+throws.Count+")");
        Check(throws.All(t=>t.Speed is >=6 and <=17),"V13: lateral sai entre 6 e 17 m/s (máx. "+throws.Max(t=>t.Speed).ToString("F1")+" m/s)");
        Check(throws.All(t=>t.Range<=PositionalV7.MaxThrowInDistance+1),"V13: lateral não passa de 25 m de alcance (máx. "+throws.Max(t=>t.Range).ToString("F1")+" m)");
        Check(legacyThrows.Any(t=>t.Range>PositionalV7.MaxThrowInDistance+5),"V12 mantém os laterais longos e retos originais");
        Check(PositionalV7.ThrowInFlightSeconds(2)==.6 && PositionalV7.ThrowInFlightSeconds(13)==1 && PositionalV7.ThrowInFlightSeconds(60)==2,"V13: tempo de voo do lateral cresce com a distância, com limites");
        Check(JsonSerializer.Serialize(throwMatches[0],Game.Json)==JsonSerializer.Serialize(MatchSimulation.Run(throwSetup,117,true),Game.Json),"V13 é determinística para a mesma semente");
        // V14: when a throw-in is awarded, outfield team-mates come short so the taker has a receiver.
        // Measured on the release frame: closest outfield team-mate (never the keeper), intended receiver
        // and preparation time from the throw-in being awarded to the release, in clock seconds.
        static List<(double Nearest,double Receiver,int Wait)> ThrowInSupport(MatchReplay replay)=>replay.Events
            .Where(e=>e.Type=="restart_taken" && e.Text.EndsWith("ThrowIn."))
            .Select(e=>{
                var f=replay.Frames.Last(f=>f.Second<=e.Second);var taker=f.Players.First(p=>p.Id==e.Player);
                double From(PlayerPose p)=>Math.Sqrt(Math.Pow(p.X-f.Ball.X,2)+Math.Pow(p.Z-f.Ball.Z,2));
                var mates=f.Players.Where(p=>p.Active && p.Team==taker.Team && p.Id!=taker.Id).ToList();
                var awarded=replay.Events.Last(t=>t.Type=="throw_in" && t.Second<=e.Second);
                return (mates.Where(p=>p.Position!="GOL").Min(From),From(mates.First(p=>p.Id==e.RelatedPlayer)),e.Second-awarded.Second);
            }).ToList();
        var supportSetup=validationSetup with{EngineVersion=PositionalV14.Version};
        var supportMatches=Enumerable.Range(117,8).Select(seed=>MatchSimulation.Run(supportSetup,seed,true)).ToList();
        var legacySupport=throwMatches.SelectMany(ThrowInSupport).ToList();
        var support=supportMatches.SelectMany(ThrowInSupport).ToList();
        Check(MatchSimulation.Version==PositionalV14.Version && supportMatches.All(r=>r.EngineVersion==PositionalV14.Version),"Novas partidas usam positional-v14");
        Check(legacySupport.Count(t=>t.Nearest<=20)*4<legacySupport.Count,"V13 permanece congelada, com companheiros longe do lateral ("+legacySupport.Count(t=>t.Nearest<=20)+" de "+legacySupport.Count+" a até 20 m)");
        Check(support.Count>=10,"Partidas V14 têm laterais físicos suficientes para medir ("+support.Count+")");
        Check(support.Count(t=>t.Nearest<=15)*4>=support.Count*3,"V14: na maioria dos laterais um jogador de linha se oferece a até 15 m ("+support.Count(t=>t.Nearest<=15)+" de "+support.Count+")");
        Check(support.Count(t=>t.Receiver<=20)*3>=support.Count*2,"V14: a maioria dos laterais vai para um receptor a até 20 m ("+support.Count(t=>t.Receiver<=20)+" de "+support.Count+")");
        Check(support.All(t=>t.Wait>=36 && t.Wait<=PositionalV7.ThrowInMaxPreparation),"V14: preparação do lateral entre 36 e "+PositionalV7.ThrowInMaxPreparation+" s de relógio (máx. "+support.Max(t=>t.Wait)+" s)");
        var supportThrows=supportMatches.SelectMany(ThrowIns).ToList();
        Check(supportThrows.All(t=>t.Speed is >=6 and <=17 && t.Range<=PositionalV7.MaxThrowInDistance+1),"V14 mantém a força e o alcance realistas dos laterais V13");
        Check(JsonSerializer.Serialize(supportMatches[0],Game.Json)==JsonSerializer.Serialize(MatchSimulation.Run(supportSetup,117,true),Game.Json),"V14 é determinística para a mesma semente");
    }
}


