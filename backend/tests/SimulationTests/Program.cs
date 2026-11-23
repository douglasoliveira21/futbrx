using Brasileirao;
using System.Text.Json;
if (args.Contains("--physical-ball")) { PhysicalBallTests.Execute(); return; }
if (args.Contains("--training")) { TrainingTests.Run(); return; }
if (args.Contains("--intelligence")) { IntelligenceTests.Run(); return; }
if (args.Contains("--ai-baseline")) { IntelligenceTests.Baseline(); return; }
if (args.Contains("--coach-career")) { CoachCareerTests.Run(); return; }
if (args.Contains("--live-controls")) { LiveControlsTests.Run(); return; }
if (args.Contains("--club-life")) { ClubLifeTests.Run(); return; }
if (args.Contains("--contact")) { ContactTests.Run(); return; }
if (args.Contains("--broadcast")) { BroadcastTests.Run(); return; }
if (args.Contains("--calendar")) { CalendarTests.Run(); return; }
if (args.Contains("--football")) { FootballTests.Run(); FootballDecisionsTests.Run(); return; }
if (args.Contains("--advanced")) { AdvancedTests.Run(); return; }
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
var g = Game.New("pal");
Check(g.Fixtures.Count == 380, "380 jogos em uma temporada");
Check(g.Fixtures.GroupBy(f => f.Round).All(r => r.Count() == 10 && r.SelectMany(f => new[] { f.Home, f.Away }).Distinct().Count() == 20), "Cada clube joga exatamente uma vez por rodada");
Check(g.Fixtures.GroupBy(f => (f.Home, f.Away)).Count() == 380, "Turno e returno sem duplicatas");
Check(g.Clubs.All(c => g.Fixtures.Count(f => f.Home == c.Id) == 19 && g.Fixtures.Count(f => f.Away == c.Id) == 19), "19 partidas em casa e 19 fora");
var a = g.Fixtures[0]; var b = JsonSerializer.Deserialize<Fixture>(JsonSerializer.Serialize(a))!; Game.Simulate(g, a); Game.Simulate(g, b);
Check(JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b), "Simulação determinística para a mesma semente e estado");
Game.Simulate(g, a);
Check(JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b), "Reexecutar a mesma partida não duplica eventos");
var season = Game.New("pal"); for (int i = 0; i < 38; i++) Game.Advance(season);
Check(season.Round == 39 && season.Fixtures.All(f => f.HomeGoals != null), "Temporada completa sem partidas pendentes");
Check(season.Fixtures.All(f => f.Events.Count(e => e.Type == "goal" && e.Team == f.Home) == f.HomeGoals && f.Events.Count(e => e.Type == "goal" && e.Team == f.Away) == f.AwayGoals), "Gols dos eventos conferem com todos os placares");
var table = JsonSerializer.SerializeToElement(Game.Table(season), Game.Json);
Check(table.EnumerateArray().All(r => r.GetProperty("played").GetInt32() == 38), "Classificação contabiliza 38 jogos por clube");
Check(table.EnumerateArray().Sum(r => r.GetProperty("goals").GetInt32()) == table.EnumerateArray().Sum(r => r.GetProperty("against").GetInt32()), "Gols pró e contra se conservam");
try { Game.Advance(season); throw new Exception("Rodada extra aceita"); } catch (GameRuleException) { Console.WriteLine("PASS: Bloqueia rodada após encerramento"); }
Console.WriteLine("Todos os testes do motor passaram.");
void Reject(Action action, string message) { try { action(); throw new Exception("Aceitou dados inválidos: " + message); } catch (GameRuleException) { Console.WriteLine("PASS: " + message); } }
var catalog = Game.DemoCatalog();
catalog.Players[0].Player.Name = "Nome, com \"aspas\"\ne quebra";
var csv = CatalogImport.ToCsv(catalog);
var parsed = CatalogImport.Parse("csv", csv, "csv-roundtrip", 2026, "Fonte de teste", "Licença de teste");
Check(parsed.Players[0].Player.Name == catalog.Players[0].Player.Name && parsed.Clubs.Count == 20 && parsed.Players.Count == 472, "CSV conserva acentos, vírgulas, aspas e quebras de linha");
Reject(() => CatalogImport.Parse("csv", csv + "\"campo sem fim", "bad-csv", 2026, "Fonte", "Licença"), "CSV com aspas abertas é rejeitado");
Reject(() => CatalogImport.Parse("json", "{}", "empty-data", 2026, "Fonte", "Licença"), "Catálogo vazio é rejeitado");
var duplicate = Game.DemoCatalog(); duplicate.Players[1].Player.Id = duplicate.Players[0].Player.Id;
Reject(() => CatalogImport.Validate(duplicate), "IDs de jogadores duplicados são rejeitados");
var incomplete = Game.DemoCatalog(); incomplete.License = "";
Reject(() => CatalogImport.Validate(incomplete), "Fonte e licença são obrigatórias");
var nullRecord = Game.DemoCatalog(); nullRecord.Players[0] = null!;
Reject(() => CatalogImport.Validate(nullRecord), "Registro nulo de jogador é rejeitado sem erro interno");
var semicolon = CatalogImport.Parse("csv", CatalogImport.ToCsv(Game.DemoCatalog()).Replace(',', ';'), "csv-semicolon", 2026, "Fonte", "Licença");
Check(semicolon.Players.Count == 472, "CSV com ponto-e-vírgula é aceito");
var snapshot = Game.New("pal", catalog); catalog.Players[0].Player.Name = "Nome alterado depois";
Check(snapshot.Squad[0].Name != catalog.Players[0].Player.Name, "Carreira copia os dados e preserva seu próprio snapshot");
var match = snapshot.Fixtures[0]; Game.Simulate(snapshot, match);
var replay = MatchSimulation.Run(match.Setup!, match.Seed, true);
Check(replay.HomeGoals == match.HomeGoals && replay.AwayGoals == match.AwayGoals, "Replay reproduz o placar autoritativo");
Check(replay.Frames.Count >= 901 && replay.Frames[0].Second == 0 && replay.Frames[^1].Second == replay.Duration, "Timeline cobre a partida do início ao fim");
Check(replay.Frames.All(f => f.Players.Count == 22 && f.Players.All(p => double.IsFinite(p.X) && double.IsFinite(p.Z) && Math.Abs(p.X) <= 52.5 && Math.Abs(p.Z) <= 34)), "Os 22 jogadores têm posições finitas dentro do campo");
Check(replay.Frames.Zip(replay.Frames.Skip(1)).All(p => p.First.Second < p.Second.Second), "Frames têm timestamps estritamente crescentes");
Check(replay.Frames.All(f => double.IsFinite(f.Ball.X) && double.IsFinite(f.Ball.Y) && Math.Abs(f.Ball.X) <= 55 && Math.Abs(f.Ball.Z) <= (MatchSimulation.SupportsControls(replay.EngineVersion)?35.2:34)), "Trajetória da bola é limitada e finita");
Check(replay.Events.Any(e => e.Type == "pass") && replay.Events.Any(e => e.Type == "interception"), "Motor gera decisões de passe e interceptação");
var differentFormation = match.Setup!.Home.Formation == "3-5-2" ? "4-3-3" : "3-5-2";
var changed = match.Setup! with { Home = match.Setup!.Home with { Formation = differentFormation, Mentality = "Defensiva" } };
var otherReplay = MatchSimulation.Run(changed, match.Seed, true);
Check(JsonSerializer.Serialize(replay.Frames[0]) != JsonSerializer.Serialize(otherReplay.Frames[0]), "Formação muda o posicionamento autoritativo");
Check(JsonSerializer.Serialize(MatchSimulation.Run(match.Setup!, match.Seed, true)) == JsonSerializer.Serialize(replay), "Frames e eventos são determinísticos na reprodução");
Console.WriteLine("Testes de catálogo e replay passaram.");
ManagementTests.Run();

Check(season.PlayerStats.Values.Sum(p => p.Goals) == season.Fixtures.Sum(f => f.HomeGoals!.Value + f.AwayGoals!.Value), "Estatísticas individuais conservam todos os gols da temporada");
var archivedGoals = season.PlayerStats.Values.Sum(p => p.Goals); TransferMarket.NextSeason(season);
Check(season.PlayerStats.Count == 0 && season.PastSeasons[^1].PlayerStats.Sum(p => p.Goals) == archivedGoals, "Virada arquiva estatísticas individuais e zera apenas o novo ano");
SportsTests.Run();
TrainingTests.Run();
YouthTests.Run();
FootballTests.Run();
AdvancedTests.Run();

CalendarTests.Run();

BroadcastTests.Run();

FootballDecisionsTests.Run();
