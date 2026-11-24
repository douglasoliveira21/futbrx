using System.Text.Json;

namespace Brasileirao;

public static class LiveBroadcast
{
    // Série B and C careers play their own league; every other base keeps the plain league name.
    static string LeagueTitle(GameState g)=>g.DataVersion.StartsWith("serie-b-2026",StringComparison.Ordinal)?"Brasileirão Série B":g.DataVersion.StartsWith("serie-c-2026",StringComparison.Ordinal)?"Brasileirão Série C":"Brasileirão";
    public const int EntranceSeconds = 12;
    public const int Speed = PositionalV5.TimeScale;
    public static double MatchSecond(LiveMatch live) => Math.Clamp(Math.Round((live.WatchedSeconds - EntranceSeconds) * Speed, 6), 0, live.Duration);
    public static bool Complete(LiveMatch live) => (!live.Incremental || live.EngineFinished) && live.WatchedSeconds >= EntranceSeconds + live.Duration / (double)Speed;
    public static void EnsureIdle(GameState g)
    {
        if (g.LiveMatch != null) throw new GameRuleException("Conclua a transmissão em andamento antes de alterar a carreira.");
    }
    public static LiveMatch Start(GameState g, DateTimeOffset now)
    {
        if (g.LiveMatch != null) return g.LiveMatch;
        ClubLife.Upgrade(g);ClubLife.EnsureEmployed(g);
        var prepared = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(g, Game.Json), Game.Json)!;
        var cup=ClubCompetitions.NextDue(prepared);
        Fixture fixture;
        if(cup!=null){Sports.Prepare(prepared);ClubLife.Travel(prepared,cup.Fixture,ClubCompetitions.CompetitionName(prepared,cup) is "Libertadores" or "Sul-Americana");fixture=cup.Fixture;}
        else{Game.PrepareRound(prepared);fixture=prepared.Fixtures.SingleOrDefault(f=>f.Round==g.Round&&(f.Home==g.ClubId||f.Away==g.ClubId))??throw new GameRuleException("Seu clube não joga nesta semana. Avance a semana para o próximo compromisso.");}
        fixture.Setup=cup!=null?ClubCompetitions.Setup(prepared,prepared.Life!.Competitions.First(c=>c.Matches.Contains(cup)),cup):new(MatchSimulation.Version,Sports.BuildTeam(prepared,fixture.Home),Sports.BuildTeam(prepared,fixture.Away));
        return g.LiveMatch=new(){CompetitionMatchId=cup?.Id??"",CompetitionName=cup==null?LeagueTitle(g):ClubCompetitions.CompetitionName(prepared,cup)+(cup.Fixture.Leg==1?" · Ida":cup.Fixture.Leg==2?" · Volta":""),ManagedTeam=g.ClubId,StartingBalance=g.Balance,Incremental=true,PreparedState=JsonSerializer.Serialize(prepared,Game.Json),Fixture=fixture,Season=g.Season,Duration=MatchSimulation.Duration,LastHeartbeat=now};
    }

    public static void Tick(LiveMatch live, DateTimeOffset now, bool playing, string clientId = "")
    {
        if(MatchSimulation.SupportsControls(live.Fixture.Setup?.EngineVersion)){
            clientId=string.IsNullOrWhiteSpace(clientId)?"legacy":clientId;
            if(clientId.Length>80)throw new GameRuleException("Identificador de transmissão inválido.");
            foreach(var stale in live.Viewers.Where(v=>(now-v.Value.LastSeen).TotalSeconds>10).Select(v=>v.Key).ToList())live.Viewers.Remove(stale);
            if(live.Viewers.Count>=64&&!live.Viewers.ContainsKey(clientId))live.Viewers.Remove(live.Viewers.MinBy(v=>v.Value.LastSeen).Key);
            live.Viewers[clientId]=new(now,playing);
            playing=live.Viewers.Values.Any(v=>v.Playing&&(now-v.LastSeen).TotalSeconds is >=0 and <=3);
        }
        // No credit for time offline, suspended tabs or long gaps. Multiple tabs share one clock.
        var gap = (now - live.LastHeartbeat).TotalSeconds;
        if (live.WasPlaying && playing && !live.HalfTimeAwaiting && !live.StoppagePaused && gap is > 0 and <= 3)
        {
            var entry = Math.Min(gap,Math.Max(0,EntranceSeconds-live.WatchedSeconds));
            var credit = entry + (gap-entry) * (MatchSimulation.SupportsControls(live.Fixture.Setup?.EngineVersion) ? live.PlaybackSpeed/(double)Speed : 1);
            live.WatchedSeconds = live.Incremental && !live.EngineFinished ? live.WatchedSeconds + credit : Math.Min(EntranceSeconds + live.Duration / (double)Speed, live.WatchedSeconds + credit);
        }
        if (now >= live.LastHeartbeat) live.LastHeartbeat = now;
        live.WasPlaying = playing;
    }
    public static GameState Finish(GameState g, Guid id)
    {
        var live = g.LiveMatch ?? throw new GameRuleException("Não há transmissão em andamento.");
        if (live.Id != id || !Complete(live)) throw new GameRuleException("Acompanhe a partida até o apito final.");
        var prepared = JsonSerializer.Deserialize<GameState>(live.PreparedState, Game.Json)!;
        prepared.SavedTactics = g.SavedTactics;
        if (live.Incremental)
        {
            using var engine = new LiveSimulation(); var result = engine.Advance(live);
            if (!live.EngineFinished) throw new GameRuleException("A partida ainda está em andamento.");
            ClubLife.Upgrade(prepared);
            if(live.CompetitionMatchId!=""){
                var cup=prepared.Life!.Competitions.SelectMany(c=>c.Matches).Single(m=>m.Id==live.CompetitionMatchId);cup.Fixture.Setup=live.Fixture.Setup;Game.ApplyResult(cup.Fixture,result);Sports.AfterRound(prepared,[cup.Fixture],true,3,false);ClubLife.Upgrade(prepared);ClubLife.CupGate(prepared,cup.Fixture);ClubLife.MatchReaction(prepared,cup.Fixture,false);ClubCompetitions.Complete(prepared,cup);CoachCareer.ProcessCompleted(prepared);return prepared;
            }
            var fixture = prepared.Fixtures.Single(f => f.Round == live.Fixture.Round && f.Home == live.Fixture.Home);
            fixture.Setup=live.Fixture.Setup;
            Game.ApplyResult(fixture, result);
            Game.CompleteRound(prepared, live.StartingBalance);
        }
        return prepared;
    }
    public static object Public(LiveMatch live) => new
    {
        live.Id,live.CompetitionName,live.CompetitionMatchId, live.Season, live.WatchedSeconds, live.Duration, live.SimulatedSecond, live.Incremental,
        entranceSeconds = EntranceSeconds, speed = MatchSimulation.SupportsControls(live.Fixture.Setup?.EngineVersion) ? live.PlaybackSpeed : Speed,
        live.ManagedTeam, live.PauseAfterSecond, live.StoppagePaused, live.HalfTimeAwaiting, live.HalfTimeAccepted, matchSecond=MatchSecond(live), controlsEnabled=MatchSimulation.SupportsControls(live.Fixture.Setup?.EngineVersion),
        fixture = new { live.Fixture.Round, live.Fixture.Home, live.Fixture.Away, live.Fixture.Seed, live.Fixture.Setup, live.Fixture.Date, live.Fixture.Kickoff,live.Fixture.CompetitionMatchId,live.Fixture.CompetitionName, live.Fixture.Leg, homeGoals = (int?)null, awayGoals = (int?)null, events = Array.Empty<MatchEvent>() }
    };
}
