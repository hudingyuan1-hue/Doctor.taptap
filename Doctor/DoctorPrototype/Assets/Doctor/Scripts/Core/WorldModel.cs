using System;
using System.Collections.Generic;

namespace Doctor
{
    // Deterministic rules, no MonoBehaviour, rendering, or story-combo dependency.
    public sealed class WorldModel
    {
        public const int SearchLimit = 10;
        public static readonly Point Bed = new Point(-4, -1);
        public static readonly Point Exit = new Point(4.85f, 2.6f);
        // Reachable interaction anchors in the open aisles; the furniture itself never moves.
        public static readonly Point[] Spots = {
            new Point(-4,1.9f),new Point(-2,1.9f),new Point(0,1.9f),new Point(2,1.9f),
            new Point(-4,-1.9f),new Point(-2,-1.9f),new Point(0,-1.9f),new Point(2,-1.9f),
            new Point(4,-1.9f),new Point(4,1.9f)};
        public static readonly string[] SpotNames = {"床头柜","衣柜","书桌","书架","床侧","旧箱子","矮柜","抽屉","储物箱","门边柜"};
        public readonly Parameters C;
        public WorldState S;
        public bool Paused { get; set; }
        public event Action<ControlPoint, Point> BodyAnomaly;
        public event Action<int, float> EnvironmentDamaged;
        public event Action<string> Changed;

