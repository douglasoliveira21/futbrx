namespace Brasileirao;

public static class TacticalPlans
{
    public static List<SavedTacticalPlan> ForClub(GameState g)=>g.SavedTactics.TryGetValue(g.ClubId,out var plans)?plans:[];
    public static SavedTacticalPlan Save(GameState g,string id,string name,string formationId,string mentality,TacticalPlan plan)
    {
        name=name?.Trim()??"";if(name.Length is <1 or >60)throw new GameRuleException("Informe um nome de até 60 caracteres.");
        if(!Tactics.Mentalities.Contains(mentality))throw new GameRuleException("Mentalidade inválida.");
        if(!FormationCatalog.Templates.Any(t=>t.Id==formationId))throw new GameRuleException("Formação indisponível.");
        Tactics.ValidatePlan(plan,g.Squad.Select(p=>p.Id));
        if(!g.SavedTactics.TryGetValue(g.ClubId,out var plans))g.SavedTactics[g.ClubId]=plans=[];
        var existing=plans.FindIndex(p=>p.Id==id);
        if(id!=""&&existing<0)throw new GameRuleException("Plano não encontrado neste clube.");
        if(existing<0&&plans.Count>=20)throw new GameRuleException("Limite de vinte planos por clube.");
        var saved=new SavedTacticalPlan(existing<0?Guid.NewGuid().ToString("N"):id,name,formationId,mentality,plan with{Roles=plan.Roles==null?null:new(plan.Roles),Positions=plan.Positions==null?null:new(plan.Positions)});
        if(existing<0)plans.Add(saved);else plans[existing]=saved;return saved;
    }
    public static TacticalPlan Materialize(GameState g,SavedTacticalPlan saved)
    {
        var template=FormationCatalog.Templates.Single(t=>t.Id==saved.FormationId);var positions=FormationCatalog.Positions(template);
        var starters=g.Squad.Where(p=>p.Starter&&Sports.Eligible(p)).OrderBy(p=>Tactics.RoleOrder(p.Position)).ThenBy(p=>p.Id).ToList();
        if(starters.Count!=11||starters.Count(p=>p.Position=="GOL")!=1)throw new GameRuleException("Escale onze atletas disponíveis, incluindo um goleiro.");
        var mapped=starters.Select((p,i)=>(p.Id,positions[i])).ToDictionary(x=>x.Id,x=>x.Item2);
        foreach(var (player,point) in saved.Plan.Positions??[])if(starters.Any(p=>p.Id==player))mapped[player]=point;
        return saved.Plan with{Positions=mapped,Roles=saved.Plan.Roles?.Where(r=>g.Squad.Any(p=>p.Id==r.Key)).ToDictionary()};
    }
    public static void Delete(GameState g,string id){if(!g.SavedTactics.TryGetValue(g.ClubId,out var plans)||plans.RemoveAll(p=>p.Id==id)==0)throw new GameRuleException("Plano não encontrado neste clube.");}
    public static TacticalPlan MaterializeLive(SavedTacticalPlan saved,MatchTeam team,MatchReplay replay)
    {
        var positions=FormationCatalog.Positions(FormationCatalog.Templates.Single(t=>t.Id==saved.FormationId));
        int side=team.Id==replay.Setup.Home.Id?0:1;
        var roster=team.Players.Concat(team.Bench??[]).ToDictionary(p=>p.Id);
        var active=replay.Frames.LastOrDefault()?.Players.Where(p=>p.Team==side&&p.Active).OrderBy(p=>Tactics.RoleOrder(p.Position)).ThenBy(p=>p.Id).ToList()??[];
        if(active.Count!=11||active.Count(p=>p.Position=="GOL")!=1)throw new GameRuleException("O plano salvo exige onze jogadores ativos; ajuste a equipe após a expulsão.");
        var mapped=active.Select((p,i)=>(p.Id,positions[i])).ToDictionary(p=>p.Id,p=>p.Item2);
        foreach(var (id,point) in saved.Plan.Positions??[])if(mapped.ContainsKey(id))mapped[id]=point;
        return saved.Plan with{Positions=mapped,Roles=saved.Plan.Roles?.Where(r=>roster.ContainsKey(r.Key)).ToDictionary()};
    }
}
