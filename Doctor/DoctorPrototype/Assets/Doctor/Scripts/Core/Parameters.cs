using System;

namespace Doctor
{
    // All balancing values here are PROTOTYPE TEST PARAMETERS, not approved final values.
    [Serializable]
    public sealed class Parameters
    {
        public string parameterStatus = "测试参数，不是最终数值；文档确定的10次额度、每实际周目1+1和死亡+3不在此改写";
        public int startingNeurons = 0, startingClusters = 0;
        public int skinCost = 1, gutCost = 1, unlockCost = 1, upgradeCost = 1;
        public float skinStrength = 2, gutStrength = 2, strengthPerLevel = 1;
        public float skinInitialResistance = 3, gutInitialResistance = 3, resistanceGrowth = 1;
        public int completedActionReward = 1;
        public float moveSpeed = 1.65f, numbSpeedMultiplier = .45f;
        public float beforeActionSeconds = 2.5f, searchSeconds = 3.5f, reactionSeconds = 1.4f;
        public float restSeconds = 5, motherDoorSeconds = 4, motherAdviceSeconds = 5;
        public float healthRecovery = 30, mentalRecovery = 40;
        public float interruptedRestMentalLoss = 5;
        // Undecided meaning of "double": explicit test policy, not a claimed accepted revision.
        public string interruptedRestPolicy = "TEST_AddExtraGrowth_NotDoubleExisting";
        public float interruptedRestExtraGrowth = 1;
        public float faintRiskPerSecond = .05f, faintCheckSeconds = 1;
        public float catChance = .35f, motherChancePerAction = .12f;
        public float appearancePerTrappedRound = .035f, appearancePerCast = .006f, appearanceCap = .75f;
        public float catNoticeDistance = 4.5f, catNearDistance = 1.45f, catMediumDistance = 2.8f;
        public float catNearDamage = 10, catMediumDamage = 5;
        public bool catRespondsToSkin = true, catRespondsToGut = true;
        public float motherFullMentalEntryChance = .5f, motherRestGlobalResistance = 1;
        public float lollipopMentalRecovery = 10;
        public float searchDamage = 1, castDamage = 1;
        public float stainScalePerDamage = .025f, maxStainScale = .6f;
        public float baseLightIntensity = 1.25f, lightLossPerDamage = .006f, minimumLightIntensity = .45f;
        public float fragmentImpulse = 1.1f, fragmentLifetime = 4;
        public int maxFragments = 18;
        public float catDepartureSeconds = 1.5f;
        public int randomSeed = 0;
        public bool forceCatAtRoundStart = false, suppressCat = false;
        public bool forceMotherAtBoundary = false, suppressMother = false;
    }

    [Serializable]
    public struct Point
    {
        public float x, z;
        public Point(float x, float z) { this.x = x; this.z = z; }
        public static float Distance(Point a, Point b) { float x = a.x-b.x, z=a.z-b.z; return (float)Math.Sqrt(x*x+z*z); }
        public static Point Toward(Point a, Point b, float d)
        {
            float len=Distance(a,b); if(len<=d || len<.0001f) return b;
            return new Point(a.x+(b.x-a.x)*d/len,a.z+(b.z-a.z)*d/len);
        }
    }
    public enum Phase { BeforeSearch, Moving, Searching, Reaction, Boundary, MotherDoor, MotherAdvice, RestMoving, Resting, Exiting, Complete, Street }
    public enum ControlPoint { Skin, Gut }
    public enum RestReason { Exhausted, LowMental, Mother }

    [Serializable]
    public sealed class WorldState
    {
        public int schema = 1, round = 1, trapped = 1, neurons, clusters, searches, plannedSearches;
        public int actionSerial = 1, target = -1, keyPoint = -1, previousKey = -1, totalCasts;
        public int skinLevel = 1, gutLevel;
        public float skinResistance, gutResistance, skinSum, gutSum;
        public bool skinTriggered, gutTriggered, skinUsed, gutUsed, skinRestPenalty, gutRestPenalty;
        public bool pendingSkinAnomaly, pendingGutAnomaly;
        public Point skinAnomalyOrigin, gutAnomalyOrigin;
        public bool interrupted, currentIsRetry, lowRestUsed, restInterrupted, motherBonus;
        public bool catPresent, catLeaveBeforeNext, motherPresent, motherVisited, motherRestRefused;
        public bool doorOpen, hasKey, areaUnlocked, chocolate, lollipop, keyRevealed;
        public float health = 100, mental = 100, timer, risk, faintClock, worldSeconds;
        public Point position = new Point(-4, -1), catPosition, motherPosition = new Point(4.6f, 2.6f);
        public Phase phase;
        public RestReason restReason;
        public uint rng;
        public int[] queue = new int[0], retries = new int[0];
        public int routeStep;
        public Point[] route = new Point[0];
        public float[] damage = new float[10];
        public bool[] awakeningPlayed = new bool[5];
        public bool awakeningThisRound;
        public string speech = "钥匙在哪……我得出去。";
        public string[] journal = new string[0];
    }
}
