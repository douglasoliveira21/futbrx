using Brasileirao;

// The other nine South American countries: real clubs, national championships by format, qualification and the continental cups.
public static class SouthAmericaTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }

    public static void Run()
    {
        var countries = SouthAmerica.Countries;
        var expected = new Dictionary<string, int> { ["ARG"] = 30, ["URU"] = 16, ["PAR"] = 12, ["CHI"] = 16, ["COL"] = 20, ["ECU"] = 16, ["PER"] = 18, ["BOL"] = 16, ["VEN"] = 14 };
        Check(countries.Count == 9 && countries.All(c => expected[c.Code] == c.Clubs.Count) && countries.Sum(c => c.Clubs.Count) == 158, "Nove países com os clubes de 2026 (30, 16, 12, 16, 20, 16, 18, 16 e 14: 158 clubes)");
        var all = countries.SelectMany(c => c.Clubs).ToList();
        Check(all.Select(c => c.Id).Distinct().Count() == 158 && all.All(c => c.Strength is >= 30 and <= 80 && System.Text.RegularExpressions.Regex.IsMatch(c.Color, "^#[0-9a-fA-F]{6}$") && c.Name != "" && c.City != "" && c.Short.Length <= 5), "Ids únicos, cor, sigla, cidade e força válidas em todos os clubes");
        Check(new[] { "River Plate", "Boca Juniors", "Peñarol", "Nacional", "Olimpia", "Colo-Colo", "Atlético Nacional", "LDU Quito", "Alianza Lima", "Bolívar", "Deportivo Táchira" }.All(n => all.Any(c => c.Name == n)), "Clubes tradicionais de cada país estão na base com o nome real");

        var g = Game.New("pal", Game.Clubs2026Catalog());
        foreach (var country in countries)
        {
            var r = SouthAmerica.Simulate(g, country, 2026); var ids = country.Clubs.Select(c => c.Id).ToHashSet();
            var (libSlots, sulaSlots) = SouthAmerica.Slots(country.Code);
            Check(ids.Contains(r.Champion) && r.Tournaments.Count >= 1 && r.Table.Count == ids.Count && r.Table.All(t => ids.Contains(t.ClubId)) && r.Table.Select(t => t.ClubId).Distinct().Count() == ids.Count, $"{country.Name}: campeão do país, tabela completa e torneios do formato ({r.Tournaments.Count})");
            Check(r.Libertadores.Count == libSlots && r.Sudamericana.Count == sulaSlots && r.Libertadores.Concat(r.Sudamericana).Distinct().Count() == libSlots + sulaSlots && r.Libertadores.Concat(r.Sudamericana).All(ids.Contains), $"{country.Name}: {libSlots} vagas na Libertadores e {sulaSlots} na Sul-Americana, sem repetir clube");
            var again = SouthAmerica.Simulate(g, country, 2026);
            Check(again.Champion == r.Champion && again.Libertadores.SequenceEqual(r.Libertadores) && r.Libertadores.Contains(r.Champion), $"{country.Name}: a temporada é repetível e o campeão vai à Libertadores");
        }
        // Matches played in the round robins follow the format.
        var chile = SouthAmerica.Simulate(g, countries.Single(c => c.Code == "CHI"), 2026); var bolivia = SouthAmerica.Simulate(g, countries.Single(c => c.Code == "BOL"), 2026);
        var paraguay = SouthAmerica.Simulate(g, countries.Single(c => c.Code == "PAR"), 2026); var colombia = SouthAmerica.Simulate(g, countries.Single(c => c.Code == "COL"), 2026);
        Check(chile.Table.All(t => t.Played == 30) && bolivia.Table.All(t => t.Played == 30) && paraguay.Table.All(t => t.Played == 44) && colombia.Table.All(t => t.Played == 38), "Jogos por clube: Chile e Bolívia 30, Paraguai 22 em cada torneio e Colômbia 19 em cada torneio");
        var arg = SouthAmerica.Simulate(g, countries.Single(c => c.Code == "ARG"), 2026);
        Check(arg.Tournaments.Select(t => t.Name).SequenceEqual(["Apertura", "Clausura"]) && arg.Tournaments.All(t => t.Table.All(r => r.Played == 14)) && arg.Tournaments[0].Champion != "" && arg.Libertadores.Contains(arg.Tournaments[0].Champion) && arg.Libertadores.Contains(arg.Tournaments[1].Champion), "Argentina: Apertura e Clausura em duas zonas de 15 (14 jogos por clube) e os dois campeões na Libertadores");
        // Different seasons give different champions over time (the strongest clubs win most, but not always).
        var uru = Enumerable.Range(2026, 12).Select(y => SouthAmerica.Simulate(g, countries.Single(c => c.Code == "URU"), y).Champion).ToList();
        Check(uru.Distinct().Count() >= 2 && uru.Count(c => c is "uru-penarol" or "uru-nacional") >= 6, "Ao longo de doze temporadas os grandes ganham mais, mas não sempre");

        // Continental cups with real clubs.
        var lib = Game.New("pal", Game.Clubs2026Catalog()); lib.Life!.Competitions.Clear();
        lib.Qualification = new QualificationState { Season = lib.Season, Berths = lib.Clubs.OrderBy(c => c.Id).Take(4).Select(c => new Berth(c.Id, "libertadores", "teste")).Concat(lib.Clubs.OrderBy(c => c.Id).Skip(4).Take(2).Select(c => new Berth(c.Id, "pre-libertadores", "teste"))).Concat(lib.Clubs.OrderBy(c => c.Id).Skip(6).Take(6).Select(c => new Berth(c.Id, "sul-americana", "teste"))).ToList() };
        lib.Qualification.Berths[0] = new Berth("pal", "libertadores", "teste"); lib.Qualification.Berths.RemoveAll(b => b.ClubId == "pal" && b != lib.Qualification.Berths[0]);
        Continental.CreateSeason(lib);
        var groups = lib.Life.Competitions.Single(c => c.Id == "libertadores-groups");
        var opponents = groups.Matches.SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Distinct().Where(id => id != "pal").ToList();
        Check(opponents.Count == 3 && opponents.All(id => SouthAmerica.CountryOf(id) != null) && opponents.Select(id => SouthAmerica.CountryOf(id)!.Code).Distinct().Count() == 3 && lib.KnownClubs.Any(c => c.Id == opponents[0]), "Grupo da Libertadores do clube: três adversários reais de três países diferentes, conhecidos pelo nome");
        for (int pass = 0; pass < 12 && lib.Life.Competitions.Single(c => c.Id == "libertadores-groups").Matches.Any(m => m.Fixture.HomeGoals == null); pass++)
            foreach (var m in lib.Life.Competitions.Single(c => c.Id == "libertadores-groups").Matches.Where(m => m.Fixture.HomeGoals == null).ToList()) { m.Fixture.HomeGoals = m.Fixture.Home == "pal" ? 1 : 0; m.Fixture.AwayGoals = m.Fixture.Away == "pal" ? 1 : 0; ClubCompetitions.Complete(lib, m); }
        var knockout = lib.Life.Competitions.Single(c => c.Id == "libertadores");
        var clubsIn = knockout.Matches.Where(m => m.Stage == 0).SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Distinct().ToList();
        Check(clubsIn.Count == 16 && clubsIn.Contains("pal") && clubsIn.Count(id => SouthAmerica.CountryOf(id) != null) >= 9, "Oitavas da Libertadores: 16 clubes, a maioria de clubes reais de outros países");
        Check(lib.SouthAmerica != null && lib.SouthAmerica.Season == lib.Season && lib.SouthAmerica.Results.Count == 9, "As nove ligas nacionais da temporada anterior definem os classificados");
        var sula = Game.New("pal", Game.Clubs2026Catalog()); sula.Life!.Competitions.Clear();
        sula.Qualification = new QualificationState { Season = sula.Season, Berths = [new("pal", "sul-americana", "teste"), .. sula.Clubs.Where(c => c.Id != "pal").OrderBy(c => c.Id).Take(5).Select(c => new Berth(c.Id, "sul-americana", "teste"))] };
        Continental.CreateSeason(sula);
        var sg = sula.Life.Competitions.Single(c => c.Id == "sul-americana-groups");
        Check(sg.Matches.SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Distinct().Count() == 4 && sg.Matches.All(m => m.Fixture.Home == "pal" || m.Fixture.Away == "pal" || SouthAmerica.CountryOf(m.Fixture.Home) != null), "Grupo da Sul-Americana com clubes reais");
        // The season close qualifies the next year's clubs.
        var season = Game.New("pal", Game.Clubs2026Catalog()); season.Life!.Competitions.Clear();
        foreach (var f in season.Fixtures) { f.HomeGoals = 0; f.AwayGoals = 0; }
        season.Round = season.SeasonRounds + 1; TransferMarket.NextSeason(season);
        Check(season.SouthAmerica != null && season.SouthAmerica.Season == 2027 && season.SouthAmerica.Results.All(r => r.Season == 2026), "Ao fim da temporada as ligas dos nove países são simuladas e qualificam os clubes do ano seguinte");
    }
}
