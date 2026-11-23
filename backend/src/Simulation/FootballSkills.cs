namespace Brasileirao;

public static class FootballSkills
{
    // Passe, Finalização, Defesa, Físico and Goleiro are the existing training
    // attributes. Detailed skills extend the same profile, not a parallel model.
    public static readonly string[] Detailed=["Visão","Técnica","Drible","Primeiro toque","Posicionamento","Antecipação","Decisões","Concentração","Trabalho em equipe","Criatividade","Velocidade","Aceleração","Resistência","Força","Agilidade","Compostura"];
    public static void Upgrade(GameState g){foreach(var p in g.Squad.Concat(g.Market).Concat(g.Academy).Concat(g.OpponentSquads.Values.SelectMany(s=>s))){if(!g.Development.TryGetValue(p.Id,out var profile))continue;foreach(var key in Detailed){var foundation=key is "Velocidade" or "Aceleração" or "Resistência" or "Força" or "Agilidade"?"Físico":key is "Posicionamento" or "Antecipação" or "Concentração"?"Defesa":key is "Compostura"?"Finalização":"Passe";int basis=profile.Attributes.GetValueOrDefault(foundation,p.Rating);profile.Attributes.TryAdd(key,Math.Clamp(basis+ClubLife.Seed(g,"skill:"+p.Id+":"+key)%17-8,1,99));}}}
    public static double Get(MatchPlayer p,string key)=>Math.Clamp(p.Skills?.GetValueOrDefault(key,p.Rating)??p.Rating,1,p.NaturalPosition!=""&&p.Position=="GOL"&&p.NaturalPosition!="GOL"&&key=="Goleiro"?25:99);
    public static double Normal(MatchPlayer p,string key)=>Get(p,key)/100;
}
