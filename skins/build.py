"""Build pinned source adaptations and deterministic installable runtime archives."""
from pathlib import Path
import concurrent.futures,hashlib,io,json,os,shutil,subprocess,sys,urllib.request,zipfile
ROOT=Path(__file__).resolve().parent
PINS=[('skin','Staaar0/WeaponSkins','1425290ae3d29493171c70e9ed55f74e688c366e'),('core','RRimmer/PanoramaMenuManagerCS2','279d2bbfac084ed3f2b8e286932a5d9b707b1fb0'),('pmm','Staaar0/PMM_WeaponSkins','8db43e369814ba48f2bb653ccf8ae9920f06fa92')]

def sources():
    def fetch(row):
        name,repo,commit=row;dest=ROOT/'src'/name
        if dest.is_dir():return
        req=urllib.request.Request('https://api.github.com/repos/'+repo+'/zipball/'+commit,headers={'User-Agent':'OsnetSkinMenu-build'})
        with urllib.request.urlopen(req,timeout=90)as f:data=f.read(64*1024*1024)
        with zipfile.ZipFile(io.BytesIO(data))as z:
            for item in z.infolist():
                parts=Path(item.filename).parts[1:]
                if item.is_dir()or not parts:continue
                if '..'in parts:raise RuntimeError('Unsafe source archive')
                p=dest/Path(*parts);p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(z.read(item))
    with concurrent.futures.ThreadPoolExecutor(max_workers=3)as pool:list(pool.map(fetch,PINS))

def archive(folder,target):
    with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9)as z:
        for p in sorted(folder.rglob('*')):
            if not p.is_file():continue
            name=str(p.relative_to(folder));info=zipfile.ZipInfo(name,date_time=(2026,11,4,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=(0o100644<<16);z.writestr(info,p.read_bytes())

def package():
    stage=ROOT/'runtime-stage'
    if stage.exists():shutil.rmtree(stage)
    stage.mkdir()
    for name in ('OsnetSkins','OsnetSkinMenuCore','OsnetSkinMenu'):
        src=ROOT/'adapted'/name/'bin/Release/net10.0';dest=stage/'addons/counterstrikesharp/plugins'/name
        dest.mkdir(parents=True)
        for p in src.rglob('*'):
            if not p.is_file()or p.suffix in('.pdb',)or p.name in('CounterStrikeSharp.API.dll','OsnetSkinMenuApi.dll','OsnetSkinMenuApi.deps.json')or 'runtimes'in p.parts:continue
            if p.suffix not in('.dll','.json')and 'Data'not in p.parts and 'lang'not in p.parts:continue
            target=dest/p.relative_to(src);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
        for lic in (ROOT/'adapted').glob('LICENSE-*'):shutil.copyfile(lic,dest/lic.name)
    shared=stage/'addons/counterstrikesharp/shared/OsnetSkinMenuApi';shared.mkdir(parents=True)
    for suffix in('.dll','.deps.json'):
        src=ROOT/'adapted/OsnetSkinMenuApi/bin/Release/net10.0'/('OsnetSkinMenuApi'+suffix)
        if src.exists():shutil.copyfile(src,shared/src.name)
    gd=stage/'addons/counterstrikesharp/gamedata';gd.mkdir()
    shutil.copyfile(ROOT/'src/skin/WeaponSkins/gamedata/weaponskins.json',gd/'osnet-skins.json')
    shutil.copyfile(ROOT/'adapted/PROVENANCE.json',stage/'PROVENANCE.json')
    archive(stage,ROOT/'OsnetSkins-v0.1.0.zip')
    # Complete corresponding GPL sources, including upstream data and our patch/build scripts.
    source=ROOT/'source-stage'
    if source.exists():shutil.rmtree(source)
    shutil.copytree(ROOT/'adapted',source,ignore=shutil.ignore_patterns('bin','obj','*.build.log'))
    for name in ('adapt.py','build.py'):shutil.copyfile(ROOT/name,source/name)
    archive(source,ROOT/'OsnetSkins-v0.1.0-source.zip')
    sums=''.join(hashlib.sha256((ROOT/n).read_bytes()).hexdigest()+'  '+n+'\n'for n in('OsnetSkins-v0.1.0.zip','OsnetSkins-v0.1.0-source.zip'))
    (ROOT/'SHA256SUMS').write_text(sums);print(sums)

if __name__=='__main__':
    sources()
    subprocess.run([sys.executable,str(ROOT/'adapt.py')],check=True)
    dotnet=os.environ.get('OSNET_DOTNET','dotnet')
    for name in ('OsnetSkins','OsnetSkinMenuCore','OsnetSkinMenu'):
        subprocess.run([dotnet,'build',str(ROOT/'adapted'/name/(name+'.csproj')),'-c','Release','--nologo','--disable-build-servers'],check=True)
    package()
