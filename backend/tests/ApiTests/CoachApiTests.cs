using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Brasileirao.IntegrationTests;

public static class CoachApiTests
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    static async Task<JsonElement> Json(HttpResponseMessage r){var s=await r.Content.ReadAsStringAsync();if(!r.IsSuccessStatusCode)throw new Exception(r.StatusCode+": "+s);return JsonDocument.Parse(s).RootElement.Clone();}
    public static async Task Run(HttpClient owner,HttpClient other)
    {
        var created=await Json(await owner.PostAsJsonAsync("/api/careers",new{name="Carreira do treinador",clubId="spo"}));var id=created.GetProperty("id").GetGuid();string path=$"/api/careers/{id}";
        async Task<JsonElement> Career()=>await Json(await owner.GetAsync(path));
        async Task<JsonElement> Coach()=>await Json(await owner.GetAsync(path+"/coach"));
        var c=await Career();int revision=c.GetProperty("revision").GetInt32();var coach=await Coach();string contract=coach.GetProperty("contract").GetProperty("id").GetString()!;
        Check(coach.GetProperty("employed").GetBoolean()&&coach.GetProperty("player").GetProperty("name").GetString()=="Administrador de teste","Perfil de treinador usa a identidade da conta e contrato persistido");
        Check((await other.GetAsync(path+"/coach")).StatusCode==HttpStatusCode.NotFound,"Carreira de treinador pertence apenas ao dono da conta");
        Check((await other.PostAsJsonAsync(path+"/coach/actions",new{revision,type="resign",id=contract})).StatusCode==HttpStatusCode.NotFound,"Outra conta não pode rescindir contrato");
        Check((await owner.PostAsJsonAsync(path+"/coach/actions",new{revision,type="accept",id="fake",clubId="fla",confidence=100,reputation=100})).StatusCode==HttpStatusCode.BadRequest,"Cliente não pode forjar proposta, confiança ou reputação");
        Check((await owner.PostAsJsonAsync(path+"/coach/actions",new{revision,type="advance",days=7})).StatusCode==HttpStatusCode.BadRequest,"Empregado não usa calendário livre para pular a transmissão");
        var result=await Json(await owner.PostAsJsonAsync(path+"/coach/actions",new{revision,type="resign",id=contract}));int next=result.GetProperty("revision").GetInt32();
        Check((await owner.PostAsJsonAsync(path+"/coach/actions",new{revision,type="resign",id=contract})).StatusCode==HttpStatusCode.Conflict,"Rescisão repetida com revisão antiga é bloqueada");
        coach=await Coach();Check(!coach.GetProperty("employed").GetBoolean()&&coach.GetProperty("dismissals").GetArrayLength()==1,"Rescisão coloca treinador livre e cria comunicado com histórico");
        Check((await owner.PostAsJsonAsync(path+"/actions",new{revision=next,type="tactics",value="4-4-2",extra="Ofensiva"})).StatusCode==HttpStatusCode.BadRequest,"Treinador livre não comanda táticas do ex-clube");
        Check((await owner.PostAsJsonAsync(path+"/life/actions",new{revision=next,type="job",value="pal"})).StatusCode==HttpStatusCode.BadRequest,"Endpoint legado não permite escolher novo clube após saída");
        string notice=coach.GetProperty("dismissals")[0].GetProperty("id").GetString()!;result=await Json(await owner.PostAsJsonAsync(path+"/coach/actions",new{revision=next,type="acknowledge",id=notice}));next=result.GetProperty("revision").GetInt32();
        string oldDate=(await Coach()).GetProperty("date").GetString()!;result=await Json(await owner.PostAsJsonAsync(path+"/coach/actions",new{revision=next,type="advance",days=7}));next=result.GetProperty("revision").GetInt32();c=await Career();coach=await Coach();Check(coach.GetProperty("date").GetString()!=oldDate&&c.GetProperty("state").GetProperty("round").GetInt32()>1,"API avança o mundo sem clube e preserva o modo livre");
        string offerId="api-valid-offer";
        using(var scope=BroadcastApiTests.Factory!.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<GameDb>();var career=await db.Careers.SingleAsync(x=>x.Id==id);var game=Game.Read(career);var w=game.Life!.Coaches!;string target="juv";var npc=CoachCareer.At(game,target);if(npc!=null){var nc=CoachCareer.Active(game,npc.Id)!;nc.Status="Fim de contrato";nc.Closed=w.Date;npc.ClubId="";}var b=w.Boards[target];b.VacancyVersion++;var vacancy=new CoachVacancy{Id="api-vacancy",ClubId=target,Version=b.VacancyVersion,Opened=w.Date,Deadline=w.Date.AddDays(14)};w.Vacancies.Add(vacancy);w.Offers.Add(new(){Id=offerId,ClubId=target,VacancyId=vacancy.Id,VacancyVersion=vacancy.Version,Status="Proposta oficial",Wage=30_000,Years=2,TargetPosition=16,TransferBudget=1_000_000,Created=w.Date,Expires=w.Date.AddDays(14)});Game.Write(career,game);await db.SaveChangesAsync();next=career.Revision;
        }
        result=await Json(await owner.PostAsJsonAsync(path+"/coach/actions",new{revision=next,type="accept",id=offerId,wage=1,years=50,target=20}));c=await Career();coach=await Coach();Check(c.GetProperty("state").GetProperty("clubId").GetString()=="juv"&&coach.GetProperty("contract").GetProperty("wage").GetInt64()==30_000,"Assinatura usa termos persistidos; valores extras do navegador são ignorados");
        Check(coach.GetProperty("contracts").EnumerateArray().Count(x=>x.GetProperty("status").GetString()=="Ativo")==1,"Uma única contratação ativa após troca de clube");
        Check((await owner.PostAsJsonAsync(path+"/coach/actions",new{revision=next,type="accept",id=offerId})).StatusCode==HttpStatusCode.Conflict,"Revisão protege a vaga e impede assinatura duplicada");
        next=c.GetProperty("revision").GetInt32();Check((await owner.PostAsJsonAsync(path+"/actions",new{revision=next,type="tactics",value="4-4-2",extra="Ofensiva"})).IsSuccessStatusCode,"Permissões de gestão retornam após contratação válida");
    }
}
