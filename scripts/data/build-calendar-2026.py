"""Parse the CC0 Openfootball snapshot; never import real-world scores into saves."""
import datetime,hashlib,json,pathlib,re
ROOT=pathlib.Path(__file__).resolve().parents[2]
NAMES={'CA Mineiro':'cam','SE Palmeiras':'pal','Coritiba FBC':'ctb','RB Bragantino':'rbb','SC Internacional':'int','CA Paranaense':'cap','EC Vitória':'vit','Clube do Remo':'rem','Fluminense FC':'flu','Grêmio FBPA':'gre','Chapecoense AF':'cha','Santos FC':'san','SC Corinthians Paulista':'cor','EC Bahia':'bah','São Paulo FC':'sao','CR Flamengo':'fla','Mirassol FC':'mir','CR Vasco da Gama':'vas','Botafogo FR':'bot','Cruzeiro EC':'cru'}
MONTHS={m:i+1 for i,m in enumerate(['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'])}
def parse(text):
 rows=[];round=0;date=None;time=None
 for line in text.splitlines():
  m=re.match(r'▪ Matchday (\d+)',line)
  if m:round=int(m[1]);continue
  m=re.match(r'\s+(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun) ([A-Z][a-z]{2}) (\d+)',line)
  if m:date=datetime.date(2026,MONTHS[m[1]],int(m[2])).isoformat();time=None;continue
  if ' v ' not in line:continue
  m=re.match(r'\s*(?:(\d\d:\d\d)\s+)?(.+?)\s+v\s+(.+)',line)
  if not m:raise ValueError('Unrecognized fixture')
  if m[1]:time=m[1]
  home=NAMES[m[2].strip()];away=next((v for k,v in NAMES.items() if m[3].startswith(k)),None)
  if away is None or date is None:raise ValueError('Missing club/date')
  rows.append({'round':round,'home':home,'away':away,'date':date,'kickoff':date+'T'+time+':00-03:00' if time else None})
 if len(rows)!=380 or len({(r['home'],r['away']) for r in rows})!=380:raise ValueError('Incomplete or duplicate calendar')
 for round in range(1,39):
  rs=[r for r in rows if r['round']==round]
  if len(rs)!=10 or len({c for r in rs for c in (r['home'],r['away'])})!=20:raise ValueError('Invalid round '+str(round))
 return sorted(rows,key=lambda r:(r['round'],r['home']))
if __name__=='__main__':
 source=ROOT/'data/real-2026/openfootball/2026_br1.txt';raw=source.read_bytes()
 result={'season':2026,'source':'https://github.com/openfootball/south-america/blob/master/brazil/2026_br1.txt','license':'CC0-1.0','retrievedOn':'2026-11-21','sourceSha256':hashlib.sha256(raw).hexdigest(),'notice':'Calendário comunitário baseado na tabela de 2026. Não é feed oficial ao vivo. Datas futuras podem mudar; horários ausentes ficam sem confirmação. Resultados reais não são importados.','fixtures':parse(raw.decode('utf-8'))}
 for target in [ROOT/'data/real-2026/calendar-2026.json',ROOT/'frontend/public/data/calendar-2026.json']:
  target.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('380 fixtures validated; no scores imported.')
