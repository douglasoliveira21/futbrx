using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Brasileirao.IntegrationTests;

public static class ClubLifeApiTests
{
    static async Task<JsonElement> Json(HttpResponseMessage r){var body=await r.Content.ReadAsStringAsync();if(!r.IsSuccessStatusCode)throw new Exception(r.StatusCode+": "+body);return JsonDocument.Parse(body).RootElement.Clone();}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    public static async Task Run(HttpClient owner,HttpClient other)
    {
        var created=await Json(await owner.PostAsJsonAsync("/api/careers",new{name="Sistemas do clube",clubId="pal"}));var id=created.GetProperty("id").GetString();var path=$"/api/careers/{id}";
        async Task<JsonElement> Read()=>await Json(await owner.GetAsync(path));
        async Task<JsonElement> Life()=>await Json(await owner.GetAsync(path+"/life"));
        var view=await Life();Check(!view.GetRawText().Contains("hiddenPotential",StringComparison.OrdinalIgnoreCase),"API dos bastidores protege o potencial exato");
        Check((await other.GetAsync(path+"/life")).StatusCode==HttpStatusCode.NotFound,"Outra conta não acessa os bastidores");
        var career=await Read();int revision=career.GetProperty("revision").GetInt32(),pid=career.GetProperty("state").GetProperty("squad")[2].GetProperty("id").GetInt32();
        Check((await other.PostAsJsonAsync(path+"/life/actions",new{revision,type="loan",amount=5_000_000})).StatusCode==HttpStatusCode.NotFound,"Outra conta não contrata dívida na carreira");
        var talk=await Json(await owner.PostAsJsonAsync(path+"/life/actions",new{revision,type="talk",playerId=pid,value="Apoiar"}));
        Check(talk.GetProperty("revision").GetInt32()>revision,"Decisão de vestiário incrementa revisão do save");
        Check((await owner.PostAsJsonAsync(path+"/life/actions",new{revision,type="loan",amount=5_000_000})).StatusCode==HttpStatusCode.Conflict,"Revisão antiga não duplica decisão financeira");
        career=await Read();revision=career.GetProperty("revision").GetInt32();long balance=career.GetProperty("state").GetProperty("balance").GetInt64();
        Check((await owner.PostAsJsonAsync(path+"/life/actions",new{revision,type="loan",amount=-1})).StatusCode==HttpStatusCode.BadRequest,"Crédito inválido enviado pelo cliente é rejeitado");
        var credit=await Json(await owner.PostAsJsonAsync(path+"/life/actions",new{revision,type="loan",amount=5_000_000,balance=999999999L,debt=0}));revision=credit.GetProperty("revision").GetInt32();
        view=await Life();career=await Read();Check(view.GetProperty("economy").GetProperty("debt").GetInt64()==5_600_000&&career.GetProperty("state").GetProperty("balance").GetInt64()==balance+5_000_000,"Servidor calcula dívida, encargos e caixa sem confiar nos valores do cliente");
        Check((await owner.PostAsJsonAsync(path+"/life/actions",new{revision,type="treatment",playerId=pid,value="Intensivo"})).StatusCode==HttpStatusCode.BadRequest,"Não cobra tratamento de atleta sem lesão");
        var trial=await Json(await owner.PostAsJsonAsync(path+"/life/actions",new{revision,type="trial",value="Sub-15"}));revision=trial.GetProperty("revision").GetInt32();view=await Life();
        Check(view.GetProperty("trials").GetArrayLength()==4&&view.GetProperty("trials").EnumerateArray().All(t=>t.GetProperty("player").GetProperty("age").GetInt32()<15),"Peneira Sub-15 chega à API com atletas da categoria correta");
        var youth=view.GetProperty("trials")[0].GetProperty("player").GetProperty("id").GetInt32();var recruit=await Json(await owner.PostAsJsonAsync(path+"/life/actions",new{revision,type="recruit",playerId=youth}));revision=recruit.GetProperty("revision").GetInt32();view=await Life();
        Check(view.GetProperty("academy").EnumerateArray().Any(p=>p.GetProperty("player").GetProperty("id").GetInt32()==youth),"Captação incorpora o jovem à base persistida");
        Check((await owner.PostAsJsonAsync(path+"/life/actions",new{revision,type="job",value="fla"})).StatusCode==HttpStatusCode.BadRequest,"Treinador empregado não troca de clube sem encerrar contrato");
        var quote=await Json(await owner.GetAsync(path+"/market/quote/"+pid+"?kind=renewal&years=3"));Check(quote.GetProperty("agentCommission").GetInt64()>0&&quote.GetProperty("agentName").GetString()!.Length>0,"Negociação apresenta empresário e comissão antes de confirmar");
        var done=await Json(await BroadcastApiTests.CompleteRound(owner,id!,revision));Check(done.GetProperty("state").GetProperty("round").GetInt32()==2&&done.GetProperty("state").GetProperty("nextCup").ValueKind==JsonValueKind.Object,"Próxima partida eliminatória aparece na carreira após a liga");
        var cup=await Json(await owner.PostAsJsonAsync(path+"/live/start",new{revision=done.GetProperty("revision").GetInt32()}));Check(cup.GetProperty("competitionMatchId").GetString()!.Length>0&&cup.GetProperty("fixture").GetProperty("leg").GetInt32()==1&&!cup.GetProperty("fixture").GetProperty("setup").GetProperty("knockout").GetBoolean(),"Ida do Estadual começa pelo mesmo sistema de transmissão obrigatória, sem pênaltis");
        career=await Read();Check((await owner.PostAsJsonAsync(path+"/life/actions",new{revision=career.GetProperty("revision").GetInt32(),type="ticket",amount=80})).StatusCode==HttpStatusCode.BadRequest,"Gestão fora de campo permanece bloqueada durante partida");
    }
}
