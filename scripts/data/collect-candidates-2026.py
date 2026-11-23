"""One bounded CC0 provider query; candidate data is never an approval or publication."""
import datetime,json,pathlib,sys,urllib.request,urllib.parse,urllib.error
ROOT=pathlib.Path(__file__).resolve().parents[2];DATA=ROOT/'data/real-2026'
if '--offline' not in sys.argv:
 clubs=json.loads((DATA/'wikidata-clubs.json').read_text(encoding='utf-8'))
 values=' '.join('("'+r['id']['value']+'" <'+r['club']['value']+'>)' for r in clubs)
 cutoff=json.loads((DATA/'clubs.json').read_text(encoding='utf-8'))['asOf']
 query='''SELECT ?id ?club ?person ?birth ?position ?start ?end ?statement ?labelPt ?labelEn WHERE {
 VALUES (?id ?club) { VALUES_HERE }
 ?person p:P54 ?statement;wdt:P569 ?birth. ?statement ps:P54 ?club.
 OPTIONAL {?person wdt:P413 ?position} OPTIONAL {?statement pq:P580 ?start} OPTIONAL {?statement pq:P582 ?end}
 FILTER(?birth>="1975-11-22T00:00:00Z"^^xsd:dateTime && ?birth<="2011-11-21T23:59:59Z"^^xsd:dateTime)
 OPTIONAL {?person rdfs:label ?labelPt FILTER(LANG(?labelPt)="pt")}
 OPTIONAL {?person rdfs:label ?labelEn FILTER(LANG(?labelEn)="en")}
 FILTER(!BOUND(?start)||?start<="CUTOFFT23:59:59Z"^^xsd:dateTime)
 FILTER(!BOUND(?end)||?end>="CUTOFFT00:00:00Z"^^xsd:dateTime)
 }'''.replace('VALUES_HERE',values).replace('CUTOFF',cutoff)
 request=urllib.request.Request('https://query.wikidata.org/sparql?'+urllib.parse.urlencode({'query':query,'format':'json'}),headers={'User-Agent':'BrasileiraoManager/0.12 (local CC0 research)','Accept':'application/sparql-results+json'})
 try:
  result=json.load(urllib.request.urlopen(request,timeout=55))
  (DATA/'roster-candidates-2026.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 except urllib.error.HTTPError as e:
  print('No changes. HTTP',e.code,'Retry-After:',e.headers.get('Retry-After','not supplied'),file=sys.stderr);sys.exit(2)
 except Exception as e:print('No changes; provider unavailable:',e,file=sys.stderr);sys.exit(2)
rows=json.loads((DATA/'roster-candidates-2026.json').read_text(encoding='utf-8'))['results']['bindings']
print('Candidate rows:',len(rows),'clubs:',len({r['id']['value'] for r in rows}),'approved/publications: unchanged')

# Labels are facts from the same CC0 query; membership remains unapproved.
identity_path = DATA/'candidate-identities.json'
identities = json.loads(identity_path.read_text(encoding='utf-8')) if identity_path.exists() else {}
for row in rows:
 qid=row['person']['value'].rsplit('/',1)[-1]
 if qid not in identities and not any(k in row for k in ['labelPt','labelEn']):continue
 item=identities.setdefault(qid, {'type':'item','id':qid,'labels':{}})
 for key,lang in [('labelPt','pt'),('labelEn','en')]:
  if key in row:item.setdefault('labels',{})[lang]={'language':lang,'value':row[key]['value']}
identity_path.write_text(json.dumps(identities,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Identities cached:',len(identities))
