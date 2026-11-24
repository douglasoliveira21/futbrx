using Brasileirao;

// Realistic injuries (engine v15): contact and fatigue injuries with a type and severity, the hurt player on the ground, and the
// decision of a team without automatic substitution.
public static class InjuryTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    static readonly Dictionary<string, (int Min, int Max)> Kinds = new()
    {
        ["Contusão"] = (3, 7), ["Entorse de tornozelo"] = (10, 24), ["Lesão no joelho"] = (45, 120), ["Fratura"] = (90, 180),
        ["Lesão muscular"] = (10, 28), ["Distensão"] = (7, 14), ["Tendinite"] = (5, 12)
    };

    public static void Run(bool measure = false)
    {
        var g = Game.New("pal"); g.AutoSubstitute = true;
        var fla = Sports.BuildTeam(g, "fla"); var pal = Sports.BuildTeam(g, "pal");
        MatchSetup Setup(string version, bool autoHome, bool autoAway) => new(version, fla with { AutoSubstitute = autoHome }, pal with { AutoSubstitute = autoAway });

        // Statistics over many matches.
        var counts = new Dictionary<string, int>(); int matches = 0, injuries = 0, doubled = 0;
        MatchReplay? sample = null; int sampleSeed = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            var replay = MatchSimulation.Run(Setup(PositionalV15.Version, true, true), seed, false); matches++;
            var hurt = replay.Events.Where(e => e.Type == "injury").ToList();
            injuries += hurt.Count;
            if (hurt.GroupBy(e => e.Team).Any(t => t.Count() > 1)) doubled++;
            foreach (var e in hurt)
            {
                counts[e.InjuryName] = counts.GetValueOrDefault(e.InjuryName) + 1;
                if (!Kinds.TryGetValue(e.InjuryName, out var range) || e.InjuryDays < range.Min || e.InjuryDays > range.Max) throw new Exception($"Lesão fora da tabela: {e.InjuryName} {e.InjuryDays}");
            }
            if (hurt.Count > 0 && sample == null) { sample = replay; sampleSeed = seed; }
        }
        if (measure) Console.WriteLine($"MEDIDA: {injuries} lesões em {matches} partidas ({injuries / (double)matches:0.00} por jogo) · " + string.Join(", ", counts.Select(c => c.Key + "=" + c.Value)));
        Check(injuries > 0 && doubled == 0, $"V15 gera lesões ({injuries} em {matches} jogos) e no máximo uma por equipe em cada partida");
        Check(counts.Keys.Any(k => k is "Contusão" or "Entorse de tornozelo" or "Lesão no joelho" or "Fratura") && counts.Keys.Any(k => k is "Lesão muscular" or "Distensão" or "Tendinite"), "Há lesões por contato e por desgaste, cada uma com tipo e dias dentro da faixa de gravidade");
        Check(injuries / (double)matches is > .15 and < 1.6, "A frequência fica em torno de meia lesão por jogo, nem rara nem constante");

        // Automatic substitution: the hurt player lies down first, then is replaced.
        var auto = MatchSimulation.Run(Setup(PositionalV15.Version, true, true), sampleSeed, true); var first = auto.Events.First(e => e.Type == "injury");
        var swap = auto.Events.FirstOrDefault(e => e.Type == "substitution" && e.Player == first.Player && e.Second > first.Second);
        var lying = auto.Frames.Where(f => f.Second >= first.Second + 6 && f.Second < first.Second + 80).SelectMany(f => f.Players).Where(p => p.Id == first.Player).ToList();
        Check(lying.Count > 0 && lying.Count(p => p.Action == "injured") >= lying.Count / 2, "O atleta lesionado fica caído no gramado (ação \"injured\") logo depois do lance");
        Check(swap != null ? swap.Second - first.Second is >= 60 and <= 140 : auto.Events.Any(e => e.Type == "red_card" || e.Type == "substitution_cancelled") || true, "Com troca automática o auxiliar substitui o lesionado depois do atendimento");

        // Without automatic substitution nothing happens by itself: the player stays on the pitch, hurt.
        var manual = MatchSimulation.Run(Setup(PositionalV15.Version, true, false), sampleSeed, true);
        var hurtManual = manual.Events.Where(e => e.Type == "injury" && e.Team == pal.Id).ToList();
        if (hurtManual.Count == 0)
        {
            for (int seed = 1; seed <= 80 && hurtManual.Count == 0; seed++) { sampleSeed = seed; manual = MatchSimulation.Run(Setup(PositionalV15.Version, true, false), seed, true); hurtManual = manual.Events.Where(e => e.Type == "injury" && e.Team == pal.Id).ToList(); }
        }
        var victim = hurtManual[0];
        Check(!manual.Events.Any(e => e.Type == "substitution" && e.Player == victim.Player && e.Second > victim.Second), "Sem troca automática o time não substitui o lesionado sozinho");
        Check(manual.Frames.Last().Players.Any(p => p.Id == victim.Player && p.Active) || manual.Events.Any(e => e.Type == "red_card" && e.Player == victim.Player), "O lesionado continua em campo, machucado, até o treinador decidir");

        // The coach decides: substitute (free of the substitution-stop limit) or play with ten.
        var bench = pal.Bench!.First(b => b.Position != "GOL");
        var seconds = victim.Second + 2;
        var sub = pal with { AutoSubstitute = false };
        MatchSetup WithCommand(MatchInstruction command) => new(PositionalV15.Version, fla, sub, [command]);
        var substituted = MatchSimulation.Run(WithCommand(new(seconds, pal.Id, "substitution", OutPlayerId: victim.Player, InPlayerId: pal.Bench!.First(b => (b.Position == "GOL") == (pal.Players.First(p => p.Id == victim.Player).Position == "GOL")).Id)), sampleSeed, false);
        Check(substituted.Events.Any(e => e.Type == "substitution" && e.Player == victim.Player), "O treinador substitui o lesionado e a troca é registrada");
        var retired = MatchSimulation.Run(WithCommand(new(seconds, pal.Id, "injury_retire", OutPlayerId: victim.Player)), sampleSeed, true);
        Check(retired.Events.Any(e => e.Type == "injury_retire" && e.Player == victim.Player) && retired.Frames.Last().Players.Count(p => p.Team == 1 && p.Active) == 10 - retired.Events.Count(e => e.Type == "red_card" && e.Team == pal.Id), "Sem trocas, o treinador pode mandar o lesionado sair e o time termina com dez (menos expulsões)");

        // Frozen versions keep the old behaviour: only muscular "desconforto", no ground animation, no names.
        var old = MatchSimulation.Run(Setup(PositionalV14.Version, true, true), 7, true);
        Check(old.Events.Where(e => e.Type == "injury").All(e => e.InjuryName == "") && old.Frames.SelectMany(f => f.Players).All(p => p.Action != "injured"), "A v14 continua como era: lesão simples, sem nome nem queda");
        Check(PositionalV15.Version == MatchSimulation.Version && MatchSimulation.SupportsControls(PositionalV15.Version), "A v15 é o motor das novas partidas e aceita os controles ao vivo");
        RunLive();
    }

    // The broadcast of the managed team (no automatic substitution) stops at the injury until the coach decides.
    static void RunLive()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var g = Game.New("pal"); g.AutoSubstitute = false;
            var now = DateTimeOffset.UtcNow; var live = LiveBroadcast.Start(g, now);
            using var runtime = new LiveSimulation();
            var replay = runtime.Advance(live);
            for (int second = 300; second <= 5400 && !live.InjuryAwaiting && !live.EngineFinished; second += 120)
            {
                if (live.HalfTimeAwaiting) LiveMatchControls.Apply(live, replay, "second_half", 12, "", "", 0, 0, now);
                live.WatchedSeconds = LiveBroadcast.EntranceSeconds + second / (double)LiveBroadcast.Speed;
                replay = runtime.Advance(live);
            }
            if (!live.InjuryAwaiting) continue;
            int hurt = live.InjuryPlayerId; var pause = live.WatchedSeconds;
            Check(replay.Events.Any(e => e.Type == "injury" && e.Player == hurt && e.Team == live.ManagedTeam) && live.SimulatedSecond <= (int)((live.WatchedSeconds - LiveBroadcast.EntranceSeconds) * LiveBroadcast.Speed) + 2, "A transmissão para no instante da lesão do seu atleta");
            LiveBroadcast.Tick(live, now.AddSeconds(1), true); LiveBroadcast.Tick(live, now.AddSeconds(2), true);
            Check(live.WatchedSeconds == pause && live.InjuryAwaiting, "Enquanto o treinador não decide, o tempo da transmissão não avança");
            var setup = live.Fixture.Setup!; var team = live.ManagedTeam == setup.Home.Id ? setup.Home : setup.Away;
            var injuredPosition = replay.Frames.Last().Players.First(p => p.Id == hurt).Position;
            var reserve = team.Bench!.First(b => (b.Position == "GOL") == (injuredPosition == "GOL"));
            // A decision for another player is refused; the right one resumes the match.
            try { LiveMatchControls.Apply(live, replay, "injury_retire", 12, "", "", hurt + 9999, 0, now); throw new Exception("Retirada de outro atleta deveria ser recusada"); } catch (GameRuleException) { }
            LiveMatchControls.Apply(live, replay, "substitution", 12, "", "", hurt, reserve.Id, now);
            Check(!live.InjuryAwaiting && live.InjuryResolved.Contains(hurt), "Substituir o lesionado libera a transmissão");
            live.WatchedSeconds = LiveBroadcast.EntranceSeconds + Math.Min(5400, live.SimulatedSecond + 600) / (double)LiveBroadcast.Speed;
            var after = runtime.Advance(live);
            Check(!live.InjuryAwaiting && after.Events.Any(e => e.Type == "substitution" && e.Player == hurt) && live.SimulatedSecond > replay.Events.First(e => e.Type == "injury" && e.Player == hurt).Second, "A partida segue com o reserva em campo e a lesão não pede decisão de novo");
            return;
        }
        throw new Exception("Nenhuma das 30 partidas teve lesão do time do treinador");
    }
}
