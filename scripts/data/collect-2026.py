"""One provider request per run. Never auto-publish Wikidata memberships.
Use --offline to rebuild the coverage page from the existing cache.
"""
import json,pathlib,urllib.request,urllib.parse,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
base=json.loads((ROOT/'data/real-2026/clubs.json').read_text(encoding='utf8'))
metadata=json.loads((ROOT/'data/real-2026/wikidata-clubs.json').read_text(encoding='utf8'))
entities={r['id']['value']:r['club']['value'] for r in metadata}
if '--offline' not in sys.argv:
 q=(ROOT/'data/real-2026/rosters.sparql').read_text(encoding='utf8')
 begin=q.index('VALUES (?id ?club) {')+len('VALUES (?id ?club) {');end=q.index('}',begin)
 values=' '.join('("'+id+'" <'+entity+'>)' for id,entity in entities.items());q=q[:begin]+values+q[end:]
 url='https://query.wikidata.org/sparql?'+urllib.parse.urlencode({'query':q,'format':'json'})
 try:
  request=urllib.request.Request(url,headers={'User-Agent':'BrasileiraoManager/0.7 (local open data research)','Accept':'application/sparql-results+json'})
  result=json.load(urllib.request.urlopen(request,timeout=55))
  (ROOT/'data/real-2026/roster-evidence.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
 except Exception as error:
  print('No update; provider unavailable. Respect Retry-After before retrying:',error,file=sys.stderr);sys.exit(2)
evidence=json.loads((ROOT/'data/real-2026/roster-evidence.json').read_text(encoding='utf8'))['results']['bindings']
summary={**base,'license':'Wikidata CC0; participantes conferidos na CBF. Escudos têm licenças individuais.','status':'Elencos em revisão; não é uma base jogável publicada.','providerNotice':'Ausência de fim de vínculo não comprova elenco em 2026. Cada atleta exige conferência em fonte atual.','clubs':[]}
for c in base['clubs']:
 count=len({r['person']['value'] for r in evidence if r['id']['value']==c['id']})
 summary['clubs'].append({**c,'wikidata':entities.get(c['id']),'evidenceCount':count,'rosterStatus':'Pendente de conferência com fonte atual','crestStatus':'Licença individual pendente'})
(ROOT/'frontend/public/data/real-2026.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
print('Evidence rows:',len(evidence),'clubs:',len(summary['clubs']),'published squads: 0')
