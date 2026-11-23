"""Fetch reviewed originals, preserving attribution; never infer rights from Wikidata alone."""
import hashlib,html,json,pathlib,re,urllib.request,urllib.parse,time
ROOT=pathlib.Path(__file__).resolve().parents[2];DATA=ROOT/'data/real-2026'
def plain(value):return html.unescape(re.sub('<[^>]+>','',value)).strip()
def run():
 files=json.loads((DATA/'crest-files.json').read_text(encoding='utf-8'))
 pages={v['title'][5:]:v for v in json.loads((DATA/'all-crest-license-evidence.json').read_text(encoding='utf-8'))['query']['pages'].values()}
 clubs=json.loads((DATA/'clubs.json').read_text(encoding='utf-8'))['clubs'];assets=[];blocked=False
 dest=ROOT/'frontend/public/assets/crests';dest.mkdir(exist_ok=True)
 for club in clubs:
  id=club['id'];row={'clubId':id,'name':club['name'],'reviewedOn':'2026-11-21','status':'pending','path':None}
  if id in ['cor','rbb']:
   row['reason']='Corinthians: arquivo recente autodeclarado não comprova origem oficial; fotografia antiga não foi adotada como arte do escudo.' if id=='cor' else 'Não foi localizado arquivo atual com concessão de direitos verificável; escudo histórico anterior a 2020 não foi adotado.'
   assets.append(row);continue
  f=files.get(id,[None])[0];page=pages.get(f,{});info=page.get('imageinfo',[{}])[0];m=info.get('extmetadata',{});lic=m.get('LicenseShortName',{}).get('value','')
  if lic not in ['Public domain','CC BY-SA 3.0','CC BY-SA 4.0','CC BY 3.0 br']:
   row['reason']='Licença não aprovada: '+lic;assets.append(row);continue
  row.update(source=info['descriptionurl'],author=plain(m.get('Artist',{}).get('value','')),license=lic,licenseUrl=m.get('LicenseUrl',{}).get('value','https://creativecommons.org/publicdomain/mark/1.0/'),restrictions=m.get('Restrictions',{}).get('value',''))
  suffix=pathlib.Path(f).suffix.lower();target=dest/(id+suffix)
  if blocked and not target.exists():
   row['reason']='Licença revisada; download adiado após limite do provedor.';assets.append(row);continue
  try:
   if target.exists():raw=target.read_bytes()
   else:
    raw=urllib.request.urlopen(urllib.request.Request(info['url'],headers={'User-Agent':'BrasileiraoManager/0.9 (licensed crest originals)'}),timeout=30).read();time.sleep(15)
   if len(raw)>2_000_000:raise ValueError('File exceeds asset budget')
   if suffix=='.svg' and re.search(rb'<(?:\w+:)?(?:script|foreignObject)\b|\bon(?:load|error|click)\s*=',raw,re.I):raise ValueError('Active SVG rejected')
   target.write_bytes(raw)
   row.update(status='included',path='/assets/crests/'+target.name,source=info['descriptionurl'],author=plain(m.get('Artist',{}).get('value','')),credit=plain(m.get('Credit',{}).get('value','')),license=lic,licenseUrl=m.get('LicenseUrl',{}).get('value','https://creativecommons.org/publicdomain/mark/1.0/'),restrictions=m.get('Restrictions',{}).get('value',''),sha256=hashlib.sha256(raw).hexdigest(),bytes=len(raw),changes='Original sem alteração visual')
  except Exception as e:
   row['reason']='Download temporariamente indisponível; licença já revisada.'
   if isinstance(e,urllib.error.HTTPError) and e.code==429:
    blocked=True;row['retryAfter']=e.headers.get('Retry-After','Provedor sem prazo; retomar em outra execução.')
  assets.append(row)
 manifest={'reviewedOn':'2026-11-21','notice':'Licenças individuais conforme o arquivo no Wikimedia Commons. Marcas identificam os clubes; sem vínculo ou endosso oficial. A licença de cada arquivo permanece aplicável.','clubs':assets}
 for p in [DATA/'crest-review.json',ROOT/'frontend/public/data/crest-credits.json']:
  p.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 (dest/'ATTRIBUTION.txt').write_text(manifest['notice']+'\n\n'+'\n\n'.join('\n'.join(str(a.get(k,'')) for k in ['name','author','credit','source','license','licenseUrl','changes']) for a in assets if a['status']=='included'),encoding='utf-8')
 print('Included',sum(a['status']=='included' for a in assets),'pending',sum(a['status']!='included' for a in assets))
if __name__=='__main__':run()