        public WorldModel(Parameters config, int seed, WorldState saved = null)
        {
            C=config;
            BodyAnomaly += CatHearsAnomaly;
            if(saved != null) { S=saved; return; }
            S=new WorldState { neurons=C.startingNeurons, clusters=C.startingClusters,
                skinResistance=C.skinInitialResistance, gutResistance=C.gutInitialResistance,
                rng=(uint)(seed==0 ? 1777 : seed) };
            StartRound(false);
        }
        float Random01() { uint x=S.rng; x^=x<<13; x^=x>>17; x^=x<<5; S.rng=x; return (x & 0x00ffffff)/16777216f; }
        int Range(int min,int max) { return min+(int)(Random01()*(max-min)); }
        public void Log(string text)
        {
            var a=new List<string>(S.journal); a.Add($"[{S.round}周目 / {S.worldSeconds:F1}s] {text}");
            if(a.Count>90) a.RemoveAt(0); S.journal=a.ToArray(); Changed?.Invoke(text);
        }
        public int Level(ControlPoint p) => p==ControlPoint.Skin ? S.skinLevel:S.gutLevel;
        public int Cost(ControlPoint p) => p==ControlPoint.Skin ? C.skinCost:C.gutCost;
        public float Strength(ControlPoint p) => (p==ControlPoint.Skin ? C.skinStrength:C.gutStrength)+Math.Max(0,Level(p)-1)*C.strengthPerLevel;
        public float Resistance(ControlPoint p) => p==ControlPoint.Skin ? S.skinResistance:S.gutResistance;
        public float Sum(ControlPoint p) => p==ControlPoint.Skin ? S.skinSum:S.gutSum;
        public bool Triggered(ControlPoint p) => p==ControlPoint.Skin ? S.skinTriggered:S.gutTriggered;
        public bool CanCast => S.phase==Phase.BeforeSearch || S.phase==Phase.Moving || S.phase==Phase.Searching ||
            S.phase==Phase.MotherDoor || S.phase==Phase.MotherAdvice || S.phase==Phase.RestMoving || S.phase==Phase.Resting;
        public string ActionLabel => S.phase==Phase.MotherAdvice ? "母亲劝休息的决定" : S.phase==Phase.MotherDoor ? "门口交谈（不占搜索次数）" :
            S.phase==Phase.RestMoving || S.phase==Phase.Resting ? "本次休息（不占搜索次数）" :
            CanCast ? $"第 {S.searches+1} 次搜索 · {(S.target>=0?SpotNames[S.target]:"准备目标")}" : "行动结算中，等待下一操作阶段";
        public bool Upgrade(ControlPoint p)
        {
            int cost=Level(p)==0 ? C.unlockCost:C.upgradeCost;
            if(cost<0 || S.clusters<cost) { Log("神经簇不足。未购买。"); return false; }
            S.clusters-=cost;
            if(p==ControlPoint.Skin) S.skinLevel++; else S.gutLevel++;
            Log($"{Name(p)}解锁/升级至 {Level(p)}，神经簇 -{cost}"); return true;
        }
        public static string Name(ControlPoint p) => p==ControlPoint.Skin ? "皮肤触觉":"肠胃蠕动";
        public bool Cast(ControlPoint p)
        {
            if(!CanCast || Level(p)<=0 || Cost(p)<0 || S.neurons<Cost(p)) { Log("当前不能施放，或资源不足 / 操控点未解锁。"); return false; }
            S.neurons-=Cost(p); S.totalCasts++;
            if(p==ControlPoint.Skin) { S.skinUsed=true; S.skinSum+=Strength(p); }
            else { S.gutUsed=true; S.gutSum+=Strength(p); }
            DamageEnvironment(NearestSpot(S.position),C.castDamage);
            Log($"{Name(p)}：神经元 -{Cost(p)}；本行动累计 {Sum(p):0.##} / 抗性 {Resistance(p):0.##}");
            if(Triggered(p)) { S.speech="身体已经作出反应……"; Log("此点本行动已触发，不重复身体效果。"); return true; }
            if(Sum(p)<=Resistance(p)) { S.speech="忍一忍……我还能继续。"; Log("被抵抗：不打断，不退款，不发身体异常事件。"); return true; }
            if(p==ControlPoint.Skin) S.skinTriggered=true; else S.gutTriggered=true;
            S.speech=p==ControlPoint.Skin ? "嘶……皮肤好难受。":"胃里翻腾……不舒服。";
            if(S.phase==Phase.MotherAdvice) { if(p==ControlPoint.Gut) S.motherRestRefused=true; }
            else if(S.phase==Phase.RestMoving || S.phase==Phase.Resting)
            {
                S.interrupted=true;
                if(S.restReason==RestReason.LowMental) {
                    S.restInterrupted=true;
                    if(p==ControlPoint.Skin) S.skinRestPenalty=true; else S.gutRestPenalty=true;
                }
            }
            else if(S.phase!=Phase.MotherDoor) S.interrupted=true;
            Log($"{Name(p)}身体效果生效；影响：{ActionLabel}");
            // Menu transactions record intent. NPC reactions and damage wait for world time to resume.
            if(p==ControlPoint.Skin){S.pendingSkinAnomaly=true;S.skinAnomalyOrigin=S.position;}
            else {S.pendingGutAnomaly=true;S.gutAnomalyOrigin=S.position;}
            return true;
        }
        public void DamageEnvironment(int id,float amount)
        {
            S.damage[id]+=amount; EnvironmentDamaged?.Invoke(id,amount);
        }
        static int NearestSpot(Point pos)
        {
            int index=0; float best=float.MaxValue;
            for(int i=0;i<Spots.Length;i++) { float d=Point.Distance(pos,Spots[i]); if(d<best){best=d;index=i;} } return index;
        }
        void CatHearsAnomaly(ControlPoint p,Point origin)
        {
            if(!S.catPresent || (p==ControlPoint.Skin && !C.catRespondsToSkin) || (p==ControlPoint.Gut && !C.catRespondsToGut)) return;
            float distance=Point.Distance(origin,S.catPosition);
            if(distance>C.catNoticeDistance) { Log($"猫未响应：距离 {distance:F1} 超出感知范围。"); return; }
            float amount=distance<=C.catNearDistance ? C.catNearDamage : distance<=C.catMediumDistance ? C.catMediumDamage:0;
            S.health=Math.Max(0,S.health-amount); S.mental-=amount;
            S.catPresent=false; S.catLeaveBeforeNext=false;S.keyRevealed=true;
            Log($"猫独立响应身体异常，距离 {distance:F1}；健康 -{amount}，精神 -{amount}；离开。");
            if(amount>0) S.speech=amount>=C.catNearDamage ? "猫扑过来了！……好痛。":"手臂被抓伤了……";
        }
        void ClearAction()
        {
            S.skinSum=0; S.gutSum=0; S.skinTriggered=false; S.gutTriggered=false; S.interrupted=false; S.actionSerial++;
        }
        void StartRound(bool motherBonus)
        {
            S.neurons++; S.clusters++;
            if(motherBonus) { S.neurons++; S.clusters++; }
            S.searches=0; S.skinUsed=S.gutUsed=S.skinRestPenalty=S.gutRestPenalty=false;
            S.pendingSkinAnomaly=S.pendingGutAnomaly=false;S.keyRevealed=S.trapped>=4;
            S.lowRestUsed=S.restInterrupted=S.motherBonus=false;
            S.motherPresent=S.motherVisited=S.motherRestRefused=false; S.catLeaveBeforeNext=false;
            S.risk=S.faintClock=0; S.position=Bed; S.awakeningThisRound=false;
            ClearAction();
            Log($"实际进入周目 {S.round} / 本场景受困 {S.trapped}：系统 +1 神经元、+1 神经簇"+(motherBonus?"；母亲额外 +1、+1":""));
            if(S.areaUnlocked) { S.catPresent=false; BeginExit(); return; }
            S.hasKey=false; S.doorOpen=false;
            S.plannedSearches=S.trapped==1?Range(7,11):S.trapped==2?Range(5,8):S.trapped==3?Range(2,5):1;
            var ids=new List<int>(); for(int i=0;i<10;i++) ids.Add(i);
            for(int i=9;i>0;i--) { int j=Range(0,i+1); int t=ids[i];ids[i]=ids[j];ids[j]=t; }
            int keyIndex=S.plannedSearches-1;
            if(ids[keyIndex]==S.previousKey) { int j=(keyIndex+1)%10; int t=ids[keyIndex];ids[keyIndex]=ids[j];ids[j]=t; }
            S.keyPoint=ids[keyIndex]; S.previousKey=S.keyPoint;
            S.queue=ids.GetRange(0,S.plannedSearches).ToArray(); S.retries=new int[0];
            S.catPresent=!C.suppressCat && (C.forceCatAtRoundStart || Random01()<Appearance(C.catChance));
            S.catPosition=Spots[S.keyPoint];
            S.speech="钥匙在哪……我得出去。";
            SelectNextSearch();
        }
        float Appearance(float baseChance) => Math.Min(C.appearanceCap,baseChance+(S.trapped-1)*C.appearancePerTrappedRound+S.totalCasts*C.appearancePerCast);
        public void NextRound(bool death)
        {
            bool bonus=S.motherBonus && !death;
            if(S.skinUsed) S.skinResistance+=C.resistanceGrowth+(S.skinRestPenalty?C.interruptedRestExtraGrowth:0);
            if(S.gutUsed) S.gutResistance+=C.resistanceGrowth+(S.gutRestPenalty?C.interruptedRestExtraGrowth:0);
            Log($"针对性成长仅结算一次：皮肤 {S.skinResistance:0.##}；肠胃 {S.gutResistance:0.##}");
            S.round+=death?3:1; S.trapped+=death?3:1;
            if(death) { S.health=100;S.mental=100;Log("死亡：到达后第3周目；跳过两轮不发资源、不补成长。"); }
            else { S.health=Math.Min(100,S.health+C.healthRecovery);S.mental=Math.Min(100,S.mental+C.mentalRecovery); }
            StartRound(bonus);
        }
        bool CheckVital()
        {
            if(S.health<=0) { NextRound(true); return true; }
            if(S.mental<=0) { S.phase=Phase.Street; S.speech="Street 事件出口 · 后续玩法暂缓"; return true; }
            return false;
        }
        void SelectNextSearch()
        {
            if(S.doorOpen || S.hasKey) { BeginExit();return; }
            if(S.searches>=SearchLimit) { BeginRest(RestReason.Exhausted);return; }
            if(S.catLeaveBeforeNext) { S.catPresent=false;S.catLeaveBeforeNext=false;S.keyRevealed=true;Log("猫在下一角色行动前离开，显露钥匙。"); }
            if(S.queue.Length>0) { S.target=Pop(ref S.queue);S.currentIsRetry=false; }
            else if(S.retries.Length>0) { S.target=Pop(ref S.retries);S.currentIsRetry=true; }
            else { BeginRest(RestReason.Exhausted);return; }
            ClearAction(); S.phase=Phase.BeforeSearch; S.timer=C.beforeActionSeconds;
            S.speech=$"去{SpotNames[S.target]}看看。";
            if(!S.awakeningThisRound) {
                for(int i=0;i<S.awakeningPlayed.Length;i++) if(S.trapped>=i+2 && !S.awakeningPlayed[i]) {
                    string[] lines={"奇怪……我怎么……","我的身体出现了……一些状况？","似乎……我生病了。","饶了我吧……","是你吗？是你搞的鬼吗？"};
                    S.awakeningPlayed[i]=true;S.awakeningThisRound=true;S.speech=lines[i];Log("觉醒对白（全剧一次）："+S.speech);break;
                }
            }
        }
        static int Pop(ref int[] queue) { int first=queue[0];var l=new List<int>(queue);l.RemoveAt(0);queue=l.ToArray();return first; }
        void Retry(int id) { var l=new List<int>(S.retries); if(!l.Contains(id)) l.Add(id); S.retries=l.ToArray(); }
        void CompleteSearch()
        {
            S.searches++; DamageEnvironment(S.target,C.searchDamage);
            bool catBlocks=!S.interrupted && S.target==S.keyPoint && S.catPresent;
            bool finalRetryBlocked=S.interrupted && S.currentIsRetry && S.queue.Length==0 && S.retries.Length==0;
            if(S.interrupted) { Retry(S.target);Log($"第 {S.searches}/10 次搜索被打断，不奖励；安排末尾重试。"); }
            else {
                Reward("搜索完成");
                if(catBlocks) { Retry(S.target);S.catLeaveBeforeNext=true;S.speech="钥匙在猫下面……先看看别处。";Log("猫普通阻钥匙：消耗一次搜索，猫下行动前离开。"); }
                else if(S.target==S.keyPoint) { S.hasKey=true;S.speech="找到了。去开门。";Log("取得实际钥匙，下一步开门不消耗搜索额度。"); }
                else S.speech="这里没有……";
            }
            if(S.lollipop) { S.lollipop=false;S.mental=Math.Min(100,S.mental+C.lollipopMentalRecovery);Log("行动时使用棒棒糖恢复精神。"); }
            if(S.restInterrupted) S.mental-=C.interruptedRestMentalLoss;
            ClearAction();
            if(CheckVital())return;
            if(finalRetryBlocked) { Log("最后重试位置再次被有效阻止，开始休息。");BeginRest(RestReason.Exhausted);return; }
            S.phase=Phase.Boundary;S.timer=C.reactionSeconds;
        }
        void Reward(string why) { S.neurons+=C.completedActionReward;Log($"{why}：神经元 +{C.completedActionReward}（与施放扣费独立）"); }
        void Boundary()
        {
            if(!S.motherVisited && !C.suppressMother && (C.forceMotherAtBoundary || Random01()<Appearance(C.motherChancePerAction))) {
                S.motherVisited=true; S.motherPresent=true; ClearAction(); S.phase=Phase.MotherDoor; S.timer=C.motherDoorSeconds;
                S.speech="母亲：我可以进来吗？";Log("母亲在行动边界出现，暂停角色原流程。");return;
            }
            ContinueAfterPerson();
        }
        void ContinueAfterPerson()
        {
            if(CheckVital())return;
            if(S.mental<50 && S.mental>=20 && !S.lowRestUsed) { BeginRest(RestReason.LowMental);return; }
            SelectNextSearch();
        }
        void MotherDecision()
        {
            // Special animal encounter overrides normal gifts/advice, not additive.
            if(S.catPresent) {
                S.catPresent=false;S.catLeaveBeforeNext=false;OpenDoor("母亲驱逐猫并进入");S.lollipop=true;
                Log("母亲与猫特殊规则：只给棒棒糖，不叠加粥/巧克力/额外神经簇。");
                S.motherPresent=false;ClearAction();ContinueAfterPerson();return;
            }
            if(S.mental<100) {
                OpenDoor("母亲因精神低于100进入"); ClearAction();S.phase=Phase.MotherAdvice;S.timer=C.motherAdviceSeconds;
                S.motherRestRefused=false;S.speech="母亲：喝点粥，好好休息。";Log("默认接受劝休息；本次决定前有效肠胃操控可拒绝。");return;
            }
            if(Random01()<C.motherFullMentalEntryChance) {
                OpenDoor("母亲询问后进入");S.chocolate=true;S.clusters++;Log("母亲给予巧克力，神经簇 +1。");
            } else Log("精神100，母亲未进入。");
            S.motherPresent=false;ClearAction();ContinueAfterPerson();
        }
        void OpenDoor(string why) { S.doorOpen=true;S.areaUnlocked=true;Log(why+"；房门已打开，本区域永久解锁。"); }
        void ResolveAdvice()
        {
            S.motherPresent=false;
            if(S.motherRestRefused) { Log("有效肠胃操控：拒绝休息，继续前进。");ClearAction();BeginExit(); }
            else {
                S.skinResistance+=C.motherRestGlobalResistance;S.gutResistance+=C.motherRestGlobalResistance;
                S.motherBonus=true;Log("接受母亲劝休息：原文全局抗性奖励单独结算；额外1+1在下一实际周目发放。");
                BeginRest(RestReason.Mother);
            }
        }
        void BeginRest(RestReason reason)
        {
            if(CheckVital())return;
            ClearAction();S.restReason=reason;if(reason==RestReason.LowMental)S.lowRestUsed=true;
            S.phase=Phase.RestMoving;SetRoute(Bed);S.speech="回床上……休息一下。";Log($"开始{reason}休息，不消耗搜索次数。");
        }
        void BeginExit()
        {
            ClearAction(); S.phase=Phase.Exiting;SetRoute(Exit);S.speech="门开了。出去看看。";
        }
        void SetRoute(Point goal)
        {
            // Three orthogonal segments keep movement in the authored, obstacle-free central aisle.
            S.route=new[]{new Point(S.position.x,0),new Point(goal.x,0),goal};S.routeStep=0;
        }
        bool Move(float dt)
        {
            if(S.routeStep>=S.route.Length)return true;
            float speed=C.moveSpeed*(S.mental<20?C.numbSpeedMultiplier:1);
            S.position=Point.Toward(S.position,S.route[S.routeStep],speed*dt);
            if(Point.Distance(S.position,S.route[S.routeStep])<.001f)S.routeStep++;
            return S.routeStep>=S.route.Length;
        }
        public void Tick(float dt)
        {
            if(Paused || dt<=0 || S.phase==Phase.Complete || S.phase==Phase.Street)return;
            S.worldSeconds+=dt;
            if(S.pendingSkinAnomaly){S.pendingSkinAnomaly=false;BodyAnomaly?.Invoke(ControlPoint.Skin,S.skinAnomalyOrigin);}
            if(S.pendingGutAnomaly){S.pendingGutAnomaly=false;BodyAnomaly?.Invoke(ControlPoint.Gut,S.gutAnomalyOrigin);}
            if(CheckVital())return;
            if(S.mental<50 && S.chocolate && S.phase!=Phase.Resting) { S.mental=100;S.chocolate=false;Log("未到床位，自动使用巧克力恢复满精神。"); }
            if(S.mental<20) {
                S.risk=Math.Min(1,S.risk+C.faintRiskPerSecond*dt);S.faintClock+=dt;
                if(S.faintClock>=C.faintCheckSeconds) { S.faintClock=0;if(Random01()<S.risk){Log("低精神麻木，风险检定晕倒。");NextRound(false);return;} }
            }
            if(S.phase==Phase.RestMoving || S.phase==Phase.Resting) {
                if(S.interrupted) {
                    Log("休息被打断；搜索额度不变。");
                    if(S.restReason==RestReason.Mother) S.motherBonus=false;
                    ClearAction();
                    if(S.doorOpen)BeginExit();
                    else if(S.searches>=10 || (S.queue.Length==0 && S.retries.Length==0)) { S.phase=Phase.Resting;S.timer=C.restSeconds;S.speech="已经找遍了……只能休息。"; }
                    else SelectNextSearch();
                    return;
                }
            }
            S.timer-=dt;
            switch(S.phase) {
                case Phase.BeforeSearch: if(S.timer<=0){S.phase=Phase.Moving;SetRoute(Spots[S.target]);}break;
                case Phase.Moving: if(Move(dt)){S.phase=Phase.Searching;S.timer=C.searchSeconds;}break;
                case Phase.Searching: if(S.interrupted || S.timer<=0) { if(S.interrupted){S.phase=Phase.Reaction;S.timer=C.reactionSeconds;}else CompleteSearch(); }break;
                case Phase.Reaction: if(S.timer<=0)CompleteSearch();break;
                case Phase.Boundary: if(S.timer<=0)Boundary();break;
                case Phase.MotherDoor: if(S.timer<=0)MotherDecision();break;
                case Phase.MotherAdvice: if(S.timer<=0)ResolveAdvice();break;
                case Phase.RestMoving: if(Move(dt)){S.phase=Phase.Resting;S.timer=C.restSeconds;}break;
                case Phase.Resting: if(S.timer<=0){Reward("休息完成");NextRound(false);}break;
                case Phase.Exiting: if(Move(dt)) {
                    if(!S.doorOpen)OpenDoor("角色用钥匙开门");
                    Reward("开门并离开完成");S.phase=Phase.Complete;S.speech="第一场景已离开 · 后续楼层暂缓";
                }break;
            }
        }
    }
}
