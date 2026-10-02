from pathlib import Path
import re, json, math

root=Path(__file__).resolve().parent.parent
project=root/'DoctorPrototype'
scene=(project/'Assets/Doctor/Scenes/FirstRoom.unity').read_text(encoding='utf-8')
ids=re.findall(r'^--- !u!\d+ &(\d+)$',scene,re.M)
assert len(ids)==len(set(ids)), 'Duplicate object/component fileID'
defined=set(ids)
refs=re.findall(r'\{fileID: (\d+)\}',scene)
missing=set(refs)-defined-{'0'}
assert not missing, f'Unresolved local file IDs: {missing}'
known={}
for p in (project/'Assets').rglob('*.meta'):
    match=re.search(r'^guid: ([a-f0-9]+)$',p.read_text(encoding='utf-8'),re.M)
    if match:known[match[1]]=p
for p in (project/'Assets').rglob('*'):
    if p.suffix not in ('.unity','.mat'):continue
    for g in re.findall(r'guid: ([a-f0-9]+)',p.read_text(encoding='utf-8')):
        assert g in known or g in ['0000000000000000e000000000000000','0000000000000000f000000000000000'],f'Missing asset {g}'
assert 'Assets/Doctor/Scenes/FirstRoom.unity' in (project/'ProjectSettings/EditorBuildSettings.asset').read_text()
assert 'activeInputHandler: 0' in (project/'ProjectSettings/ProjectSettings.asset').read_text()
assert '6000.3.16f1' in (project/'ProjectSettings/ProjectVersion.txt').read_text()
config=json.loads((project/'Assets/Doctor/Resources/TestParameters.json').read_text())
for f in ['skinCost','gutCost','unlockCost','upgradeCost']:assert config[f]>0
for f in ['moveSpeed','searchSeconds','restSeconds','faintCheckSeconds']:assert config[f]>0
assert config['catNearDistance']<config['catMediumDistance']<config['catNoticeDistance']
assert len(re.findall('  m_Name: Damage_',scene))==10
assert len(re.findall('  m_Name: FurnitureScar_',scene))==10
for field in ['actor','cat','mother','door','key','roomCamera','roomLight']:
    assert re.search(r'^  '+field+r': \{fileID: [1-9]\d*\}',scene,re.M),f'Unwired {field}'

# Sample all authored travel segments against fixed furniture with a conservative body radius.
furniture=[(-4,3,.7,.65),(-2,3,1.3,.85),(0,3,1.6,.85),(2,3,1.2,.65),(-4,-3,1.45,1.45),
           (-2,-3,1.15,.8),(0,-3,1.3,.85),(2,-3,1.3,.85),(4,-3,1.25,.85),(4,3,.72,.6)]
targets=[(-4,1.9),(-2,1.9),(0,1.9),(2,1.9),(-4,-1.9),(-2,-1.9),(0,-1.9),(2,-1.9),(4,-1.9),(4,1.9),(-4,-1),(4.85,2.6)]
samples=0
for start in targets:
    for end in targets:
        route=[start,(start[0],0),(end[0],0),end]
        for a,b in zip(route,route[1:]):
            for i in range(101):
                x=a[0]+(b[0]-a[0])*i/100;z=a[1]+(b[1]-a[1])*i/100
                for fx,fz,w,d in furniture:
                    dx=max(abs(x-fx)-w/2,0);dz=max(abs(z-fz)-d/2,0)
                    assert math.hypot(dx,dz)>=.28,f'Body route intersects furniture: {(x,z)}'
                samples+=1
result=dict(type='Static scene and reference validation; not Unity import',objects=len(re.findall(r'^--- !u!1 &',scene,re.M)),
            materials=len(list((project/'Assets/Doctor/Materials').glob('*.mat'))),componentsAndObjects=len(ids),routeSamples=samples,passed=True)
(root/'logs/asset-checks.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
