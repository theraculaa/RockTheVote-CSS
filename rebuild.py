"""Reproduce the 0.2.0 native package from the complete corresponding source tree."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,zipfile
ROOT=Path(__file__).resolve().parent
NATIVE=ROOT/'native'
def archive(folder,target):
 with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9)as z:
  for f in sorted(folder.rglob('*')):
   if f.is_file():
    i=zipfile.ZipInfo(str(f.relative_to(folder)),date_time=(2026,11,4,0,0,0));i.compress_type=zipfile.ZIP_DEFLATED;i.external_attr=0o100644<<16;z.writestr(i,f.read_bytes())
def main():
 for name in('OsnetSkins','OsnetSkinMenuCore','OsnetSkinMenu'):
  subprocess.run([os.environ.get('OSNET_DOTNET','dotnet'),'build',str(NATIVE/name/(name+'.csproj')),'-c','Release','--nologo','--disable-build-servers'],check=True)
 stage=ROOT/'runtime-stage';shutil.rmtree(stage,ignore_errors=True);stage.mkdir()
 for name in('OsnetSkins','OsnetSkinMenuCore','OsnetSkinMenu'):
  source=NATIVE/name/'bin/Release/net10.0';dest=stage/'addons/counterstrikesharp/plugins'/name;dest.mkdir(parents=True)
  for p in source.rglob('*'):
   if not p.is_file()or p.suffix=='.pdb'or p.name in('CounterStrikeSharp.API.dll','OsnetSkinMenuApi.dll','OsnetSkinMenuApi.deps.json')or'runtimes'in p.parts:continue
   if p.suffix not in('.dll','.json')and'Data'not in p.parts and'lang'not in p.parts:continue
   target=dest/p.relative_to(source);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
  for p in NATIVE.glob('LICENSE-*'):shutil.copyfile(p,dest/p.name)
  (dest/'SOURCE-COMMIT.txt').write_text(os.environ.get('GITHUB_SHA','local-unpublished-build')+'\n')
 shared=stage/'addons/counterstrikesharp/shared/OsnetSkinMenuApi';shared.mkdir(parents=True)
 for suffix in('.dll','.deps.json'):
  p=NATIVE/'OsnetSkinMenuApi/bin/Release/net10.0'/('OsnetSkinMenuApi'+suffix)
  if p.exists():shutil.copyfile(p,shared/p.name)
 gd=stage/'addons/counterstrikesharp/gamedata';gd.mkdir();shutil.copyfile(NATIVE/'OsnetSkins/gamedata/weaponskins.json',gd/'osnet-skins.json')
 archive(stage,ROOT/'OsnetSkins-v0.2.0.zip')
 source=ROOT/'source-stage';shutil.rmtree(source,ignore_errors=True);source.mkdir()
 shutil.copytree(NATIVE,source/'native',ignore=shutil.ignore_patterns('bin','obj','__pycache__','*.log'))
 shutil.copyfile(ROOT/'rebuild.py',source/'rebuild.py')
 archive(source,ROOT/'OsnetSkins-v0.2.0-source.zip')
 (ROOT/'SHA256SUMS').write_text(''.join(hashlib.sha256((ROOT/name).read_bytes()).hexdigest()+'  '+name+'\n'for name in('OsnetSkins-v0.2.0.zip','OsnetSkins-v0.2.0-source.zip')))
 print((ROOT/'SHA256SUMS').read_text())
if __name__=='__main__':main()
