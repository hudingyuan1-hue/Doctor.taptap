using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Doctor
{
    public sealed class DoctorGame : MonoBehaviour
    {
        public Transform actor, cat, mother, door, key;
        public Camera roomCamera;
        public Light roomLight;
        public Transform[] damageMarks;
        public Transform[] furnitureScars;
        public WorldModel Model { get; private set; }
        public Parameters Config { get; private set; }
        public bool MenuOpen => menu != Menu.None;
        enum Menu { None, Controls, Abilities, Pause, Reset }
        Menu menu;
        bool debug, wasCat;
        float catLeaving, flash;
        Vector3 catFrom;
        Font font;
        GUIStyle title, body, small, button, speechStyle;
        Vector2 scroll;
        readonly Queue<GameObject> fragments = new Queue<GameObject>();
        string savePath;
        string status;
        public string SavePath => savePath;
        public bool validationMode;

        void Awake()
        {
            Application.targetFrameRate=60;
            Config=JsonUtility.FromJson<Parameters>(Resources.Load<TextAsset>("TestParameters").text);
            savePath=Path.Combine(Application.persistentDataPath,"first-room-v1.json");
            validationMode=Array.IndexOf(Environment.GetCommandLineArgs(),"-doctorValidate")>=0;
            WorldState saved=null;
            if(!validationMode && File.Exists(savePath)) {
                try { saved=JsonUtility.FromJson<WorldState>(File.ReadAllText(savePath));
                    if(saved==null || saved.schema!=1 || saved.damage==null || saved.damage.Length!=10) saved=null;
                } catch(Exception e){Debug.LogWarning("存档读取失败，将创建新档："+e.Message);}
            }
            Model=new WorldModel(Config,Config.randomSeed==0?Environment.TickCount:Config.randomSeed,saved);
            HookModel(); wasCat=Model.S.catPresent;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);
            SyncView(0);
            if(validationMode) gameObject.AddComponent<RuntimeValidation>();
        }
        void HookModel()
        {
            Model.EnvironmentDamaged+=SpawnDebris;
            Model.BodyAnomaly+=(p,pos)=>{flash=1;};
            Model.Changed+=line=>Debug.Log("[Doctor] "+line);
        }
        public void SetMenuForValidation(bool open) { SetMenu(open?Menu.Controls:Menu.None); }
        void SetMenu(Menu next)
        {
            menu=next; Model.Paused=MenuOpen;Time.timeScale=MenuOpen?0:1;
            if(MenuOpen)Save();
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F3))debug=!debug;
            if(Input.GetKeyDown(KeyCode.Escape))SetMenu(MenuOpen?Menu.None:Menu.Pause);
            if(!MenuOpen) {
                if(Input.GetMouseButtonDown(1))SetMenu(Menu.Abilities);
                else if(Input.GetMouseButtonDown(0)) {
                    var r=roomCamera.ScreenPointToRay(Input.mousePosition);
                    if(Physics.Raycast(r,out RaycastHit hit,100) && (hit.transform==actor || hit.transform.IsChildOf(actor)))SetMenu(Menu.Controls);
                }
            }
            if(!MenuOpen){Model.Tick(Time.deltaTime);SyncView(Time.deltaTime);}
        }
        void SyncView(float dt)
        {
            var s=Model.S;Vector3 pos=new Vector3(s.position.x,0,s.position.z);
            var delta=pos-actor.position;
            if(delta.sqrMagnitude>.00001f)actor.rotation=Quaternion.Slerp(actor.rotation,Quaternion.LookRotation(delta),dt*12);
            actor.position=pos;
            Transform torso=actor.Find("Body");
            if(torso!=null) {
                float lean=s.phase==Phase.Resting?70:flash*18;
                torso.localRotation=Quaternion.Euler(lean,0,Mathf.Sin(s.worldSeconds*19)*flash*8);
                var renderer=torso.GetComponent<Renderer>();
                if(renderer!=null)renderer.material.color=Color.Lerp(new Color(.4f,.68f,.85f),new Color(.29f,.31f,.34f),Mathf.Clamp01((s.round-1)*.09f));
            }
            flash=Mathf.Max(0,flash-dt);
            if(wasCat && !s.catPresent){catLeaving=Config.catDepartureSeconds;catFrom=cat.position;}
            wasCat=s.catPresent;
            if(s.catPresent){cat.gameObject.SetActive(true);cat.position=new Vector3(s.catPosition.x,.1f,s.catPosition.z+.35f);}
            else if(catLeaving>0){catLeaving-=dt;cat.gameObject.SetActive(true);cat.position=Vector3.Lerp(catFrom,new Vector3(-5.7f,1.5f,2.5f),1-catLeaving/Config.catDepartureSeconds);}
            else cat.gameObject.SetActive(false);
            mother.gameObject.SetActive(s.motherPresent);
            mother.position=s.phase==Phase.MotherAdvice?new Vector3(3.8f,0,1.2f):new Vector3(4.6f,0,3.1f);
            door.localRotation=Quaternion.Euler(0,s.doorOpen?82:0,0);
            key.gameObject.SetActive(!s.hasKey && !s.doorOpen && !s.catPresent && (s.keyRevealed || debug));
            key.position=new Vector3(WorldModel.Spots[s.keyPoint>=0?s.keyPoint:0].x,.13f,WorldModel.Spots[s.keyPoint>=0?s.keyPoint:0].z);
            float total=0;
            for(int i=0;i<damageMarks.Length;i++) {
                float amount=s.damage[i];total+=amount;damageMarks[i].gameObject.SetActive(amount>0);
                float size=Mathf.Min(Config.maxStainScale,.13f+amount*Config.stainScalePerDamage);
                damageMarks[i].localScale=new Vector3(size,.009f,size*1.6f);
                if(i<furnitureScars.Length)furnitureScars[i].gameObject.SetActive(amount>0);
            }
            roomLight.intensity=Mathf.Max(Config.minimumLightIntensity,Config.baseLightIntensity-total*Config.lightLossPerDamage);
        }
        void SpawnDebris(int id,float amount)
        {
            if(!Application.isPlaying)return;
            while(fragments.Count>=Mathf.Max(1,Config.maxFragments)){var old=fragments.Dequeue();if(old!=null)Destroy(old);}
            var bit=GameObject.CreatePrimitive(PrimitiveType.Cube);bit.name="Temporary fragment (non blocking)";
            var p=WorldModel.Spots[id];bit.transform.position=new Vector3(p.x,.45f,p.z);
            bit.transform.localScale=new Vector3(.08f,.07f,.12f);
            bit.GetComponent<Renderer>().material.color=new Color(.3f,.16f,.12f);
            var rb=bit.AddComponent<Rigidbody>();rb.mass=.05f;
            rb.AddForce(new Vector3(.4f,1,.25f)*Config.fragmentImpulse,ForceMode.Impulse);
            fragments.Enqueue(bit);Destroy(bit,Config.fragmentLifetime);
        }
        void Save()
        {
            if(validationMode || Model==null)return;
            try { Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                File.WriteAllText(savePath+".tmp",JsonUtility.ToJson(Model.S,true));
                if(File.Exists(savePath))File.Copy(savePath,savePath+".bak",true);
                File.Copy(savePath+".tmp",savePath,true);File.Delete(savePath+".tmp");
            } catch(Exception e){Debug.LogWarning("保存失败："+e.Message);status="保存失败："+e.Message;}
        }
        void OnApplicationQuit(){Save();}
        void OnDisable(){if(Model!=null)Save();Time.timeScale=1;}
        void InitStyles()
        {
            if(body!=null)return;
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};body.normal.textColor=new Color(.89f,.9f,.85f);
            small=new GUIStyle(body){fontSize=14};
            title=new GUIStyle(body){fontSize=27,fontStyle=FontStyle.Bold};
            speechStyle=new GUIStyle(body){fontSize=21,alignment=TextAnchor.MiddleCenter};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=17,wordWrap=true};
            button.padding=new RectOffset(12,12,8,8);
        }
        void Panel(Rect r,float alpha=.9f)
        {
            Color old=GUI.color;GUI.color=new Color(.055f,.075f,.09f,alpha);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;
        }
        bool Button(Rect r,string text)=>GUI.Button(r,text,button);
        void OnGUI()
        {
            if(Model==null)return;InitStyles();
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            var s=Model.S;
            Panel(new Rect(22,20,840,80),.86f);
            GUI.Label(new Rect(40,30,270,35),$"DOCTOR   /   周目 {s.round}",title);
            GUI.Label(new Rect(355,33,490,25),$"搜索 {s.searches}/10     神经元 {s.neurons}     神经簇 {s.clusters}",body);
            GUI.Label(new Rect(40,69,790,25),$"健康 {s.health:0}   精神 {s.mental:0}    ·    {PhaseText(s.phase)}",small);
            if(Button(new Rect(1150,22,106,40),"菜单  Esc"))SetMenu(MenuOpen?Menu.None:Menu.Pause);
            Panel(new Rect(250,603,780,91),.88f);
            GUI.Label(new Rect(269,609,742,38),s.speech,speechStyle);
            GUI.Label(new Rect(269,653,742,31),Model.ActionLabel,small);
            GUI.Label(new Rect(24,682,410,25),"左键角色：操控点    右键：能力图    F3：开发记录",small);
            if(s.phase==Phase.Complete || s.phase==Phase.Street) {
                Panel(new Rect(365,237,550,178));
                GUI.Label(new Rect(389,258,502,45),s.phase==Phase.Complete?"第一场景验证终点":"Street 事件出口",title);
                GUI.Label(new Rect(389,316,498,70),"当前进度已保留。后续楼层与完整 Street 玩法暂缓。\n可在菜单中开始新的测试档。",body);
            }
            if(debug && !MenuOpen)DrawDebug();
            if(!MenuOpen)return;
            Panel(new Rect(0,0,1280,720),.52f);Panel(new Rect(340,118,600,466),.96f);
            GUI.Label(new Rect(366,135,540,38),menu==Menu.Controls?"操控点":menu==Menu.Abilities?"能力图":menu==Menu.Reset?"开始新的测试档":"世界暂停",title);
            GUI.Label(new Rect(366,177,540,45),"世界已暂停 · 人物 / 伤害 / 概率 / 物理均停止",small);
            if(menu==Menu.Controls) {
                GUI.Label(new Rect(366,212,535,43),"影响："+Model.ActionLabel,body);
                SkillCard(ControlPoint.Skin,263,false);SkillCard(ControlPoint.Gut,360,false);
                GUI.Label(new Rect(366,463,540,50),"同点强度在本行动内累加；严格超过抗性才生效。每次均扣费并留下损伤。",small);
            } else if(menu==Menu.Abilities) {
                GUI.Label(new Rect(366,212,535,40),$"持有 {s.clusters} 神经簇 · 数值均为测试参数",body);
                SkillCard(ControlPoint.Skin,263,true);SkillCard(ControlPoint.Gut,360,true);
            } else if(menu==Menu.Reset) {
                GUI.Label(new Rect(366,235,530,80),"将清除当前测试档中的进度、能力与损伤。\n旧档备份保留为 .bak。",body);
                if(Button(new Rect(366,342,530,47),"确认新建测试档")) {
                    Save();Model=new WorldModel(Config,Environment.TickCount);HookModel();wasCat=Model.S.catPresent;
                    catLeaving=0;SetMenu(Menu.None);SyncView(0);Save();
                }
            } else {
                GUI.Label(new Rect(366,227,530,80),"角色会自行移动、搜索、休息与开门。\n你通过操控点干预；无法直接指挥移动。",body);
                if(Button(new Rect(366,319,252,44),"操控点"))SetMenu(Menu.Controls);
                if(Button(new Rect(635,319,252,44),"能力图"))SetMenu(Menu.Abilities);
                if(Button(new Rect(366,377,521,44),"开始新的测试档"))SetMenu(Menu.Reset);
                GUI.Label(new Rect(366,440,532,55),status??"自动保存于菜单打开或退出时。F3 查看结算记录。",small);
            }
            if(Button(new Rect(366,525,548,40),"关闭菜单并继续   /   Esc"))SetMenu(Menu.None);
        }
        void SkillCard(ControlPoint p,float y,bool upgrading)
        {
            int level=Model.Level(p);bool showResistance=debug || Model.S.awakeningPlayed[0];
            GUI.Label(new Rect(366,y,345,30),$"{WorldModel.Name(p)}   {(level>0?"Lv."+level:"未解锁")}",body);
            string detail=upgrading?$"施放强度 {Model.Strength(p):0.##} · 升级 +{Config.strengthPerLevel:0.##}":
                $"累计 {Model.Sum(p):0.##} · 单次强度 {Model.Strength(p):0.##}"+(showResistance?$" · 抗性 {Model.Resistance(p):0.##}":" · 抗性未知");
            GUI.Label(new Rect(366,y+34,354,47),detail,small);
            GUI.enabled=upgrading?Model.S.clusters>=(level==0?Config.unlockCost:Config.upgradeCost):Model.CanCast && level>0 && Model.S.neurons>=Model.Cost(p);
            if(Button(new Rect(726,y+4,187,59),upgrading?$"{(level==0?"解锁":"升级")} · {(level==0?Config.unlockCost:Config.upgradeCost)} 神经簇":$"施放 · {Model.Cost(p)} 神经元")) {
                if(upgrading)Model.Upgrade(p);else Model.Cast(p);Save();
            }
            GUI.enabled=true;
        }
        void DrawDebug()
        {
            Panel(new Rect(882,78,375,508),.91f);
            GUI.Label(new Rect(897,90,347,30),"开发记录 / 测试参数",body);
            GUI.Label(new Rect(897,123,348,62),$"受困 {Model.S.trapped} / 计划第 {Model.S.plannedSearches} 次\n皮肤抗性 {Model.S.skinResistance} / 肠胃 {Model.S.gutResistance}\n风险 {Model.S.risk:P0} · 本行动 {Model.S.actionSerial}",small);
            scroll=GUI.BeginScrollView(new Rect(896,200,348,369),scroll,new Rect(0,0,324,Model.S.journal.Length*49));
            for(int i=0;i<Model.S.journal.Length;i++)GUI.Label(new Rect(0,i*49,321,48),Model.S.journal[Model.S.journal.Length-1-i],small);
            GUI.EndScrollView();
        }
        static string PhaseText(Phase p)
        {
            switch(p){case Phase.BeforeSearch:return "准备搜索";case Phase.Moving:return "前往搜索位置";case Phase.Searching:return "正在搜索";
                case Phase.Reaction:return "身体反应";case Phase.Boundary:return "行动结算";case Phase.MotherDoor:return "母亲在门口";
                case Phase.MotherAdvice:return "母亲劝休息";case Phase.RestMoving:return "回床休息";case Phase.Resting:return "休息中";
                case Phase.Exiting:return "离开房间";case Phase.Complete:return "第一场景已解锁";default:return "Street 出口";}
        }
    }
}
