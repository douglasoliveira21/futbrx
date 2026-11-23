"""Copy explicitly CC0, publicly playable Freesound previews; no login/download endpoint is used."""
import hashlib, json, pathlib, re, urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEST = ROOT / 'frontend/public/assets/audio'
TRACKS = [
    ('crowd', 'Sandermotions', '494351', 'Soccer Stadium 01.wav'),
    ('goal', 'Sandermotions', '494352', 'Goal.wav'),
    ('near-miss', 'Sandermotions', '494362', 'Soccer stadium Oehh.wav'),
    ('kick', 'bittermelonheart', '555042', 'Soccer Ball Kick'),
    ('whistle', 'Rosa-Orenes256', '538422', 'Referee whistle sound.wav'),
]

def fetch(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'BrasileiraoManager/0.12 (CC0 game-audio attribution)'}), timeout=25).read()

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    assets = []
    for key, author, ident, title in TRACKS:
        page = f'https://freesound.org/people/{author}/sounds/{ident}/'
        text = fetch(page).decode()
        if 'creativecommons.org/publicdomain/zero/1.0/' not in text:
            raise ValueError(f'{key}: CC0 licence could not be verified')
        urls = re.findall(r'https://cdn\.freesound\.org/previews/[^\s"<>]+-hq\.mp3', text)
        if not urls:
            raise ValueError(f'{key}: no public high-quality player preview')
        url = urls[0]; target = DEST / f'{key}.mp3'
        raw = target.read_bytes() if target.exists() else fetch(url)
        if not 100 < len(raw) < 2_000_000: raise ValueError('Audio exceeds budget')
        target.write_bytes(raw)
        assets.append({'id':key,'title':title,'author':author,'source':page,'audioSource':url,'path':f'/assets/audio/{key}.mp3','licence':'CC0-1.0','licenceUrl':'https://creativecommons.org/publicdomain/zero/1.0/','reviewedOn':'2026-11-21','format':'Public MP3 player preview, unchanged','bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
        print(key, len(raw), 'bytes; CC0 verified')
    (DEST/'credits.json').write_text(json.dumps({'assets':assets},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (DEST/'ATTRIBUTION.txt').write_text('\n\n'.join(f"{a['title']} — {a['author']}\n{a['source']}\nCC0 1.0: {a['licenceUrl']}\nPublic player MP3 preview, unchanged.\nSHA256: {a['sha256']}" for a in assets)+'\n',encoding='utf-8')

if __name__ == '__main__': main()
