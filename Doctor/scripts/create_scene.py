"""Deterministic Unity text-asset authoring. No editor/license required to materialize the scene."""
from pathlib import Path
import hashlib, json, math

ROOT=Path(r'D:\Doctor\DoctorPrototype')
ASSET=ROOT/'Assets/Doctor'
def guid(name): return hashlib.md5(('doctor-first-room:'+name).encode()).hexdigest()
def write(path,text):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,encoding='utf-8')
def meta(path,kind='DefaultImporter',extra=''):
    rel=path.relative_to(ROOT).as_posix()
    write(str(path)+'.meta',f'fileFormatVersion: 2\nguid: {guid(rel)}\n{kind}:\n  externalObjects: {{}}\n{extra}  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
for path in ASSET.rglob('*.cs'):
    meta(path,'MonoImporter','  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n')
for path in ASSET.rglob('*.json'):
    meta(path,'TextScriptImporter')

colors={'floor':(.42,.4,.34),'wall':(.63,.62,.52),'trim':(.25,.29,.27),'wood':(.36,.25,.18),
        'linen':(.70,.72,.64),'blue':(.4,.68,.85),'skin':(.79,.64,.49),'hair':(.13,.10,.09),
        'cat':(.39,.44,.48),'collar':(.11,.34,.8),'mother':(.60,.35,.29),'apron':(.87,.80,.59),
        'headband':(.96,.75,.22),'blood':(.32,.035,.035),'key':(.96,.73,.16),'glass':(.36,.55,.61),
        'rug':(.30,.37,.37),'book':(.37,.43,.33),'metal':(.17,.20,.20)}
for name,c in colors.items():
    path=ASSET/f'Materials/{name}.mat'
    write(path,f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Glossiness: 0.15
    - _Metallic: 0
    m_Colors:
    - _Color: {{r: {c[0]}, g: {c[1]}, b: {c[2]}, a: 1}}
  m_BuildTextureStacks: []
''');meta(path,'NativeFormatImporter','  mainObjectFileID: 2100000\n')

objects=[];nextid=1000
def obj(name,pos=(0,0,0),scale=(1,1,1),parent=None,mesh=None,mat='wood',collider=False,rotation=(0,0,0),tag='Untagged'):
    global nextid
    data=dict(id=nextid,name=name,pos=pos,scale=scale,parent=parent,mesh=mesh,mat=mat,collider=collider,rotation=rotation,tag=tag,children=[],components=[])
    nextid+=20;objects.append(data)
    if parent:parent['children'].append(data)
    return data
def cube(name,pos,scale,mat='wood',parent=None,collider=True):return obj(name,pos,scale,parent,10202,mat,collider)
def sphere(name,pos,scale,mat,parent=None):return obj(name,pos,scale,parent,10207,mat,True)
def capsule(name,pos,scale,mat,parent=None):return obj(name,pos,scale,parent,10208,mat,True)
room=obj('Room - fixed furniture and open center aisle')
cube('Floor',(0,-.16,0),(12,.3,8),'floor',room)
cube('Back wall',(-.9,1.6,4),(10.2,3.2,.18),'wall',room)
cube('Door right wall',(5.7,1.6,4),(.6,3.2,.18),'wall',room)
cube('Door lintel',(4.6,2.9,4),(1.7,.6,.18),'trim',room)
cube('Left wall',(-6,1.6,0),(.18,3.2,8),'wall',room)
cube('Skirting back',(-.9,.12,3.85),(10.2,.24,.10),'trim',room)
cube('Window',(-5.88,1.8,1.7),(.035,1.5,1.8),'glass',room,False)
cube('Window mullion',(-5.85,1.8,1.7),(.05,1.55,.08),'trim',room,False)
cube('Rug',(0,.005,0),(5.6,.018,2.25),'rug',room,False)
furn=[(-4,3,.7,.85,.65),(-2,3,1.3,2.3,.85),(0,3,1.6,.95,.85),(2,3,1.2,1.9,.65),
      (-4,-3,1.45,.48,1.45),(-2,-3,1.15,.60,.8),(0,-3,1.3,.60,.85),(2,-3,1.3,.68,.85),
      (4,-3,1.25,.52,.85),(4,3,.72,.72,.6)]
labels=['Bedside','Wardrobe','Desk','Bookshelf','Bed','Chest','Low cabinet','Drawers','Storage','Door cabinet']
scars=[]
for i,(x,z,w,h,d) in enumerate(furn):
    root=obj(f'SearchPoint_{i:02d}_{labels[i]}',parent=room)
    cube(labels[i],(x,h/2,z),(w,h,d),'wood',root)
    scar=cube(f'FurnitureScar_{i:02d}',(x+.18,h*.55,z+(-1 if z>0 else 1)*(d/2+.022)),(.055,h*.65,.016),'blood',root,False)
    scars.append(scar)
    if i!=4:
        cube('Handle',(x,h*.72,z+(-1 if z>0 else 1)*(d/2+.015)),(.22,.045,.03),'metal',root,False)
    if i==4:
        cube('Mattress',(x,.57,z),(1.4,.18,1.44),'linen',root)
        cube('Pillow',(x,.73,z-.4),(1,.13,.35),'linen',root,False)
    if i==2:
        cube('Closed book',(x+.25,h+.045,z),(.4,.09,.28),'book',root,False)
    if i==3:
        for j in range(5):cube('Book',(x-.38+j*.17,h+.16,z),(.12,.32,.33),'book',root,False)
door=obj('DoorHinge',(3.8,0,3.94),parent=room)
cube('Door',(0.77,1.27,0),(1.54,2.54,.12),'wood',door)
sphere('Doorknob',(1.31,1.18,-.12),(.12,.12,.12),'key',door)
actor=obj('AutonomousCharacter')
capsule('Body',(0,.97,0),(.48,.53,.36),'blue',actor)
sphere('Head',(0,1.65,0),(.43,.48,.43),'skin',actor)
sphere('Hair',(0,1.80,-.015),(.44,.23,.44),'hair',actor)
for x in [-.15,.15]:
    capsule('Leg',(x,.33,0),(.18,.30,.18),'blue',actor)
    cube('Foot',(x,.075,.08),(.18,.13,.31),'metal',actor)
for x in [-.35,.35]:capsule('Arm',(x,.98,0),(.15,.36,.15),'blue',actor)
cat=obj('Cat')
sphere('Body',(0,.29,0),(.6,.42,.8),'cat',cat)
sphere('Head',(0,.53,.30),(.4,.39,.4),'cat',cat)
cube('Blue collar',(0,.40,.22),(.4,.08,.12),'collar',cat,False)
for x in [-.14,.14]:cube('Ear',(x,.76,.30),(.12,.18,.12),'cat',cat,False)
obj('Tail',(.18,.35,-.46),(.08,.08,.6),cat,10208,'cat',False,(80,0,30))
mother=obj('Mother')
capsule('Dress',(0,.75,0),(.66,.66,.42),'mother',mother)
cube('Apron',(0,.80,-.23),(.48,.68,.055),'apron',mother,False)
for x in [-.15,0,.15]:sphere('Apron flower',(x,.9,-.275),(.09,.09,.03),'mother',mother)
sphere('Head',(0,1.65,0),(.43,.49,.43),'skin',mother)
sphere('Long hair',(0,1.48,.12),(.50,.8,.30),'mother',mother)
cube('Yellow headband',(0,1.83,-.13),(.43,.08,.18),'headband',mother,False)
for x in [-.4,.4]:capsule('Arm',(x,.99,0),(.16,.35,.16),'mother',mother)
key=obj('ActualKey')
cube('Shaft',(0,0,0),(.3,.04,.05),'key',key,False)
sphere('Ring',(-.2,0,0),(.16,.05,.16),'key',key)
spots=[(-4,1.9),(-2,1.9),(0,1.9),(2,1.9),(-4,-1.9),(-2,-1.9),(0,-1.9),(2,-1.9),(4,-1.9),(4,1.9)]
marks=[]
for i,(x,z) in enumerate(spots):marks.append(cube(f'Damage_{i:02d}_stable',(x+.35,.023,z+.16),(.001,.009,.001),'blood',room,False))
camera=obj('Main Camera',(10,12,-16),rotation=(32,-32,0),tag='MainCamera');camera['components']=['camera','listener']
light=obj('RoomLight',rotation=(48,-35,0));light['components']=['light']
fill=obj('Window fill',rotation=(35,130,0));fill['components']=['fill']
controller=obj('DoctorGame - rules and UI');controller['components']=['controller']

def vec(v):return '{x: %s, y: %s, z: %s}'%tuple(v)
def quat(angles):
    x,y,z=[math.radians(a)/2 for a in angles];sx,cx=math.sin(x),math.cos(x);sy,cy=math.sin(y),math.cos(y);sz,cz=math.sin(z),math.cos(z)
    return '{x: %.8f, y: %.8f, z: %.8f, w: %.8f}'%(cy*sx*cz+sy*cx*sz,sy*cx*cz-cy*sx*sz,cy*cx*sz-sy*sx*cz,cy*cx*cz+sy*sx*sz)
out=['''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!104 &1
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 10
  m_Fog: 0
  m_AmbientSkyColor: {r: 0.45, g: 0.48, b: 0.52, a: 1}
  m_AmbientEquatorColor: {r: 0.3, g: 0.32, b: 0.34, a: 1}
  m_AmbientGroundColor: {r: 0.2, g: 0.2, b: 0.2, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 3
  m_SkyboxMaterial: {fileID: 0}
  m_Sun: {fileID: 0}
''']
for o in objects:
    i=o['id']; comps=[i+1]
    if o['mesh']:comps += [i+2,i+3]
    if o['collider']:comps += [i+4]
    comps += [i+5+j for j,_ in enumerate(o['components'])]
    out.append(f'''--- !u!1 &{i}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
'''+''.join(f'  - component: {{fileID: {c}}}\n' for c in comps)+f'''  m_Layer: 0
  m_Name: {o['name']}
  m_TagString: {o['tag']}
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{i+1}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {i}}}
  serializedVersion: 2
  m_LocalRotation: {quat(o['rotation'])}
  m_LocalPosition: {vec(o['pos'])}
  m_LocalScale: {vec(o['scale'])}
  m_ConstrainProportionsScale: 0
'''+('  m_Children:\n'+''.join(f"  - {{fileID: {c['id']+1}}}\n" for c in o['children']) if o['children'] else '  m_Children: []\n')+f'''  m_Father: {{fileID: {o['parent']['id']+1 if o['parent'] else 0}}}
  m_LocalEulerAnglesHint: {vec(o['rotation'])}
''')
    if o['mesh']:
        out.append(f'''--- !u!33 &{i+2}
MeshFilter:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: {i}}}
  m_Mesh: {{fileID: {o['mesh']}, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &{i+3}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: {i}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {guid('Assets/Doctor/Materials/'+o['mat']+'.mat')}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SortingLayerID: 0
  m_SortingOrder: 0
''')
    if o['collider']:
        out.append(f'''--- !u!65 &{i+4}
BoxCollider:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: {i}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 1, y: 1, z: 1}}
  m_Center: {{x: 0, y: 0, z: 0}}
