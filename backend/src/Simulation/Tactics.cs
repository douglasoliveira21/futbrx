namespace Brasileirao;

public static class Tactics
{
    public static readonly string[] Formations = ["4-3-3", "4-4-2", "3-5-2","4-2-3-1"];
    public static readonly string[] LegacyMentalities = ["Defensiva", "Equilibrada", "Ofensiva"];
    public static readonly string[] Mentalities = ["Ultradefensiva","Muito defensiva","Defensiva","Cautelosa","Equilibrada","Positiva","Ofensiva","Muito ofensiva","Ataque total"];
    public static double MentalityShift(string mentality) => mentality switch
    {
        "Ultradefensiva" => -11, "Muito defensiva" => -8, "Defensiva" => -5, "Cautelosa" => -2.5,
        "Positiva" => 3, "Ofensiva" => 6, "Muito ofensiva" => 9, "Ataque total" => 12, _ => 0
    };
    public static readonly string[] IndividualRoles=["Livre","Apoio","Infiltrar","Pivô","Falso nove","Ultrapassar","Marcar","Cobertura","Subir mais","Recuar","Manter posição","Movimentar livremente","Cortar para dentro","Atacar profundidade","Aproximar para receber","Pressionar mais","Pressionar menos","Marcar jogador","Marcar por zona","Passar curto","Arriscar passes","Chutar mais","Chutar menos","Driblar mais","Driblar menos","Cruzar mais","Atacar a área","Fazer ultrapassagens","Preservar energia"];
    public static void ValidatePlan(TacticalPlan p,IEnumerable<int> players){if(p==null||p.Tempo is not (null or "Baixo" or "Normal" or "Alto")||p.DefensiveTransition is not (null or "Recompor" or "Pressionar após perda")||p.Positions?.Count>40||p.Positions?.Any(r=>!players.Contains(r.Key)||r.Value==null||!double.IsFinite(r.Value.X)||!double.IsFinite(r.Value.Z)||Math.Abs(r.Value.X)>1||Math.Abs(r.Value.Z)>1)==true||!new[]{"Equilibrado","Posse","Contra-ataque","Direto"}.Contains(p.Style)||!new[]{"Baixa","Média","Alta"}.Contains(p.Pressure)||!new[]{"Fechada","Normal","Aberta"}.Contains(p.Width)||!new[]{"Ambos","Esquerda","Direita"}.Contains(p.AttackSide)||p.Roles?.Count>40||p.Roles?.Any(r=>!players.Contains(r.Key)||!IndividualRoles.Contains(r.Value))==true)throw new GameRuleException("Instruções táticas inválidas.");}
    // Only the advanced decision policy uses the new formation. Legacy slot
    // coordinates stay unchanged, including old snapshots with unknown names.
    public static List<(double X,double Z)> AdvancedSlots(string formation,int side,int active=11)
    {
        if(active==11&&formation!="4-2-3-1")return Slots(formation,side);
        int[] rows=active==11?[4,2,3,1]:active>=8?[formation=="3-5-2"?3:4,active-(formation=="3-5-2"?3:4)-3,2]:[Math.Max(2,active-4),2,1];
        var result=new List<(double,double)>{(-49*side,0)};
        for(int row=0;row<rows.Length;row++)for(int col=0;col<rows[row];col++)result.Add(((rows.Length==4?new[]{-29,-12,9,28}[row]:-29+row*22)*side,(col-(rows[row]-1)/2.0)*(rows[row]>=5?11:15)));
        return result;
    }
    public static List<(double X, double Z)> Slots(string formation, int side)
    {
        int[] rows = formation switch { "4-4-2" => [4, 4, 2], "3-5-2" => [3, 5, 2], _ => [4, 3, 3] };
        List<(double, double)> result = [(-49 * side, 0)];
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < rows[row]; column++)
                result.Add(((-29 + row * 22) * side, (column - (rows[row] - 1) / 2.0) * (rows[row] == 5 ? 12 : 15)));
        return result;
    }
    public static int RoleOrder(string position) => position switch { "GOL" => 0, "ZAG" => 1, "LAT" => 2, "MEI" => 3, _ => 4 };
    public static MatchTeam Team(string id, List<Player> squad, string formation, string mentality)
    {
        var players = squad.Where(p => p.Starter && Sports.Eligible(p)).OrderBy(p => RoleOrder(p.Position)).ThenBy(p => p.Id)
            .Select(p => new MatchPlayer(p.Id, p.Name, p.Position, p.Rating, p.Fitness)).ToList();
        if (players.Count != 11 || players.Count(p => p.Position == "GOL") != 1)
            throw new GameRuleException("Escale onze titulares aptos, incluindo exatamente um goleiro.");
        return new(id, formation, mentality, players);
    }
}
