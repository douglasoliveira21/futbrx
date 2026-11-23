using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Brasileirao;

public static class CatalogImport
{
    public const int MaxCharacters = 1_000_000;
    public static SeasonCatalog Parse(string format, string content, string version, int season, string source, string license)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > MaxCharacters)
            throw new GameRuleException("O arquivo deve conter dados e ter no máximo 1 milhão de caracteres.");
        SeasonCatalog data;
        try
        {
            data = format switch
            {
                "json" => JsonSerializer.Deserialize<SeasonCatalog>(content, Game.Json) ?? throw new GameRuleException("JSON sem catálogo."),
                "csv" => FromCsv(content),
                _ => throw new GameRuleException("Formato aceito: json ou csv.")
            };
        }
        catch (JsonException) { throw new GameRuleException("JSON inválido. Confira o modelo de importação."); }
        // Metadados da requisição são explícitos e têm precedência sobre os do arquivo.
        data.Version = version?.Trim() ?? "";
        data.Season = season;
        data.Source = source?.Trim() ?? "";
        data.License = license?.Trim() ?? "";
        Validate(data);
        return data;
    }

    public static void Validate(SeasonCatalog d)
    {
        var errors = new List<string>();
        if (!Regex.IsMatch(d.Version ?? "", @"^[a-zA-Z0-9][a-zA-Z0-9._-]{2,79}$")) errors.Add("Versão: use 3 a 80 letras, números, pontos, hífens ou sublinhados.");
        if (d.Season is < 1900 or > 2200) errors.Add("Temporada fora de 1900–2200.");
        if (string.IsNullOrWhiteSpace(d.Source) || d.Source.Length > 500) errors.Add("Informe a fonte dos dados (até 500 caracteres).");
        if (string.IsNullOrWhiteSpace(d.License) || d.License.Length > 500) errors.Add("Informe a licença/autorização dos dados (até 500 caracteres).");
        if (d.Clubs is null || d.Players is null) throw new GameRuleException("Listas clubs e players são obrigatórias.");
        if (d.Players.Any(p => p is null || p.Player is null || p.ClubId is null)) throw new GameRuleException("Registros de jogador e vínculo não podem ser nulos.");
        if (d.Clubs.Count != 20 || d.Clubs.Any(c => c is null) || d.Clubs.Select(c => c.Id).Distinct().Count() != 20)
            errors.Add("O catálogo da Série A deve conter exatamente vinte clubes com IDs distintos.");
        if (d.Clubs.Any(c => c is null)) throw new GameRuleException(string.Join(" ", errors));
        foreach (var c in d.Clubs)
            if (!Regex.IsMatch(c.Id ?? "", @"^[a-z0-9-]{2,20}$") || string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 80 ||
                string.IsNullOrWhiteSpace(c.Short) || c.Short.Length > 5 || !Regex.IsMatch(c.Color ?? "", "^#[a-fA-F0-9]{6}$") || c.Strength is < 1 or > 99)
                errors.Add($"Clube inválido: {c.Id}. Confira nome, sigla, cor hexadecimal e força de 1 a 99.");
        if (d.Players.Count > 1500 || d.Players.Select(p => p?.Player?.Id).Distinct().Count() != d.Players.Count)
            errors.Add("Limite de 1.500 jogadores com IDs únicos no catálogo.");
        foreach (var entry in d.Players)
        {
            var p = entry?.Player;
            if (p is null || p.Id < 0 || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 80 || p.Age is < 15 or > 50 ||
                p.Rating is < 1 or > 99 || p.Value is < 0 or > 2_000_000_000 || p.Wage is < 0 or > 10_000_000 ||
                !new[] { "GOL", "ZAG", "LAT", "MEI", "ATA" }.Contains(p.Position) ||
                (entry!.ClubId != "" && !d.Clubs.Any(c => c.Id == entry.ClubId)))
                errors.Add($"Jogador inválido: {p?.Id}. Confira vínculo, posição, idade, habilidade e valores.");
        }
        foreach (var c in d.Clubs)
        {
            var squad = d.Players.Where(p => p?.ClubId == c.Id && p.Player != null).Select(p => p.Player).ToArray();
            if (squad.Length is < 18 or > 40 || squad.Count(p => p.Position == "GOL") < 2 || squad.Count(p => p.Position != "GOL") < 16)
                errors.Add($"{c.Name}: são necessários 18 a 40 jogadores, incluindo pelo menos dois goleiros.");
        }
        if (errors.Count > 0) throw new GameRuleException(string.Join(" ", errors.Take(12)));
    }

    // CSV RFC 4180: aceita aspas escapadas, quebras em campos e separador vírgula ou ponto-e-vírgula.
    static List<string[]> ReadRows(string csv)
    {
        csv = csv.TrimStart('\uFEFF');
        var first = csv.Split('\n', 2)[0];
        char delimiter = first.Contains(';') ? ';' : ',';
        List<string[]> rows = []; List<string> row = []; var cell = new StringBuilder();
        bool quoted = false, closed = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char ch = csv[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') { quoted = false; closed = true; }
                else cell.Append(ch);
            }
            else if (ch == '"' && cell.Length == 0 && !closed) quoted = true;
            else if (ch == delimiter) { row.Add(cell.ToString()); cell.Clear(); closed = false; }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); closed = false;
                if (row.Any(s => s.Length > 0)) rows.Add([.. row]);
                row.Clear();
            }
            else if (closed || ch == '"') throw new GameRuleException("CSV com aspas ou separadores inválidos.");
            else cell.Append(ch);
        }
        if (quoted) throw new GameRuleException("CSV contém um campo com aspas não fechadas.");
        row.Add(cell.ToString()); if (row.Any(s => s.Length > 0)) rows.Add([.. row]);
        return rows;
    }
    static SeasonCatalog FromCsv(string csv)
    {
        var rows = ReadRows(csv);
        string[] required = ["kind", "id", "clubId", "name", "short", "color", "strength", "position", "age", "rating", "value", "wage"];
        if (rows.Count < 2 || rows[0].Distinct().Count() != rows[0].Length || required.Except(rows[0]).Any())
            throw new GameRuleException("Cabeçalho CSV inválido. Use o modelo para download.");
        var header = rows[0]; var d = new SeasonCatalog();
        foreach (var row in rows.Skip(1))
        {
            if (row.Length != header.Length) throw new GameRuleException("Uma linha CSV tem quantidade incorreta de colunas.");
            string Get(string name) => row[Array.IndexOf(header, name)].Trim();
            int Number(string name) => int.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : throw new GameRuleException($"Número inválido na coluna {name}: {Get(name)}.");
            if (Get("kind") == "club") d.Clubs.Add(new(Get("id"), Get("name"), Get("short"), Get("color"), Number("strength")));
            else if (Get("kind") == "player") d.Players.Add(new(Get("clubId"), new() { Id = Number("id"), Name = Get("name"), Position = Get("position"), Age = Number("age"), Rating = Number("rating"), Value = Number("value"), Wage = Number("wage") }));
            else throw new GameRuleException("Coluna kind deve conter club ou player.");
        }
        return d;
    }
    public static string ToCsv(SeasonCatalog d)
    {
        static string Escape(object? value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture)!.Replace("\"", "\"\"") + "\"";
        var b = new StringBuilder("kind,id,clubId,name,short,color,strength,position,age,rating,value,wage\r\n");
        foreach (var c in d.Clubs) b.AppendLine(string.Join(',', new object[] { "club", c.Id, "", c.Name, c.Short, c.Color, c.Strength, "", "", "", "", "" }.Select(Escape)));
        foreach (var e in d.Players) b.AppendLine(string.Join(',', new object[] { "player", e.Player.Id, e.ClubId, e.Player.Name, "", "", "", e.Player.Position, e.Player.Age, e.Player.Rating, e.Player.Value, e.Player.Wage }.Select(Escape)));
        return b.ToString();
    }
}