''')
    for j,comp in enumerate(o['components']):
        cid=i+5+j
        if comp=='camera':
            out.append(f'''--- !u!20 &{cid}
Camera:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: {i}}}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {{r: 0.085, g: 0.105, b: 0.12, a: 1}}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 100
  field of view: 60
  orthographic: 1
  orthographic size: 6.9
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {{fileID: 0}}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
''')
        elif comp=='listener':out.append(f'--- !u!81 &{cid}\nAudioListener:\n  m_ObjectHideFlags: 0\n  m_GameObject: {{fileID: {i}}}\n  m_Enabled: 1\n')
        elif comp in ('light','fill'):
            out.append(f'''--- !u!108 &{cid}
Light:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: {i}}}
  m_Enabled: 1
  serializedVersion: 11
  m_Type: 1
  m_Shape: 0
  m_Color: {{r: 1, g: 0.92, b: 0.79, a: 1}}
  m_Intensity: {1.25 if comp=='light' else .45}
  m_Range: 10
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.8
  m_CookieSize: 10
  m_Shadows:
    m_Type: {2 if comp=='light' else 0}
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 0.7
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
''')
        else:
            out.append(f'''--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: {i}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid('Assets/Doctor/Scripts/DoctorGame.cs')}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  actor: {{fileID: {actor['id']+1}}}
  cat: {{fileID: {cat['id']+1}}}
  mother: {{fileID: {mother['id']+1}}}
  door: {{fileID: {door['id']+1}}}
  key: {{fileID: {key['id']+1}}}
  roomCamera: {{fileID: {camera['id']+5}}}
  roomLight: {{fileID: {light['id']+5}}}
  damageMarks:
