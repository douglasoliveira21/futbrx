using System.Text.Json;
using Brasileirao;

// Séries B and C playable from the researched club directory, and the directory itself.
public static class SeriesTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }

    public static void Run()
    {
        var b = Game.SeriesCatalog("B");
        var c = Game.SeriesCatalog("C");
        CatalogImport.Validate(b);
        CatalogImport.Validate(c);
        Check(b.Version == Game.SerieB2026Version && c.Version == Game.SerieC2026Version, "Catálogos das Séries B e C têm versões próprias");
        Check(b.Clubs.Count == 20 && c.Clubs.Count == 20, "Séries B e C têm vinte clubes reais cada");
        Check(b.Clubs.Concat(c.Clubs).Select(x => x.Id).Distinct().Count() == 40, "IDs de clubes das Séries B e C não se repetem");
        Check(b.Clubs.Concat(c.Clubs).All(x => x.Short.Length is >= 2 and <= 5 && x.Strength is >= 40 and <= 80), "Siglas e forças dos clubes de B e C estão em faixas válidas");
        Check(b.Clubs.Min(x => x.Strength) > c.Clubs.Max(x => x.Strength) - 5 && b.Clubs.Average(x => x.Strength) > c.Clubs.Average(x => x.Strength) + 4, "Série B é mais forte que a Série C");

        // The directory lists every club of A-D; each playable series must match its catalog.
        using var directory = JsonDocument.Parse(Game.DirectoryJson());
        var clubs = directory.RootElement.GetProperty("clubs").EnumerateArray().ToList();
        string Text(JsonElement e, string name) => e.GetProperty(name).GetString()!;
        Check(clubs.Count == 156, "Diretório reúne 156 clubes das Séries A a D");
        var bySeries = clubs.GroupBy(x => Text(x, "series")).ToDictionary(x => x.Key, x => x.Count());
        Check(bySeries.Count == 4 && bySeries["A"] == 20 && bySeries["B"] == 20 && bySeries["C"] == 20 && bySeries["D"] == 96, "Diretório tem 20 clubes em A, B e C e 96 na D");
        Check(clubs.Select(x => Text(x, "id")).Distinct().Count() == 156, "IDs do diretório são únicos");
        Check(clubs.Select(x => Text(x, "uf")).Distinct().Count() == 27, "Diretório cobre os 27 estados");
        Check(clubs.All(x => Text(x, "id").Length is >= 2 and <= 20 && Text(x, "name").Length > 0 && Text(x, "city").Length > 0), "Todo clube do diretório tem id válido, nome e cidade");
        var a = Game.Clubs2026Catalog();
        foreach (var (series, catalog) in new[] { ("A", a), ("B", b), ("C", c) })
        {
            var listed = clubs.Where(x => Text(x, "series") == series).Select(x => Text(x, "id")).OrderBy(x => x).ToList();
            Check(listed.SequenceEqual(catalog.Clubs.Select(x => x.Id).OrderBy(x => x)), $"Clubes da Série {series} no diretório coincidem com o catálogo jogável");
        }
        var versions = directory.RootElement.GetProperty("versions");
        Check(versions.GetProperty("B")[0].GetString() == Game.SerieB2026Version && versions.GetProperty("C")[0].GetString() == Game.SerieC2026Version && versions.GetProperty("A").GetArrayLength() == 2, "Diretório indica as versões de dados de cada série");

        // Careers: cash and board expectation depend on the series.
        var cashA = Game.New("pal", a).Balance;
        var strongB = Game.New("fortaleza", b);
        var weakB = Game.New("athletic", b);
        var strongC = Game.New("paysandu", c);
        var weakC = Game.New("ypiranga-de-erechim", c);
        Check(strongB.Clubs.Count == 20 && strongB.Fixtures.Count == 380 && strongB.Clubs.Any(x => x.Id == "fortaleza"), "Carreira na Série B joga a liga de vinte clubes reais");
        Check(strongB.Balance == (long)(cashA * .45) && strongC.Balance == (long)(cashA * .2), "Caixa inicial cai nas Séries B e C");
        // The fictional continental clubs of the cup are not part of the league and keep their own cash.
        long Rival(GameState g, string id) => g.ClubBalances[id];
        Check(strongB.Clubs.Where(x => x.Id != "fortaleza").All(x => Rival(strongB, x.Id) < 30_000_000) && strongC.Clubs.Where(x => x.Id != "paysandu").All(x => Rival(strongC, x.Id) < 10_000_000), "Caixa dos adversários da liga também acompanha a série");
        Check(strongB.Life!.Board.TargetPosition == 4 && weakB.Life!.Board.TargetPosition == 16, "Meta da Série B depende da força dentro da própria liga");
        Check(strongC.Life!.Board.TargetPosition == 4 && weakC.Life!.Board.TargetPosition == 16, "Meta da Série C depende da força dentro da própria liga");
        Check(Game.New("pal", a).Life!.Board.TargetPosition == 16 && Game.New("pal", a).Balance == cashA, "Série A mantém a regra e o caixa de antes");
        for (int i = 0; i < 3; i++) Game.Advance(strongB);
        Check(strongB.Round == 4 && strongB.Fixtures.Count(f => f.HomeGoals != null) == 30, "Rodadas da Série B avançam normalmente");
        for (int i = 0; i < 3; i++) Game.Advance(strongC);
        Check(strongC.Round == 4 && strongC.Fixtures.Count(f => f.HomeGoals != null) == 30, "Rodadas da Série C avançam normalmente");
    }
}
