"""Fetch a CC0 binary source only; no downloaded code is executed."""
import hashlib,json,pathlib,urllib.request
root=pathlib.Path(__file__).resolve().parents[2]
credits=json.loads((root/'frontend/public/data/asset-credits.json').read_text(encoding='utf8'))['assets'][0]
url='https://raw.githubusercontent.com/Seyamalam/blood-league-kickoff/main/public/assets/vendor/quaternius/universal-animation-library.glb'
data=urllib.request.urlopen(url,timeout=45).read()
if hashlib.sha256(data).hexdigest()!=credits['sourceSha256']:raise SystemExit('Source hash changed. Review source/license before updating the lock.')
path=root/'.runtime/assets/universal-animation-library.glb';path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data);print(path)
