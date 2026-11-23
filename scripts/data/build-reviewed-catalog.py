"""Build an importable catalog only from explicitly reviewed, complete squads."""
import json,pathlib,datetime,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
def build(clubs,rows,as_of):
 date=datetime.date.fromisoformat(as_of);errors=[];ids=set();players=[];counts={c['id']:[] for c in clubs}
 if len(counts)!=20:errors.append('Exactly 20 distinct clubs are required.')
 for row in rows:
  entity=row.get('entity','');match=re.fullmatch(r'https://www.wikidata.org/entity/Q([1-9][0-9]*)',entity)
  if not match or int(match[1])>2147483647:errors.append('Invalid Wikidata player identity.');continue
  id=int(match[1]);club=row.get('clubId');position=row.get('position');name=row.get('name','')
  if id in ids:errors.append('Duplicate player identity.');continue
  ids.add(id)
  if club not in counts or position not in ['GOL','ZAG','LAT','MEI','ATA'] or not isinstance(name,str) or not name.strip() or len(name)>80:errors.append('Invalid club, position or name.');continue
  if row.get('membershipVerified') is not True or row.get('verifiedOn')!=as_of or not re.match(r'^https://[^/]+/.+',row.get('membershipSource','')):errors.append(f'{name}: current membership has not been reviewed.');continue
  try:birth=datetime.date.fromisoformat(row['birthDate']);age=date.year-birth.year-((date.month,date.day)<(birth.month,birth.day))
  except (ValueError,KeyError):errors.append(f'{name}: invalid birth date.');continue
  if not 15<=age<=50:errors.append(f'{name}: age outside game limits.');continue
  counts[club].append(position);players.append({'clubId':club,'player':{'id':id,'name':name,'position':position,'age':age,'rating':65,'value':1000000,'wage':30000}})
 for id,positions in counts.items():
  if not 18<=len(positions)<=40 or positions.count('GOL')<2 or len(positions)-positions.count('GOL')<16:errors.append(f'{id}: needs 18–40 verified players, 2 goalkeepers and 16 outfield players.')
 if errors:raise ValueError('\n'.join(errors))
 return {'version':'real-2026-reviewed-'+as_of,'season':2026,'source':'Elencos conferidos em '+as_of+'; fontes por atleta no manifesto. Habilidade, valores, salários e força dos clubes são parâmetros de jogo estimados.','license':'Dados estruturados Wikidata CC0-1.0; parâmetros estimados pelo projeto. Escudos não incluídos.','clubs':[{k:c[k] for k in ['id','name','short','color','strength']} for c in clubs],'players':players,'provenance':rows}
if __name__=='__main__':
 source=ROOT/'data/real-2026/approved-players.json';data=json.loads((ROOT/'data/real-2026/clubs.json').read_text(encoding='utf8'))
 try:catalog=build(data['clubs'],json.loads(source.read_text(encoding='utf8')),data['asOf'])
 except ValueError as error:print('Catalog not created: incomplete/unreviewed squads.\n'+str(error),file=sys.stderr);sys.exit(2)
 target=ROOT/'data/real-2026/catalog-reviewed.json';target.write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf8');print(target)
