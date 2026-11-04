"""Verified original source + small reviewed overlay; never executes unverified downloads."""
from pathlib import Path
import hashlib,io,shutil,subprocess,sys,urllib.request,zipfile
ROOT=Path(__file__).resolve().parent
URL='https://github.com/theraculaa/RockTheVote-CSS/releases/download/osnet-skins-v0.1.0-r1/OsnetSkins-v0.1.0-source.zip'
SHA='dc96166bff24731da7c863cfa379e3e56f75370d0a8f58d8fd2f7294c3badf31'
def main():
 req=urllib.request.Request(URL,headers={'User-Agent':'OsnetPortal-build'})
 with urllib.request.urlopen(req,timeout=120)as f:b=f.read(64*1024*1024)
 if hashlib.sha256(b).hexdigest()!=SHA:raise RuntimeError('Original source SHA mismatch')
 root=ROOT/'native';root.mkdir(exist_ok=True)
 with zipfile.ZipFile(io.BytesIO(b))as z:
  for item in z.infolist():
   path=Path(item.filename)
   if item.is_dir():continue
   if path.is_absolute()or'..'in path.parts:raise RuntimeError('Unsafe archive')
   target=root/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(z.read(item))
 for p in(ROOT/'overlay').rglob('*'):
  if p.is_file():target=root/p.relative_to(ROOT/'overlay');target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
 subprocess.run([sys.executable,str(ROOT/'rebuild.py')],check=True)
if __name__=='__main__':main()
