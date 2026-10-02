using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Doctor;

static class CoreChecks
{
    static readonly List<object> results=new List<object>();
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true,WriteIndented=true};
    static int failures;
    static Parameters C()=>new Parameters{ suppressCat=true,suppressMother=true,moveSpeed=50,
        beforeActionSeconds=.05f,searchSeconds=.1f,reactionSeconds=.05f,restSeconds=.1f,motherDoorSeconds=.1f,motherAdviceSeconds=.1f };
    static WorldModel M(Parameters c=null,int seed=42)=>new WorldModel(c??C(),seed);
    static void Assert(bool b,string why){if(!b)throw new Exception(why);}
    static void Until(WorldModel m,Func<bool> done,int steps=10000)
    { for(int i=0;i<steps && !done();i++)m.Tick(.05f);Assert(done(),"State did not reach expected endpoint: "+m.S.phase); }
    static void Test(string name,Action test)
    { try{test();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.Message});Console.WriteLine("FAIL "+name+" :: "+e.Message);} }
    static void Main(string[] args)
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
        if(!Directory.Exists(Path.Combine(root,"DoctorPrototype")))root=Path.GetFullPath(Path.Combine(root,".."));
        string project=Path.Combine(root,"DoctorPrototype");
        string resources=Path.Combine(project,"Assets/Doctor/Resources");Directory.CreateDirectory(resources);
        if(args.Contains("--write-config"))File.WriteAllText(Path.Combine(resources,"TestParameters.json"),JsonSerializer.Serialize(new Parameters(),Json));
        Test("Autonomous search across 200 seeds: actual key, 7-10 attempts, no deadlock",()=>{
            for(int seed=1;seed<=200;seed++){var m=M(seed:seed);int planned=m.S.plannedSearches;Until(m,()=>m.S.phase==Phase.Complete);Assert(m.S.searches==planned && planned>=7 && planned<=10 && m.S.hasKey && m.S.doorOpen,"Autonomy / quota / key");}
        });
        Test("Planned ranges change with trapped count, fixed limit remains 10, key relocates",()=>{
            var m=M();int previous=m.S.keyPoint;
            for(int round=2;round<=12;round++){m.NextRound(false);int n=m.S.plannedSearches;Assert(m.S.keyPoint!=previous,"key repeated");previous=m.S.keyPoint;
                Assert(round==2?n>=5&&n<=7:round==3?n>=2&&n<=4:n==1,"bad range");Assert(WorldModel.SearchLimit==10,"quota changed");}
        });
        Test("Strict threshold: equal strength resisted; cost and damage persist; completed action rewarded",()=>{
            var c=C();c.skinInitialResistance=2;var m=M(c);int n=m.S.neurons;Assert(m.Cast(ControlPoint.Skin),"cast failed");
            Assert(!m.S.skinTriggered&&!m.S.interrupted&&m.S.neurons==n-c.skinCost&&m.S.damage.Sum()==c.castDamage,"resisted result");
            Until(m,()=>m.S.searches==1);Assert(m.S.neurons==n-c.skinCost+c.completedActionReward,"separate reward");
        });
        Test("Same-point cumulative breakthrough once; different point stays independent",()=>{
            var m=M();m.S.neurons=20;m.S.gutLevel=1;int effects=0;m.BodyAnomaly+=(p,pos)=>effects++;
            m.Cast(ControlPoint.Skin);Assert(!m.S.interrupted,"first cast not resisted");m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);
            Assert(m.S.skinSum==6 && m.S.gutSum==0,"sums mixed");m.Tick(.01f);Assert(effects==1,"retriggered skin");
            m.Cast(ControlPoint.Gut);m.Cast(ControlPoint.Gut);m.Tick(.01f);Assert(effects==2,"independent gut");
            Until(m,()=>m.S.searches==1);Assert(m.S.neurons==15,"interrupted action got reward");Assert(m.S.skinSum==0&&m.S.gutSum==0,"sum not cleared");
        });
        Test("Interrupted search costs exactly one attempt, searches elsewhere, retries key last",()=>{
            var m=M();m.S.neurons=100;int key=m.S.keyPoint;Until(m,()=>m.S.target==key && m.S.phase==Phase.BeforeSearch);
            int count=m.S.searches; m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);Until(m,()=>m.S.searches>count);
            Assert(m.S.searches==count+1&&!m.S.hasKey&&m.S.retries.Contains(key),"interruption bookkeeping");
            if(m.S.searches<10){Until(m,()=>m.S.phase==Phase.Complete);Assert(m.S.hasKey,"retry did not find actual key");}
        });
        Test("Retry order defers interrupted non-key point until original remaining targets",()=>{
            var m=M();m.S.neurons=10;int first=m.S.target,second=m.S.queue[0];m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);
            Until(m,()=>m.S.phase==Phase.BeforeSearch&&m.S.searches==1);
            Assert(m.S.target==second&&m.S.retries.Contains(first),"did not defer retry");
        });
        Test("Last retry blocked again rests, including fourth trapped round (no forced pass)",()=>{
            var m=M();m.NextRound(false);m.NextRound(false);m.NextRound(false);m.S.neurons=100;
            m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);Until(m,()=>m.S.phase==Phase.BeforeSearch&&m.S.searches==1);
            Assert(m.S.currentIsRetry,"missing retry");m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);
            Until(m,()=>m.S.phase==Phase.RestMoving);Assert(m.S.searches==2&&!m.S.areaUnlocked,"forced pass / bad attempts");
        });
        Test("10-attempt cap under repeated valid interruptions",()=>{
            var m=M(seed:13);m.S.neurons=500;int highest=0,round=m.S.round;
            for(int i=0;i<30000 && m.S.round==round;i++){
                if(m.S.phase==Phase.BeforeSearch&&!m.S.skinTriggered){while(m.S.skinSum<=m.S.skinResistance)m.Cast(ControlPoint.Skin);}
                highest=Math.Max(highest,m.S.searches);m.Tick(.05f);
            }
            Assert(m.S.round==round+1&&highest<=10&&highest>0&&!m.S.hasKey,"quota/rest failure");
        });
        Test("Round growth once per used point, resisted counts, unused unchanged, balances and damage retained",()=>{
            var m=M();m.S.neurons=20;m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);
            float a=m.S.skinResistance,b=m.S.gutResistance,d=m.S.damage.Sum();int n=m.S.neurons,k=m.S.clusters;m.NextRound(false);
            Assert(m.S.skinResistance==a+m.C.resistanceGrowth&&m.S.gutResistance==b,"targeted growth");
            Assert(m.S.neurons==n+1&&m.S.clusters==k+1&&m.S.damage.Sum()==d,"persistence");
            m.Cast(ControlPoint.Skin);a=m.S.skinResistance;m.NextRound(false);Assert(m.S.skinResistance==a+m.C.resistanceGrowth,"resisted memory lost");
        });
        Test("Unlock/upgrade use clusters; abilities and upgrade strength persist",()=>{
            var m=M();Assert(m.Upgrade(ControlPoint.Gut)&&m.S.gutLevel==1&&m.S.clusters==0,"unlock");
            Assert(!m.Upgrade(ControlPoint.Gut),"overspend");m.NextRound(false);float strength=m.Strength(ControlPoint.Gut);
            Assert(m.Upgrade(ControlPoint.Gut)&&m.Strength(ControlPoint.Gut)==strength+m.C.strengthPerLevel,"upgrade");m.NextRound(false);Assert(m.S.gutLevel==2,"lost level");
        });
        Test("Paused simulation freezes all model timers; menu cast defers cat damage until resume",()=>{
            var m=M();m.S.neurons=10;m.S.catPresent=true;m.S.catPosition=m.S.position;m.Paused=true;
            string old=JsonSerializer.Serialize(m.S,Json);for(int i=0;i<100;i++)m.Tick(.1f);Assert(old==JsonSerializer.Serialize(m.S,Json),"paused timers");
            m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);Assert(m.S.health==100&&m.S.catPresent,"NPC damage during paused menu");
            m.Tick(10);Assert(m.S.health==100,"tick advanced while paused");m.Paused=false;m.Tick(.01f);Assert(m.S.health==90&&!m.S.catPresent,"resume consequence missing");
        });
        Test("Fully resisted stimulus damages environment but never alarms cat",()=>{
            var m=M();m.S.catPresent=true;m.S.catPosition=m.S.position;m.Cast(ControlPoint.Skin);m.Tick(.01f);
            Assert(m.S.catPresent&&m.S.health==100&&m.S.mental==100&&m.S.damage.Sum()>0,"resisted stimulus leaked anomaly");
        });
        Test("Cat distance independently produces 10 / 5 / 0 damage or no response",()=>{
            foreach(var pair in new[]{(0f,10f),(2f,5f),(3.5f,0f),(6f,0f)}) {
                var m=M();m.S.neurons=10;m.S.catPresent=true;m.S.catPosition=new Point(m.S.position.x+pair.Item1,m.S.position.z);
                m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);m.Tick(.01f);
                Assert(m.S.health==100-pair.Item2&&m.S.mental==100-pair.Item2,"distance damage");Assert(m.S.catPresent==(pair.Item1>m.C.catNoticeDistance),"cat leaving");
            }
        });
        Test("Cat normal block makes fourth-round single search into two; no fright",()=>{
            var c=C();c.suppressCat=false;c.forceCatAtRoundStart=true;var m=M(c);m.NextRound(false);m.NextRound(false);m.NextRound(false);
            Until(m,()=>m.S.searches==1);Assert(m.S.catPresent&&!m.S.hasKey&&m.S.health==100,"cat ordinary block");
            Until(m,()=>m.S.phase==Phase.Complete);Assert(m.S.searches==2&&m.S.health==100,"cat retry failed");
        });
        Test("Cat→health/mental→mother entry→advice arises from independent rules",()=>{
            var c=C();c.suppressMother=false;c.forceMotherAtBoundary=true;c.motherFullMentalEntryChance=0;
            var m=M(c);m.S.neurons=10;m.S.catPresent=true;m.S.catPosition=m.S.position;
            m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);Until(m,()=>m.S.phase==Phase.MotherAdvice);
            Assert(m.S.health==90&&m.S.mental==90&&m.S.doorOpen,"chain did not affect decision");
            var far=M(c);far.S.neurons=10;far.S.catPresent=true;far.S.catPosition=new Point(far.S.position.x+3.5f,far.S.position.z);
            far.Cast(ControlPoint.Skin);far.Cast(ControlPoint.Skin);Until(far,()=>far.S.motherVisited&&!far.S.motherPresent);
            Assert(far.S.mental==100&&!far.S.doorOpen,"far attack improperly changed mother decision");
        });
        Test("Effective gut refuses mother rest, resisted gut accepts; entering already unlocks door",()=>{
            foreach(bool effective in new[]{false,true}){
                var c=C();c.suppressMother=false;c.forceMotherAtBoundary=true;var m=M(c);m.S.mental=90;m.S.gutLevel=1;m.S.neurons=20;
                Until(m,()=>m.S.phase==Phase.MotherAdvice);Assert(m.S.doorOpen,"door not open at entry");int round=m.S.round;
                m.Cast(ControlPoint.Gut);if(effective)m.Cast(ControlPoint.Gut);
                if(effective){Until(m,()=>m.S.phase==Phase.Complete);Assert(m.S.round==round,"refusal rested");}
                else {int n=m.S.neurons,k=m.S.clusters;Until(m,()=>m.S.round==round+1);Assert(m.S.neurons==n+2+c.completedActionReward&&m.S.clusters==k+2,"mother rewards wrong");}
            }
        });
        Test("Mother with present cat: special overrides normal porridge and chocolate",()=>{
            var c=C();c.suppressMother=false;c.forceMotherAtBoundary=true;var m=M(c);m.S.catPresent=true;m.S.mental=90;
            Until(m,()=>m.S.doorOpen);Assert(!m.S.catPresent&&m.S.lollipop&&!m.S.chocolate&&!m.S.motherBonus&&m.S.phase!=Phase.MotherAdvice,"special stacked rewards");
        });
        Test("Low mental rest can be interrupted once; later search loses mental; targeted extra growth",()=>{
            var m=M();m.S.neurons=100;m.S.mental=45;Until(m,()=>m.S.phase==Phase.RestMoving);
            Assert(m.S.lowRestUsed,"rest opportunity missing");int count=m.S.searches;float mental=m.S.mental;
            m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);m.Tick(.01f);Assert(m.S.restInterrupted&&m.S.searches==count,"rest counts as search");
            Until(m,()=>m.S.searches==count+1);Assert(m.S.mental==mental-m.C.interruptedRestMentalLoss,"post rest penalty");
            Until(m,()=>m.S.phase==Phase.BeforeSearch || m.S.phase==Phase.Exiting);Assert(m.S.lowRestUsed,"repeated rest");
            float before=m.S.skinResistance;m.NextRound(false);Assert(m.S.skinResistance==before+m.C.resistanceGrowth+m.C.interruptedRestExtraGrowth,"test extra growth");
        });
        Test("Mental lowest band priority: <=0 Street; <20 numb risk; death wins at simultaneous zero",()=>{
            var m=M();m.S.mental=0;m.Tick(.05f);Assert(m.S.phase==Phase.Street,"zero mental not Street");
            m=M();m.S.mental=19;m.C.faintRiskPerSecond=0;m.Tick(.05f);Assert(m.S.phase!=Phase.RestMoving&&m.S.phase!=Phase.Street,"numb band");
            m=M();m.S.health=0;m.S.mental=0;m.Tick(.05f);Assert(m.S.round==4&&m.S.phase!=Phase.Street,"death priority");
        });
        Test("Death round2→5: both counters +3, resources only actual arrival, growth once",()=>{
            var m=M();m.NextRound(false);m.S.neurons=10;m.Cast(ControlPoint.Skin);float r=m.S.skinResistance;int n=m.S.neurons,k=m.S.clusters;m.S.health=0;m.Tick(.01f);
            Assert(m.S.round==5&&m.S.trapped==5,"death delta");Assert(m.S.neurons==n+1&&m.S.clusters==k+1&&m.S.skinResistance==r+m.C.resistanceGrowth,"skipped round phantom reward");
        });
        Test("Numb faint enters next actual round, paused risk unchanged",()=>{
            var m=M();m.S.mental=10;m.C.faintRiskPerSecond=1;m.C.faintCheckSeconds=.1f;m.Paused=true;m.Tick(3);Assert(m.S.risk==0,"paused risk");m.Paused=false;Until(m,()=>m.S.round==2);Assert(m.S.health==100,"faint health");
        });
        Test("Awakening one per round and never replays after death stage skip; no global bonus",()=>{
            var m=M();float r=m.S.skinResistance;m.NextRound(true);Assert(m.S.awakeningPlayed.Count(x=>x)==1&&m.S.skinResistance==r,"awakening policy");
            m.NextRound(false);Assert(m.S.awakeningPlayed.Count(x=>x)==2,"duplicate awakening");
        });
        Test("Save/reload mid-action preserves queued reactions, search progress, and RNG without free resources",()=>{
            var m=M();m.S.neurons=8;m.Cast(ControlPoint.Skin);m.Cast(ControlPoint.Skin);m.Paused=true;
            var saved=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(m.S,Json),Json);var clone=new WorldModel(m.C,999,saved);
            Assert(clone.S.neurons==m.S.neurons&&clone.S.pendingSkinAnomaly&&clone.S.skinSum==m.S.skinSum,"snapshot changed");
            m.Paused=false;for(int i=0;i<100;i++){m.Tick(.05f);clone.Tick(.05f);}Assert(JsonSerializer.Serialize(m.S,Json)==JsonSerializer.Serialize(clone.S,Json),"resumed divergent");
        });
        Test("Default tuning with real NPC probabilities across 100 seeds reaches scene exit",()=>{
            for(int seed=1;seed<=100;seed++){
                var m=new WorldModel(new Parameters(),seed);Until(m,()=>m.S.phase==Phase.Complete,30000);
                Assert(m.S.searches<=10&&m.S.neurons>=0&&m.S.clusters>=0,"default integration budget");
            }
        });
        Test("Insufficient balance and locked-point casts do not mutate costs, memory, damage, or strength",()=>{
            var m=M();float d=m.S.damage.Sum();int n=m.S.neurons;
            Assert(!m.Cast(ControlPoint.Gut)&&m.S.neurons==n&&!m.S.gutUsed&&m.S.damage.Sum()==d,"locked cast mutated");
            m.S.neurons=0;Assert(!m.Cast(ControlPoint.Skin)&&!m.S.skinUsed&&m.S.skinSum==0&&m.S.damage.Sum()==d,"zero-balance cast mutated");
        });
        Directory.CreateDirectory(Path.Combine(root,"logs"));
        File.WriteAllText(Path.Combine(root,"logs/core-checks.json"),JsonSerializer.Serialize(new{framework=".NET 9, pure rules only; not Unity runtime",passed=results.Count-failures,failed=failures,results},Json));
        Console.WriteLine($"RESULT: {results.Count-failures} passed, {failures} failed. Unity graphics/physics not covered.");
        Environment.ExitCode=failures==0?0:1;
    }
}
