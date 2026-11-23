using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Brasileirao.IntegrationTests;

public static class IntelligenceApiTests
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    static async Task<JsonElement> Json(HttpResponseMessage r){string text=await r.Content.ReadAsStringAsync();if(!r.IsSuccessStatusCode)throw new Exception(r.StatusCode+": "+text);return JsonDocument.Parse(text).RootElement.Clone();}
    public static async Task Run(HttpClient owner,HttpClient other,string careerId)
    {
        string path="/api/careers/"+careerId;var c=await Json(await owner.GetAsync(path));int revision=c.GetProperty("revision").GetInt32();int player=c.GetProperty("state").GetProperty("squad")[3].GetProperty("id").GetInt32();
        var plan=new TacticalPlan("Posse","Alta","Aberta",true,true,false,true,true,"Esquerda",new(){{player,"Apoio"}});
        Check((await other.PostAsJsonAsync(path+"/tactical-plan",new{revision,plan})).StatusCode==HttpStatusCode.NotFound,"Outra conta não altera instruções de jogo");
        Check((await owner.PostAsJsonAsync(path+"/tactical-plan",new{revision,plan=plan with{Pressure="Sem limite"}})).StatusCode==HttpStatusCode.BadRequest,"Servidor rejeita instruções fora dos limites");
        Check((await owner.PostAsJsonAsync(path+"/tactical-plan",new{revision,plan=plan with{Roles=new(){{99999999,"Apoio"}}}})).StatusCode==HttpStatusCode.BadRequest,"Instruções individuais exigem jogador do próprio elenco");
        var saved=await Json(await owner.PostAsJsonAsync(path+"/tactical-plan",new{revision,plan,formation="4-2-3-1",mentality="Ofensiva"}));revision=saved.GetProperty("revision").GetInt32();c=await Json(await owner.GetAsync(path));Check(c.GetProperty("state").GetProperty("formation").GetString()=="4-2-3-1"&&c.GetProperty("state").GetProperty("tacticalPlan").GetProperty("pressure").GetString()=="Alta","Formação, mentalidade e plano são salvos na mesma revisão");
        var nine=await Json(await owner.PostAsJsonAsync(path+"/tactical-plan",new{revision,plan,formation="4-2-3-1",mentality="Ataque total"}));revision=nine.GetProperty("revision").GetInt32();c=await Json(await owner.GetAsync(path));
        Check(c.GetProperty("state").GetProperty("mentality").GetString()=="Ataque total","Carreira aceita as nove mentalidades do vestiário da partida");
        Check((await owner.PostAsJsonAsync(path+"/tactical-plan",new{revision,plan,formation="4-2-3-1",mentality="Inventada"})).StatusCode==HttpStatusCode.BadRequest,"Mentalidade desconhecida continua recusada");
        var restored=await Json(await owner.PostAsJsonAsync(path+"/tactical-plan",new{revision,plan,formation="4-2-3-1",mentality="Ofensiva"}));revision=restored.GetProperty("revision").GetInt32();
        var directory=await Json(await owner.GetAsync("/api/directory"));
        Check(directory.GetProperty("clubs").GetArrayLength()==156&&directory.GetProperty("versions").GetProperty("B")[0].GetString()=="serie-b-2026-clubes-reais-v1","API entrega o diretório de 156 clubes e as versões por série");
        var catalogs=await Json(await owner.GetAsync("/api/catalogs"));var versions=catalogs.EnumerateArray().Select(x=>x.GetProperty("version").GetString()).ToList();
        Check(versions.Contains("serie-b-2026-clubes-reais-v1")&&versions.Contains("serie-c-2026-clubes-reais-v1"),"Catálogos das Séries B e C estão publicados");
        Check((await owner.PostAsJsonAsync(path+"/tactical-plan",new{revision=revision-1,plan})).StatusCode==HttpStatusCode.Conflict,"Revisão impede sobrescrever plano por uma tela antiga");
        var catalog=await Json(await owner.GetAsync(path+"/sports/tactical-catalog"));Check(catalog.GetProperty("formations").GetArrayLength()>=40,"Catálogo de formações disponível ao dono da carreira");
        Check((await other.GetAsync(path+"/sports/tactical-catalog")).StatusCode==HttpStatusCode.NotFound,"Catálogo e planos da carreira não ficam disponíveis para outra conta");
        var preset=await Json(await owner.PostAsJsonAsync(path+"/sports/tactical-presets",new{revision,id="",name="Teste 3-2-4-1",formationId="3241",mentality="Ofensiva",plan}));revision=preset.GetProperty("revision").GetInt32();string presetId=preset.GetProperty("result").GetProperty("id").GetString()!;
        Check((await owner.PostAsJsonAsync(path+"/sports/tactical-presets/apply",new{revision=revision-1,id=presetId})).StatusCode==HttpStatusCode.Conflict,"Aplicação de plano exige revisão atual");
        preset=await Json(await owner.PostAsJsonAsync(path+"/sports/tactical-presets/apply",new{revision,id=presetId}));revision=preset.GetProperty("revision").GetInt32();c=await Json(await owner.GetAsync(path));Check(c.GetProperty("state").GetProperty("tacticalPlan").GetProperty("positions").EnumerateObject().Count()==11,"Aplicação do plano grava onze posições na carreira");
        catalog=await Json(await owner.GetAsync(path+"/sports/tactical-catalog"));Check(catalog.GetProperty("plans").EnumerateArray().Any(p=>p.GetProperty("id").GetString()==presetId),"Plano salvo é recuperado pela API");
        var live=await Json(await owner.PostAsJsonAsync(path+"/live/start",new{revision}));string id=live.GetProperty("id").GetString()!;var setup=live.GetProperty("fixture").GetProperty("setup");var home=setup.GetProperty("home");Check(setup.GetProperty("engineVersion").GetString()==MatchSimulation.Version&&home.GetProperty("players")[0].GetProperty("skills").TryGetProperty("Visão",out _),"Transmissão usa motor atual com atributos de percepção persistidos");
        var liveSave = new { matchId = id, name = "Plano salvo no banco", formationId = "3241", mentality = "Defensiva", plan };
        Check((await other.PostAsJsonAsync(path + "/live/tactical-presets", liveSave)).StatusCode == HttpStatusCode.NotFound,
            "Outra conta não salva plano no banco");
        var livePreset = await Json(await owner.PostAsJsonAsync(path + "/live/tactical-presets", liveSave));
        catalog = await Json(await owner.GetAsync(path + "/sports/tactical-catalog"));
        Check(catalog.GetProperty("plans").EnumerateArray().Any(p => p.GetProperty("id").GetString() == livePreset.GetProperty("id").GetString()),
            "Salvar no banco atualiza os planos da carreira durante a transmissão");
        var changed=await Json(await owner.PostAsJsonAsync(path+"/live/control",new{id,type="tactics",formation="4-3-3",mentality="Defensiva",plan=plan with{Pressure="Baixa",Style="Contra-ataque"}}));Check(changed.GetProperty("fixture").GetProperty("setup").GetProperty("instructions")[0].GetProperty("plan").GetProperty("pressure").GetString()=="Baixa","Comando ao vivo guarda instruções para reconstruir o mesmo replay");
        Check((await other.PostAsJsonAsync(path+"/live/control",new{id,type="preset",presetId})).StatusCode==HttpStatusCode.NotFound,"Outra conta não aplica plano na partida");
        Check((await owner.PostAsJsonAsync(path+"/live/control",new{id,type="preset",presetId="inexistente"})).StatusCode==HttpStatusCode.BadRequest,"Plano inexistente não altera a partida");
        changed=await Json(await owner.PostAsJsonAsync(path+"/live/control",new{id,type="preset",presetId}));var commands=changed.GetProperty("fixture").GetProperty("setup").GetProperty("instructions");Check(commands.GetArrayLength()==2&&commands[1].GetProperty("plan").GetProperty("positions").EnumerateObject().Count()==11,"Plano salvo registra posições no comando da partida");
        c=await Json(await owner.GetAsync(path));Check(c.GetProperty("state").GetProperty("tacticalPlan").GetProperty("pressure").GetString()=="Alta","Aplicar plano durante partida não altera o plano permanente");
        var tick=await Json(await owner.PostAsJsonAsync(path+"/live/tick",new{id,playing=true}));Check(tick.GetProperty("replay").GetProperty("events").GetArrayLength()==0,"Entrada em campo continua sem revelar decisões futuras");
    }
}
