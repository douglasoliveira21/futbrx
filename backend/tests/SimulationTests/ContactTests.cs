using Brasileirao;
using System.Text.Json;

public static class ContactTests
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    public static void Run()
    {
        Check(!RefereeDecisions.Handball(false,false,false,false), "Contato acidental com braço em posição natural não é infração");
        Check(RefereeDecisions.Handball(true,false,false,false) && RefereeDecisions.Handball(false,true,false,false), "Mão deliberada ou braço antinatural caracteriza infração");
        Check(!RefereeDecisions.Handball(true,true,false,true), "Goleiro dentro da própria área não recebe infração de mão de jogador de linha");
        Check(RefereeDecisions.Handball(false,false,true,false), "Gol imediatamente após toque no braço do próprio marcador não é válido");
        Check(!RefereeDecisions.Advantage(false,true,true) && !RefereeDecisions.Advantage(true,false,true) && RefereeDecisions.Advantage(true,true,true), "Vantagem depende de segurança, posse e oportunidade de continuar");
        Check(RefereeDecisions.ObstructsView(48,0,50,0,40,0) && !RefereeDecisions.ObstructsView(48,2,50,0,40,0) && !RefereeDecisions.ObstructsView(51,0,50,0,40,0), "Interferência exige obstrução efetiva entre bola e goleiro");
        Check(RefereeDecisions.Reviewable("goal") && !RefereeDecisions.Reviewable("yellow_card"), "VAR não reavalia amarelo comum neste conjunto de regras");
        var control=RefereeDecisions.ControlPoint(5,3,Math.PI/2,0,false);
        Check(Math.Abs(control.X-5.33)<.001 && Math.Abs(control.Z-2.915)<.001, "Condução mantém bola adiante e ao lado do pé segundo orientação do portador");
        var g=Game.New("pal");var setup=new MatchSetup(PositionalV6.Version,Sports.BuildTeam(g,"pal"),Sports.BuildTeam(g,"fla"));
        var actions=new HashSet<string>();int tackles=0,offsides=0,reviews=0;
        for(int seed=1;seed<=12;seed++)
        {
            var replay=MatchSimulation.Run(setup,seed,true);
            foreach(var a in replay.Frames.SelectMany(f=>f.Players)) actions.Add(a.Action);
            tackles+=replay.Events.Count(e=>e.Type=="tackle");offsides+=replay.Events.Count(e=>e.Type=="offside");reviews+=replay.Events.Count(e=>e.Type=="var_check");
            Check(replay.Events.Count(e=>e.Type=="goal")==replay.Events.Count(e=>e.Type=="var_confirmed"),$"Gols só entram no placar após confirmação, seed {seed}");
            Check(replay.Events.Count(e=>e.Type=="var_check")==replay.Events.Count(e=>e.Type is "var_confirmed" or "var_overturned"),$"Toda checagem tem decisão final, seed {seed}");
            Check(replay.Events.All(e=>e.Second<=replay.Duration) && replay.Events.Zip(replay.Events.Skip(1)).All(p=>p.First.Second<=p.Second.Second),$"Lances arbitrais têm tempo ordenado dentro da partida, seed {seed}");
        }
        Check(tackles>0 && offsides>0 && reviews>0,"Partidas produzem duelos, impedimento na participação e checagens de gol");
        foreach(var action in new[]{"tackle","fall","shield","challenge","air_duel"}) Check(actions.Contains(action),"Motor transmite animação autoritativa de "+action);
        var old=setup with {EngineVersion=PositionalV5.Version};
        Check(JsonSerializer.Serialize(MatchSimulation.Run(old,42,true))==JsonSerializer.Serialize(PositionalV5.Run(old,42,true)),"Replays v5 permanecem no motor congelado");
        using var runtime=new LiveSimulation();var live=new LiveMatch{Incremental=true,Fixture=new Fixture{Setup=old,Seed=42},WatchedSeconds=22};
        var past=runtime.Advance(live);
        Check(past.EngineVersion==PositionalV5.Version && past.Frames.All(f=>f.Second<=120),"Transmissão v5 em andamento continua na versão antiga após atualizar");
    }
}