'''+''.join(f"  - {{fileID: {m['id']+1}}}\n" for m in marks)+'  furnitureScars:\n'+''.join(f"  - {{fileID: {m['id']+1}}}\n" for m in scars)+'  validationMode: 0\n')
out.append('--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n'+''.join(f"  - {{fileID: {o['id']+1}}}\n" for o in objects if o['parent'] is None))
scene=ASSET/'Scenes/FirstRoom.unity';write(scene,''.join(out));meta(scene)
write(ROOT/'ProjectSettings/ProjectVersion.txt','m_EditorVersion: 6000.3.16f1\nm_EditorVersionWithRevision: 6000.3.16f1 (a56f230f6470)\n')
write(ROOT/'ProjectSettings/EditorBuildSettings.asset',f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/Doctor/Scenes/FirstRoom.unity
    guid: {guid('Assets/Doctor/Scenes/FirstRoom.unity')}
  m_configObjects: {{}}
''')
write(ROOT/'ProjectSettings/ProjectSettings.asset','''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 28
  productGUID: d0c70000000000000000000000000001
  companyName: DoctorPrototype
  productName: Doctor First Room
  defaultScreenWidth: 1280
  defaultScreenHeight: 720
  defaultIsNativeResolution: 0
  runInBackground: 1
  captureSingleScreen: 0
  resizableWindow: 1
  fullscreenMode: 3
  colorSpace: 0
  activeInputHandler: 0
  scriptingBackend:
    Standalone: 0
  apiCompatibilityLevelPerPlatform:
    Standalone: 6
  applicationIdentifier:
    Standalone: com.doctor.firstroom
''')
modules=['audio','imgui','inputlegacy','jsonserialize','physics','screencapture','ui']
write(ROOT/'Packages/manifest.json',json.dumps({'dependencies':{f'com.unity.modules.{m}':'1.0.0' for m in modules}},indent=2))
write(ROOT/'.gitignore','[Ll]ibrary/\n[Tt]emp/\n[Oo]bj/\n[Ll]ogs/\n[Uu]serSettings/\n[Bb]uild/\n*.csproj\n*.sln\n.vs/\n')
print(f'Authored {len(objects)} scene objects, {len(colors)} materials, all scene and script references serialized.')
