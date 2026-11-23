namespace Brasileirao;

public static partial class CoachCareer
{
    static bool Open(CoachOffer o)=>o.Status is "Interesse informal" or "Sondagem" or "Entrevista" or "Proposta oficial" or "Contraproposta";
    static bool Formal(CoachOffer o)=>o.Status is "Proposta oficial" or "Contraproposta";
    static int RequiredReputation(Club c)=>Math.Clamp((c.Strength-65)*3,15,80);
    static double Fit(GameState g,CoachPerson p,string club)
    {
        var w=World(g);var c=Clubs(g).First(c=>c.Id==club);var b=w.Boards[club];
        if(p.Id==w.PlayerId&&!g.Clubs.Concat(g.Divisions?.OtherClubs??[]).Any(c=>c.Id==club))return -1000;
        if(p.Retired||p.ClubId==club||p.Reputation+w.Rules.ReputationTolerance<RequiredReputation(c))return -1000;
        var lastContract=w.Contracts.Where(x=>x.CoachId==p.Id&&x.ClubId==club&&x.Closed.HasValue).OrderByDescending(x=>x.Closed).FirstOrDefault();
        if(lastContract?.Closed is {} left&&w.Date.DayNumber-left.DayNumber<90)return -1000;
        var employed=Active(g,p.Id);var currentClub=Clubs(g).FirstOrDefault(c=>c.Id==p.ClubId);
        if(p.Id!=w.PlayerId&&currentClub!=null&&currentClub.Strength>c.Strength+3&&w.Boards[p.ClubId].Confidence>=35)return -1000;
        if(MaxWage(g,club)<25_000+p.Reputation*400)return -1000;
        var last=p.Results.TakeLast(10).ToList();double performance=last.Count==0?0:(last.Sum(r=>r.Points)/(last.Count*3.0)-.45)*14;
        int experience=p.Results.Count;int age=w.Date.Year-p.BirthDate.Year;int firings=w.Contracts.Count(c=>c.CoachId==p.Id&&c.Status=="Demissão");
        return p.Reputation-Math.Max(0,p.Reputation-RequiredReputation(c)-10)*.85+Math.Min(8,experience/20.0)+Math.Min(10,p.Titles.Count*2)+performance-firings*2+(p.Style==b.PreferredStyle?5:0)+p.YouthAffinity*b.AcademyValue/2000.0+(age>=45?b.ExperiencePreference:b.YouthPreference)/25.0+(p.ClubId==""?4:0);
    }
    static void ExpireIncompatible(GameState g)
    {
        var w=World(g);
        foreach(var o in w.Offers.Where(Open)){
            bool renewal=o.Kind is "Renovação" or "Permanência";
            if(o.Expires<w.Date){o.Status="Expirada";o.Message="O prazo de resposta terminou.";Emit(g,"Proposta",Player(g),o.ClubId,"Proposta expirada",ClubName(g,o.ClubId)+": prazo de resposta encerrado.");}
            else if(renewal&&Player(g).ClubId!=o.ClubId||!renewal&&(!w.Vacancies.Any(v=>v.Id==o.VacancyId&&v.Status=="Aberta"&&v.Version==o.VacancyVersion)||At(g,o.ClubId)!=null)) {o.Status="Encerrada";o.Message="A vaga já foi preenchida ou o vínculo deixou de existir.";}
        }
    }
    static CoachOffer CreateOffer(GameState g,string club,string vacancy,string status,string kind="Contratação")
    {
        var w=World(g);var p=Player(g);var b=w.Boards[club];var current=Active(g,p.Id);long buyout=current!=null&&current.ClubId!=club?current.Wage*current.SeveranceMonths*4:0;
        var offer=new CoachOffer{Id=NextId(g,"offer"),ClubId=club,VacancyId=vacancy,VacancyVersion=b.VacancyVersion,Kind=kind,Status=status,Created=w.Date,Expires=w.Date.AddDays(Math.Clamp(w.Rules.OfferValidityDays,3,30)),Wage=Math.Min(MaxWage(g,club),(long)(25_000+p.Reputation*900)),TargetPosition=b.TargetPosition,TransferBudget=Math.Max(0,(Cash(g,club)-buyout)*2/3),Buyout=buyout,Message=status=="Entrevista"?"Convite para discutir seu projeto esportivo.":"A diretoria acompanha o desempenho do treinador."};
        w.Offers.Add(offer);Emit(g,"Proposta",p,club,ClubName(g,club)+": "+status,kind+". Prazo: "+offer.Expires.ToString("dd/MM/yyyy")+".");return offer;
    }
    public static void Tick(GameState g,DateOnly date)
    {
        var w=World(g);int end=date.DayNumber;
        for(int day=w.LastTickDay+1;day<=end;day++){
            w.Date=DateOnly.FromDayNumber(day);w.LastTickDay=day;ExpireIncompatible(g);
            foreach(var p in w.People.Where(p=>!p.Retired).ToList()){
                int age=w.Date.Year-p.BirthDate.Year;
                if(p.Id!=w.PlayerId&&age>=w.Rules.AiRetirementAge&&Draw(g,$"retire:{p.Id}:{day}",365)<4){if(p.ClubId!="")EndContract(g,p,"Aposentadoria","O treinador encerrou sua carreira profissional.");p.Retired=true;continue;}
                var contract=Active(g,p.Id);if(contract==null)continue;
                if(contract.End<w.Date){
                    if(p.Id==w.PlayerId){EndContract(g,p,"Fim de contrato","O vínculo terminou sem renovação assinada.");}
                    else if(w.Boards[contract.ClubId].Confidence>=50&&Cash(g,contract.ClubId)>0){contract.Status="Renovado";contract.Closed=w.Date;contract.EndReason="Renovação pela diretoria";InitialContract(g,p,p.ClubId,w.Boards[p.ClubId].TargetPosition);var next=Active(g,p.Id)!;next.PreviousContract=contract.Id;next.End=w.Date.AddYears(2);Emit(g,"Renovação",p,p.ClubId,p.Name+" renova com "+ClubName(g,p.ClubId),"Novo prazo de dois anos registrado.");}
                    else EndContract(g,p,"Fim de contrato","A diretoria decidiu buscar outro projeto.");
                }else if(p.Id!=w.PlayerId&&w.Boards[p.ClubId].Confidence<30&&Draw(g,$"resign:{p.Id}:{day}",600)==0)EndContract(g,p,"Pedido de demissão","O treinador pediu saída em meio à crise.");
            }
            foreach(var application in w.Applications.Where(a=>a.Status=="Em análise"&&a.AnswerDate<=w.Date).ToList()){
                var vacancy=w.Vacancies.Find(v=>v.Id==application.VacancyId);if(vacancy==null||vacancy.Status!="Aberta"){application.Status="Encerrada";application.Reason="Vaga preenchida.";continue;}
                double score=Fit(g,Player(g),vacancy.ClubId);double rival=w.People.Where(p=>p.Id!=w.PlayerId).Select(p=>Fit(g,p,vacancy.ClubId)).DefaultIfEmpty(0).Max();
                bool selected=score>=rival-14&&score>-500&&Draw(g,"application:"+application.Id)<Math.Clamp(w.Rules.ApplicationChance+(score-rival)*3,10,90);
                application.Status=selected?"Entrevista":"Recusada";application.Reason=selected?"Você entrou na seleção final da diretoria.":"A diretoria priorizou candidatos com maior compatibilidade e experiência.";
                if(selected)CreateOffer(g,vacancy.ClubId,vacancy.Id,"Entrevista");else Emit(g,"Candidatura",Player(g),vacancy.ClubId,"Candidatura não selecionada",application.Reason);
            }
            foreach(var o in w.Offers.Where(o=>o.Status is "Interesse informal" or "Sondagem").ToList()){
                int age=w.Date.DayNumber-o.Created.DayNumber;
                if(age>=4&&o.Status=="Sondagem"){o.Status="Entrevista";o.Message="A diretoria convida você para apresentar seu projeto.";Emit(g,"Entrevista",Player(g),o.ClubId,"Convite para entrevista",ClubName(g,o.ClubId)+" quer discutir uma contratação.");}
                else if(age>=2&&o.Status=="Interesse informal"){o.Status="Sondagem";o.Message="A diretoria iniciou uma sondagem formal.";}
            }
            foreach(var vacancy in w.Vacancies.Where(v=>v.Status=="Aberta").ToList()){
                if(At(g,vacancy.ClubId)!=null){vacancy.Status="Preenchida";continue;}
                var candidates=w.People.Where(p=>p.Id!=w.PlayerId&&Fit(g,p,vacancy.ClubId)>-500).OrderByDescending(p=>Fit(g,p,vacancy.ClubId)+(Draw(g,"candidate:"+vacancy.Id+":"+p.Id,11)-5)).Take(3).ToList();vacancy.Candidates=candidates.Select(p=>p.Id).ToList();
                bool protectedOffer=w.Offers.Any(o=>o.VacancyId==vacancy.Id&&Open(o)&&o.Expires>=w.Date);
                if(!protectedOffer&&!w.Offers.Any(o=>o.VacancyId==vacancy.Id)&&Fit(g,Player(g),vacancy.ClubId)>-500&&Fit(g,Player(g),vacancy.ClubId)>=(candidates.FirstOrDefault() is {} top?Fit(g,top,vacancy.ClubId):0)-10&&Draw(g,"interest:"+vacancy.Id)<w.Rules.InformalInterestChance&&Cash(g,vacancy.ClubId)>0)CreateOffer(g,vacancy.ClubId,vacancy.Id,"Interesse informal");
                if(w.Date<vacancy.Deadline||w.Offers.Any(o=>o.VacancyId==vacancy.Id&&Open(o)&&o.Expires>=w.Date))continue;
                var chosen=candidates.FirstOrDefault(p=>p.ClubId==""||Cash(g,vacancy.ClubId)>(Active(g,p.Id)?.Wage??0)*12+MaxWage(g,vacancy.ClubId)*8);
                if(chosen==null){chosen=Generate(g,"",w.Sequence);w.People.Add(chosen);}
                if(chosen.ClubId!=""){var old=Active(g,chosen.Id)!;long fee=old.Wage*old.SeveranceMonths*4;Money(g,vacancy.ClubId,"Multa de contratação do treinador",-fee);Money(g,chosen.ClubId,"Multa recebida pela saída do treinador",fee);EndContract(g,chosen,"Transferência","Proposta aceita de "+ClubName(g,vacancy.ClubId)+".");}
                chosen.ClubId=vacancy.ClubId;InitialContract(g,chosen,vacancy.ClubId,w.Boards[vacancy.ClubId].TargetPosition);w.Boards[vacancy.ClubId].Confidence=65;vacancy.Status="Preenchida";Emit(g,"Contratação",chosen,vacancy.ClubId,ClubName(g,vacancy.ClubId)+" contrata "+chosen.Name,"A diretoria concluiu a seleção entre candidatos disponíveis.");
            }
            var player=Player(g);var active=Active(g,player.Id);
            if(active!=null&&(active.End.DayNumber-w.Date.DayNumber)<=120&&!w.Offers.Any(o=>o.ClubId==player.ClubId&&o.Kind=="Renovação"&&o.Created.Year==w.Date.Year)&&w.Boards[player.ClubId].Confidence>=55)CreateOffer(g,player.ClubId,"","Proposta oficial","Renovação");
        }
        w.Date=Max(w.Date,date);ExpireIncompatible(g);
    }
    public static object Act(GameState g,CoachAction request)
    {
        Upgrade(g);var w=World(g);var p=Player(g);ExpireIncompatible(g);
        switch(request.Type){
            case "funding": {ClubLife.EnsureEmployed(g);ClubLife.Act(g,"funding",0,"",0);break;}
            case "profile": {if(p.Results.Count>0)throw new GameRuleException("O perfil só pode ser definido antes da primeira partida registrada.");string name=request.Value.Trim();if(name.Length is <2 or >60)throw new GameRuleException("Informe um nome de 2 a 60 caracteres.");p.Name=name;break;}
            case "acknowledge": {var d=w.Dismissals.Find(d=>d.Id==request.Id)??throw new GameRuleException("Comunicado indisponível.");if(d.Acknowledged)throw new GameRuleException("Comunicado já confirmado.");d.Acknowledged=true;break;}
            case "advance": AdvanceFree(g,request.Days);break;
            case "resign": {if(!IsEmployed(g))throw new GameRuleException("Você já está livre no mercado.");var current=Active(g,p.Id)!;if(request.Id!=current.Id)throw new GameRuleException("Confira o contrato antes de pedir demissão.");EndContract(g,p,"Pedido de demissão","O treinador decidiu encerrar seu vínculo. Não há indenização a receber.");break;}
            case "apply": {var vacancy=w.Vacancies.Find(v=>v.Id==request.Id&&v.Status=="Aberta"&&v.Deadline>=w.Date)??throw new GameRuleException("Vaga encerrada.");if(!g.Clubs.Concat(g.Divisions?.OtherClubs??[]).Any(c=>c.Id==vacancy.ClubId))throw new GameRuleException("Este mercado não tem uma liga jogável disponível.");if(vacancy.ClubId==p.ClubId)throw new GameRuleException("Você já trabalha neste clube.");if(w.Applications.Any(a=>a.VacancyId==vacancy.Id))throw new GameRuleException("Candidatura já enviada para esta vaga.");w.Applications.Add(new(){Id=NextId(g,"application"),VacancyId=vacancy.Id,Applied=w.Date,AnswerDate=w.Date.AddDays(3)});Emit(g,"Candidatura",p,vacancy.ClubId,"Candidatura enviada",p.Name+" entrou na seleção de "+ClubName(g,vacancy.ClubId)+". Resposta prevista em três dias.");break;}
            case "decline": {var o=Offer(g,request.Id);o.Status="Recusada";o.Message="O treinador recusou os termos e segue sua carreira.";Emit(g,"Proposta",p,o.ClubId,p.Name+" recusa proposta de "+ClubName(g,o.ClubId),p.ClubId==""?"O treinador segue livre para outras oportunidades.":"O treinador reafirmou seu compromisso com o clube atual.");break;}
            case "interview": {var o=Offer(g,request.Id);if(o.Status!="Entrevista")throw new GameRuleException("Aguarde um convite para entrevista.");if(request.Value is not ("Base" or "Resultados" or "Equilíbrio"))throw new GameRuleException("Projeto inválido.");var b=w.Boards[o.ClubId];int affinity=request.Value=="Base"?b.AcademyValue:request.Value=="Resultados"?b.TitleDemand:60;double fit=Fit(g,p,o.ClubId);if(fit<0||Draw(g,"interview:"+o.Id)>Math.Clamp(55+affinity/3,55,90)){o.Status="Recusada pela diretoria";o.Message="O projeto apresentado não foi escolhido na seleção final.";Emit(g,"Entrevista",p,o.ClubId,"Entrevista encerrada sem acordo",o.Message);}else{o.Status="Proposta oficial";o.Message="Projeto aprovado. Revise salário, prazo, metas, orçamento e rescisão antes de aceitar.";o.NegotiationHistory.Add("Projeto apresentado: "+request.Value);Emit(g,"Proposta",p,o.ClubId,"Proposta oficial de "+ClubName(g,o.ClubId),o.Message);var current=Active(g,p.Id);if(current!=null&&current.ClubId!=o.ClubId&&w.Boards[current.ClubId].Confidence>=70&&!w.Offers.Any(x=>x.Kind=="Permanência"&&Open(x))){var retention=CreateOffer(g,current.ClubId,"","Proposta oficial","Permanência");retention.Wage=Math.Min(MaxWage(g,current.ClubId),(long)(current.Wage*1.15));}}break;}
            case "negotiate": Negotiate(g,request);break;
            case "accept": Accept(g,request.Id);break;
            default:throw new GameRuleException("Ação de treinador desconhecida.");
        }
        return new{message="Decisão registrada na carreira do treinador."};
    }
    static CoachOffer Offer(GameState g,string id)=>World(g).Offers.Find(o=>o.Id==id&&Open(o)&&o.Expires>=World(g).Date)??throw new GameRuleException("Proposta indisponível ou expirada.");
    static void Negotiate(GameState g,CoachAction r)
    {
        var o=Offer(g,r.Id);if(!Formal(o))throw new GameRuleException("Aguarde uma proposta oficial.");if(o.NegotiationRound>=3)throw new GameRuleException("Limite de três rodadas de negociação atingido. Aceite ou recuse os termos atuais.");
        if(r.Wage is <5000 or >2_000_000||r.Years is <1 or >5||r.Target is <1 or >20||r.Budget<0||r.SeveranceMonths is <0 or >12)throw new GameRuleException("Termos contratuais inválidos.");
        o.NegotiationRound++;o.NegotiationHistory.Add($"Pedido {o.NegotiationRound}: R$ {r.Wage}/semana, {r.Years} anos, meta {r.Target}º, orçamento {r.Budget}, rescisão {r.SeveranceMonths} meses.");
        long max=MaxWage(g,o.ClubId);long budget=Math.Max(0,(Cash(g,o.ClubId)-o.Buyout)*3/4);int target=World(g).Boards[o.ClubId].TargetPosition;bool excessive=r.Wage>max*2||r.Budget>budget*2||r.Target>target+6;
        if(excessive){o.Status="Recusada pela diretoria";o.Message="Exigências incompatíveis com as possibilidades financeiras e esportivas. Negociação encerrada.";}
        else{bool accepted=r.Wage<=max&&r.Budget<=budget&&r.Target<=target+2&&r.SeveranceMonths<=6&&r.Years<=(Player(g).Reputation>=60?5:3);o.Status=accepted?"Proposta oficial":"Contraproposta";o.Wage=Math.Min(r.Wage,max);o.Years=Math.Min(r.Years,Player(g).Reputation>=60?5:3);o.TargetPosition=Math.Min(r.Target,target+2);o.TransferBudget=Math.Min(r.Budget,budget);o.SeveranceMonths=Math.Min(r.SeveranceMonths,6);o.Message=accepted?"A diretoria aceitou os termos negociados.":"A diretoria ajustou os termos ao orçamento e ao projeto. Confira a contraproposta.";}
        o.NegotiationHistory.Add(o.Message);Emit(g,"Negociação",Player(g),o.ClubId,"Negociação com "+ClubName(g,o.ClubId),o.Message);
    }
    static void Accept(GameState g,string id)
    {
        var w=World(g);var p=Player(g);var o=Offer(g,id);if(!Formal(o))throw new GameRuleException("Entrevista e proposta oficial são obrigatórias antes da contratação.");
        bool renewal=o.Kind is "Renovação" or "Permanência";var old=Active(g,p.Id);
        if(!renewal&&(At(g,o.ClubId)!=null||!w.Vacancies.Any(v=>v.Id==o.VacancyId&&v.Status=="Aberta"&&v.Version==o.VacancyVersion)))throw new GameRuleException("A vaga já foi preenchida.");
        long buyout=old!=null&&old.ClubId!=o.ClubId?old.Wage*old.SeveranceMonths*4:0;
        if(o.Buyout!=buyout)throw new GameRuleException("O vínculo anterior mudou. É necessária uma nova proposta.");
        if(o.Wage>MaxWage(g,o.ClubId)||Cash(g,o.ClubId)<buyout+o.Wage*8||o.TransferBudget>Math.Max(0,(Cash(g,o.ClubId)-buyout)*3/4))throw new GameRuleException("O clube já não dispõe dos recursos da proposta. Negocie novos termos.");
        if(old!=null){if(old.ClubId!=o.ClubId){Money(g,o.ClubId,"Multa de contratação do treinador",-buyout);Money(g,old.ClubId,"Multa recebida pela saída do treinador",buyout);EndContract(g,p,"Transferência","Proposta de "+ClubName(g,o.ClubId)+" aceita com multa contratual paga pelo novo clube.");}else{old.Status="Renovado";old.Closed=w.Date;old.EndReason=o.Kind;}}
        SwitchClub(g,o.ClubId);p.ClubId=o.ClubId;g.Formation=p.Formation;g.Mentality=p.Style;var b=w.Boards[o.ClubId];b.Confidence=65;b.Ultimatum=null;b.TargetPosition=o.TargetPosition;
        var contract=new CoachContract{Id=NextId(g,"contract"),PreviousContract=old?.Id??"",CoachId=p.Id,ClubId=p.ClubId,Start=w.Date,End=w.Date.AddYears(o.Years),Wage=o.Wage,SeveranceMonths=o.SeveranceMonths,TargetPosition=o.TargetPosition,TransferBudget=o.TransferBudget};w.Contracts.Add(contract);o.Status="Aceita";g.Life!.Board.Confidence=65;g.Life.Board.TargetPosition=o.TargetPosition;g.Life.Board.TransferBudget=o.TransferBudget;g.Life.Board.WageBudget=g.Squad.Sum(p=>(long)p.Wage)*13/10+100_000;
        if(old?.ClubId!=p.ClubId){g.Life.Fans.Support=Math.Clamp(45+(int)p.Reputation/3,30,85);foreach(var player in g.Squad)g.Life.Minds[player.Id].Relationship=Math.Clamp(g.Life.Minds[player.Id].Relationship+((int)p.Reputation-50)/10,0,100);}
        foreach(var v in w.Vacancies.Where(v=>v.ClubId==p.ClubId&&v.Status=="Aberta"))v.Status="Preenchida";
        foreach(var other in w.Offers.Where(x=>Open(x)&&x.Id!=o.Id)){other.Status="Encerrada";other.Message="O treinador assinou outro contrato.";}
        foreach(var d in w.Dismissals.Where(d=>!d.Acknowledged))d.Acknowledged=true;
        Emit(g,o.Kind,p,p.ClubId,p.Name+" assina com "+ClubName(g,p.ClubId),$"Contrato até {contract.End:dd/MM/yyyy}, salário semanal de R$ {contract.Wage:N0}. Meta: {contract.TargetPosition}º lugar.");AssertIntegrity(g);
    }
    public static void AssertIntegrity(GameState g)
    {
        var w=World(g);var active=w.Contracts.Where(c=>c.Status=="Ativo").ToList();if(active.GroupBy(c=>c.CoachId).Any(x=>x.Count()>1)||active.GroupBy(c=>c.ClubId).Any(x=>x.Count()>1))throw new InvalidOperationException("Contratos de treinador incompatíveis.");foreach(var p in w.People)if((p.ClubId!="")!=(active.Count(c=>c.CoachId==p.Id&&c.ClubId==p.ClubId)==1))throw new InvalidOperationException("Vínculo de treinador inconsistente.");
    }
    public static object View(GameState g)
    {
        Upgrade(g);var w=World(g);var p=Player(g);var current=Active(g,p.Id);return new{
            date=w.Date,player=p,reputationLabel=ReputationLabel(p.Reputation),employed=p.ClubId!="",contract=current,
            board=p.ClubId!=""?w.Boards[p.ClubId]:null,confidenceLabel=p.ClubId!=""?ConfidenceLabel(w,w.Boards[p.ClubId].Confidence):"Treinador livre",rules=w.Rules,
            financeBudget=new {g.Life!.Board.WageBudget,g.Life.Board.TransferBudget},
            dismissals=w.Dismissals,contracts=w.Contracts.Where(c=>c.CoachId==p.Id),applications=w.Applications,offers=w.Offers.OrderByDescending(o=>o.Created),
            vacancies=w.Vacancies.Where(v=>v.Status=="Aberta"&&g.Clubs.Concat(g.Divisions?.OtherClubs??[]).Any(c=>c.Id==v.ClubId)).Select(v=>new{v.Id,v.ClubId,v.Opened,v.Deadline,v.Reason,candidateCount=v.Candidates.Count,compatible=Fit(g,p,v.ClubId)>-500,position=Position(g,v.ClubId),board=w.Boards[v.ClubId],budget=Math.Max(0,Cash(g,v.ClubId)*3/4),maxWage=MaxWage(g,v.ClubId)}),
            clubs=Clubs(g).Select(c=>new{c.Id,c.Name,c.Short,c.Color,c.Strength,position=Position(g,c.Id),coach=At(g,c.Id) is {} ai?new{ai.Id,ai.Name,ai.Reputation,ai.Formation,ai.Style,confidence=w.Boards[c.Id].Confidence}:null}),
            timeline=w.Timeline.Where(t=>t.CoachId==p.Id).Reverse(),marketNews=w.Timeline.TakeLast(100).Reverse(),aiCoaches=w.People.Where(x=>x.Id!=p.Id).Select(x=>new{x.Id,x.Name,x.ClubId,x.Reputation,x.Retired,x.Formation,x.Style,titles=x.Titles.Count,age=w.Date.Year-x.BirthDate.Year}),
            notice="Técnicos e personalidades das diretorias são gerados e evoluem nesta simulação; não representam vínculos profissionais oficiais. Sem ligas estrangeiras ou seleções jogáveis, não há ofertas desses mercados."
        };
    }
}
