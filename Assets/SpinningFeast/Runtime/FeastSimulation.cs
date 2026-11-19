using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SpinningFeast
{
    public static class ArenaGeometry
    {
        public const float TableScale=20f/7.6f;
        public const float TableRadius=20f, TurntableRadius=4f*TableScale;
        public const float IngredientInner=4.45f*TableScale, IngredientOuter=TableRadius+1.4f;
        public const float FoodModelScale=1.5f;
        public const float IngredientMinSize=.42f*FoodModelScale*1.5f, IngredientMaxSize=.58f*FoodModelScale*1.5f;
        public const float CameraHalfHeight=15.5f, BounceSphereRadius=40f, InitialScatterOuter=37f;
        public static float SpillEdge(float scale)=>math.min(TableRadius+.25f,TableRadius+scale*.15f);
        public static float SpillFloor(float seed)=>-1.16f+math.frac(seed*.618034f)*.42f;
    }
    public enum MatchPhase { Title, Countdown, Playing, Result }
    public struct MatchState : IComponentData
    {
        public MatchPhase Phase;
        public float Remaining, Elapsed, Angle, CountdownRemaining;
        public int CyanScore, OrangeScore, Epoch, LastCyan, LastOrange;
    }
    public struct CookCommand : IBufferElementData { public int Player, Recipe, Sequence, Epoch; }
    public struct CookedDish : IBufferElementData { public int Player, Recipe, Count, BonusPoints, EatenCount, EatenPoints; }
    public struct Stock : IComponentData { public FoodKind Kind; public int Quantity, GoldenQuantity; }
    public struct Plate : IComponentData { public int Recipe, Slot, Incoming, LastPlayer; public float Pulse, ServedAt; }
    public struct Serving : IComponentData { public Entity Target; public int Recipe, Count, Player, Flags, SourceSlot, BonusPoints; public float ReadyAt; }
    public struct CookCue
    {
        public int Player, Recipe, Count, Slot, SourceSlot, Flags, BonusPoints;
        public bool Discovery => (Flags & 1) != 0;
        public bool Chain => (Flags & 2) != 0;
        public bool NewPlatter => (Flags & 4) != 0;
    }
    public struct EatingCue { public int Player, Recipe, Count, Slot, Points; }
    public struct Visual : IComponentData
    {
        public FoodKind Kind;
        public float3 Position, Start;
        public float2 Drift;
        public float Scatter, ScatterCooldown, GoldLaunch;
        public float Velocity, Floor, Scale, Life, Duration, Seed,Impact,LandedAt,OrbitAngle;
        public int EffectSlot, EffectCount, TargetSlot;
        public int Golden;
        public int Mode, Player, Recipe; // 0: stock, 1: serving flight, 2: expired, 3: missing hologram, 4: eaten-dish shower
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FeastSystem : SystemBase
    {
        public const float FlightTime = .85f;
        public const float EatingMinAge = 10f;
        public const float GoldenChance = 1f/650f, GoldenScale = 3f;
        public const int GoldenScoreMultiplier = 3;
        public const float RotationSpeed = .12f;
        Entity match;
        EntityQuery stocks, visuals, servings, plates, owned;
        Unity.Mathematics.Random random;
        float rain,nextEatAt;
        int openingTotal,openingSpawned;
        int nextEpoch;
        public bool RainEnabled = true;
        public bool AutoEatEnabled = true;
        public bool SaveDiscoveries = true;
        public bool IsPaused { get; private set; }
        public void SetPaused(bool paused)
        {
            if(IsPaused==paused)return;
            // Finish the last simulated step, not a future one. Other Unity
            // systems, UI and input keep their ordinary clock while paused.
            if(paused)Dependency.Complete();
            IsPaused=paused;
        }
        public readonly bool[] Discovered = new bool[Cookbook.All.Length];
        public int SuccessfulCommands { get; private set; }
        public int RejectedCommands { get; private set; }
        public event System.Action<int,int,int> Cooked;
        public event System.Action<int> Failed;
        public event System.Action<int,int,int> Served;
        public event System.Action<CookCue> CookingCue;
        public event System.Action<CookCue> ArrivalCue;
        public event System.Action<EatingCue> Eating;
        public Entity MatchEntity => match;
        public MatchState State => EntityManager.GetComponentData<MatchState>(match);

        protected override void OnCreate()
        {
            stocks = GetEntityQuery(typeof(Stock)); visuals = GetEntityQuery(typeof(Visual));
            servings = GetEntityQuery(typeof(Serving));
            plates = GetEntityQuery(typeof(Plate),typeof(Stock));
            owned = GetEntityQuery(new EntityQueryDesc { Any = new[] {ComponentType.ReadOnly<Stock>(), ComponentType.ReadOnly<Visual>(), ComponentType.ReadOnly<Serving>(), ComponentType.ReadOnly<MatchState>()} });
        }
        public void ResetMatch(uint seed = 20261104, int initial = 10000, float seconds = 120, MatchPhase phase = MatchPhase.Playing)
        {
            Dependency.Complete(); IsPaused=false; EntityManager.DestroyEntity(owned);
            random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            nextEpoch++; rain = 0; nextEatAt=2f; openingTotal=phase==MatchPhase.Countdown?math.max(0,initial):0; openingSpawned=0; SuccessfulCommands = RejectedCommands = 0;
            match = EntityManager.CreateEntity(typeof(MatchState));
            EntityManager.SetComponentData(match, new MatchState { Phase = phase, Remaining = seconds, CountdownRemaining=phase==MatchPhase.Countdown?3f:0, Epoch = nextEpoch });
            EntityManager.AddBuffer<CookCommand>(match);
            EntityManager.AddBuffer<CookedDish>(match);
            // v0.16 replaces recipe slots 200-399. Clear discoveries of the removed placeholder dishes.
            if (SaveDiscoveries && UnityEngine.PlayerPrefs.GetInt("feast.catalogue-version",0)<2)
            {
                for (int i=200;i<Cookbook.All.Length;i++) UnityEngine.PlayerPrefs.DeleteKey("feast.discovered."+i);
                UnityEngine.PlayerPrefs.SetInt("feast.catalogue-version",2);
                UnityEngine.PlayerPrefs.Save();
            }
            for (int i = 0; i < Cookbook.All.Length; i++)
            {
                Discovered[i] = SaveDiscoveries && UnityEngine.PlayerPrefs.GetInt("feast.discovered."+i,0) == 1;
            }
            if(phase!=MatchPhase.Countdown)Spawn(math.max(0,initial),false);
        }
        public void Submit(int player, int recipe, int sequence, int epoch)
        {
            if (IsPaused || !EntityManager.Exists(match) || State.Phase != MatchPhase.Playing) return;
            EntityManager.GetBuffer<CookCommand>(match).Add(new CookCommand { Player = player, Recipe = recipe, Sequence = sequence, Epoch = epoch });
        }
        public int[] Inventory()
        {
            Dependency.Complete(); var inventory = new int[Cookbook.KindCount];
            using (var a = stocks.ToComponentDataArray<Stock>(Allocator.Temp))
                foreach (var s in a) inventory[(int)s.Kind] += s.Quantity;
            return inventory;
        }
        public CookedDish[] MatchDishes()
        {
            Dependency.Complete();
            if(!EntityManager.Exists(match))return System.Array.Empty<CookedDish>();
            using(var rows=EntityManager.GetBuffer<CookedDish>(match).ToNativeArray(Allocator.Temp))return rows.ToArray();
        }
        // Record at the scoring transaction, even if serving completes after the bell.
        // This ledger survives consumption of a platter by either player's next recipe.
        void RecordDish(int player,int recipe,int count,int bonusPoints)
        {
            var rows=EntityManager.GetBuffer<CookedDish>(match);
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];if(row.Player!=player || row.Recipe!=recipe)continue;
                row.Count+=count;row.BonusPoints+=bonusPoints;rows[i]=row;return;
            }
            rows.Add(new CookedDish {Player=player,Recipe=recipe,Count=count,BonusPoints=bonusPoints});
        }
        void RecordEating(int player,int recipe,int count,int points)
        {
            var rows=EntityManager.GetBuffer<CookedDish>(match);
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];if(row.Player!=player || row.Recipe!=recipe)continue;
                row.EatenCount+=count;row.EatenPoints+=points;rows[i]=row;return;
            }
        }
        // Grow into successive tiers on the same rotating platform; there is no maximum plate count.
        public static float3 PlatePosition(int slot, float angle)
        {
            int layer=slot/7, seat=slot%7;
            float radius=seat==0?0:2.65f*ArenaGeometry.TableScale;
            float a=(seat-1)*(math.PI*2/6)+angle;
            return new float3(math.sin(a)*radius,.52f+layer*.85f*ArenaGeometry.FoodModelScale*ArenaGeometry.TableScale,math.cos(a)*radius);
        }
        int FreePlateSlot()
        {
            var occupied=new System.Collections.Generic.HashSet<int>();
            using(var es=stocks.ToEntityArray(Allocator.Temp)) foreach(Entity e in es)
                if(EntityManager.HasComponent<Plate>(e)) occupied.Add(EntityManager.GetComponentData<Plate>(e).Slot);
            int slot=0;while(occupied.Contains(slot))slot++;return slot;
        }

        protected override void OnUpdate()
        {
            if (IsPaused || !EntityManager.Exists(match)) return;
            Dependency.Complete();
            var state = State; float dt = SystemAPI.Time.DeltaTime; bool openingFinished=false;
            state.Angle += dt * RotationSpeed;
            if(state.Phase == MatchPhase.Countdown)
            {
                state.CountdownRemaining=math.max(0,state.CountdownRemaining-dt);
                int target=state.CountdownRemaining<=0?openingTotal:(int)math.floor(openingTotal*(3f-state.CountdownRemaining)/3f);
                int batch=math.max(0,target-openingSpawned);
                if(batch>0){Spawn(batch,false,true);openingSpawned+=batch;}
                if(state.CountdownRemaining<=0){state.Phase=MatchPhase.Playing;openingFinished=true;}
            }
            if((state.Phase == MatchPhase.Playing && !openingFinished) || state.Phase==MatchPhase.Result) state.Elapsed += dt;
            if(state.Phase == MatchPhase.Playing && !openingFinished)
            {
                state.Remaining = math.max(0,state.Remaining-dt);
                if(state.Remaining <= 0) state.Phase = MatchPhase.Result;
            }
            using (var es = servings.ToEntityArray(Allocator.Temp)) foreach (Entity e in es)
            {
                var serve = EntityManager.GetComponentData<Serving>(e);
                if (serve.ReadyAt > state.Elapsed) continue;
                Entity plateEntity = serve.Target;
                var s = EntityManager.GetComponentData<Stock>(plateEntity); s.Quantity += serve.Count;
                var p = EntityManager.GetComponentData<Plate>(plateEntity); p.Incoming -= serve.Count; p.Pulse = 1; p.ServedAt=state.Elapsed;
                EntityManager.SetComponentData(plateEntity,s); EntityManager.SetComponentData(plateEntity,p);
                Served?.Invoke(serve.Player,serve.Recipe,serve.Count);
                ArrivalCue?.Invoke(new CookCue {Player=serve.Player,Recipe=serve.Recipe,Count=serve.Count,Slot=p.Slot,SourceSlot=serve.SourceSlot,Flags=serve.Flags,BonusPoints=serve.BonusPoints});
                EntityManager.DestroyEntity(e);
            }
            // Copy and clear before structural changes invalidate a DynamicBuffer reference.
            var commands = EntityManager.GetBuffer<CookCommand>(match).ToNativeArray(Allocator.Temp);
            EntityManager.GetBuffer<CookCommand>(match).Clear();
            foreach (var cmd in commands)
            {
                if (state.Phase != MatchPhase.Playing || state.Remaining <= 0 || cmd.Epoch != state.Epoch || cmd.Player < 0 || cmd.Player > 1) continue;
                int last = cmd.Player == 0 ? state.LastCyan : state.LastOrange;
                if (cmd.Sequence <= last) continue;
                if (cmd.Player == 0) state.LastCyan = cmd.Sequence; else state.LastOrange = cmd.Sequence;
                if (cmd.Recipe < 0 || cmd.Recipe >= Cookbook.All.Length) { RejectedCommands++; Failed?.Invoke(cmd.Player); continue; }
                Cook(cmd, ref state);
            }
            commands.Dispose();
            if(state.Phase==MatchPhase.Playing && !openingFinished && AutoEatEnabled && state.Elapsed>=nextEatAt)
            {
                EatRandomDish(ref state);
                nextEatAt=state.Elapsed+random.NextFloat(2.5f,4.5f);
            }
            using (var es = visuals.ToEntityArray(Allocator.Temp)) foreach (Entity e in es)
                if (EntityManager.GetComponentData<Visual>(e).Mode == 2) EntityManager.DestroyEntity(e);
            using(var es=stocks.ToEntityArray(Allocator.Temp)) foreach(Entity e in es)
                if(EntityManager.HasComponent<Plate>(e) && EntityManager.GetComponentData<Stock>(e).Quantity==0 && EntityManager.GetComponentData<Plate>(e).Incoming==0)
                    EntityManager.DestroyEntity(e);
            if (state.Phase == MatchPhase.Playing && !openingFinished && RainEnabled)
            {
                rain += dt*65;
                int count = (int)rain;
                rain -= (int)rain; Spawn(count,true);
            }
            EntityManager.SetComponentData(match,state);
            // Index swept flight paths, not every pair of ingredients. A fast flyer can
            // hit a neighbour between frames, while distant stock is never disturbed.
            var wakes = new NativeParallelMultiHashMap<int2,FoodWake>(128,Allocator.TempJob);
            NativeParallelMultiHashMap<int2,GoldBody> goldBodies;
            using(var snapshot=visuals.ToComponentDataArray<Visual>(Allocator.Temp))
            {
                int goldCount=0;
                foreach(var v in snapshot)if(v.Mode==0 && v.Golden!=0)goldCount++;
                goldBodies=new NativeParallelMultiHashMap<int2,GoldBody>(math.max(1,goldCount*144),Allocator.TempJob);
                foreach(var v in snapshot)
                {
                    if(v.Mode==0 && v.Golden!=0)
                    {
                        float radius=FoodMotion.GoldCollisionRadius(v.Scale);
                        var body=new GoldBody {Position=v.Position.xz,Height=v.Position.y,Radius=radius,Scale=v.Scale};
                        float reach=radius+ArenaGeometry.IngredientMaxSize*.65f;
                        int2 goldLo=FoodMotion.Cell(v.Position.xz-reach);
                        int2 goldHi=FoodMotion.Cell(v.Position.xz+reach);
                        for(int x=goldLo.x;x<=goldHi.x;x++)for(int z=goldLo.y;z<=goldHi.y;z++)goldBodies.Add(new int2(x,z),body);
                    }
                    if(v.Mode!=1)continue;
                    float3 end=FoodMotion.FlightPosition(v,v.Life+dt,state.Angle);
                    if(math.lengthsq(end-v.Position)<.000064f)continue;
                    var wake=new FoodWake {From=v.Position,To=end,Radius=.65f+v.Scale*.7f};
                    float margin=FoodMotion.ContactRadius(wake,ArenaGeometry.IngredientMaxSize*GoldenScale);
                    int2 lo=FoodMotion.Cell(math.min(wake.From.xz,wake.To.xz)-margin);
                    int2 hi=FoodMotion.Cell(math.max(wake.From.xz,wake.To.xz)+margin);
                    for(int x=lo.x;x<=hi.x;x++)for(int z=lo.y;z<=hi.y;z++)wakes.Add(new int2(x,z),wake);
                }
            }
            Dependency = new AnimateFoodJob { Dt = dt, Angle = state.Angle, Wakes=wakes, GoldBodies=goldBodies }.ScheduleParallel(Dependency);
            Dependency = wakes.Dispose(Dependency);
            Dependency = goldBodies.Dispose(Dependency);
        }
        void EatRandomDish(ref MatchState state)
        {
            Entity chosen=Entity.Null;int eligible=0;
            using(var es=plates.ToEntityArray(Allocator.Temp))foreach(Entity e in es)
            {
                var p=EntityManager.GetComponentData<Plate>(e);
                var s=EntityManager.GetComponentData<Stock>(e);
                if(s.Quantity<=0 || p.Incoming>0 || state.Elapsed-p.ServedAt<EatingMinAge)continue;
                if(random.NextInt(++eligible)==0)chosen=e;
            }
            if(chosen==Entity.Null)return;
            var plate=EntityManager.GetComponentData<Plate>(chosen);
            var stock=EntityManager.GetComponentData<Stock>(chosen);
            int count=stock.Quantity;
            int points=count*Cookbook.All[plate.Recipe].Points;
            stock.Quantity=0;EntityManager.SetComponentData(chosen,stock);
            if(plate.LastPlayer==0)state.CyanScore+=points;else state.OrangeScore+=points;
            RecordEating(plate.LastPlayer,plate.Recipe,count,points);
            var cue=new EatingCue {Player=plate.LastPlayer,Recipe=plate.Recipe,Count=count,Slot=plate.Slot,Points=points};
            Eating?.Invoke(cue);
            float3 origin=PlatePosition(plate.Slot,state.Angle)+new float3(0,.4f,0);
            int shower=math.min(96,56+count*4);
            for(int i=0;i<shower;i++)
            {
                float a=random.NextFloat(0,math.PI*2),speed=random.NextFloat(7f,17f);
                Entity e=EntityManager.CreateEntity(typeof(Visual));
                EntityManager.SetComponentData(e,new Visual {Kind=stock.Kind,Mode=4,Player=plate.LastPlayer,Recipe=plate.Recipe,Position=origin+new float3(random.NextFloat(-.65f,.65f),random.NextFloat(0,1.2f),random.NextFloat(-.65f,.65f)),Start=origin,Drift=new float2(math.sin(a),math.cos(a))*speed,Velocity=random.NextFloat(12f,19f),Scale=random.NextFloat(.19f,.36f)*ArenaGeometry.FoodModelScale*ArenaGeometry.TableScale,Duration=random.NextFloat(2.3f,3.1f),Seed=random.NextFloat(0,6.28f)});
            }
        }
        void Cook(CookCommand cmd, ref MatchState state)
        {
            Recipe recipe = Cookbook.All[cmd.Recipe]; var plan = RecipeSolver.Solve(recipe,Inventory());
            if (plan.Servings == 0)
            {
                RejectedCommands++;
                for (int i = 0; i < plan.Missing.Count; i++)
                {
                    Entity e = EntityManager.CreateEntity(typeof(Visual));
                    var pos = new float3((cmd.Player == 0 ? -2.5f : 2.5f) + (i-(plan.Missing.Count-1)*.5f)*.65f,1.2f,-4.1f);
                    EntityManager.SetComponentData(e,new Visual { Kind = plan.Missing[i], Position = pos, Start = pos, Scale = 1.25f*ArenaGeometry.FoodModelScale, Mode = 3, Duration = .8f, Player = cmd.Player, EffectSlot = i, EffectCount = plan.Missing.Count });
                }
                Failed?.Invoke(cmd.Player); return;
            }
            int cueFlags=Discovered[cmd.Recipe]?0:1,sourceSlot=-1;
            for(int k=Cookbook.RawCount;k<Cookbook.KindCount;k++)if(plan.Consume[k]>0){cueFlags|=2;break;}
            // A recipe owns one shared platter, including any batches still being served.
            Entity target=Entity.Null;
            using(var es=stocks.ToEntityArray(Allocator.Temp)) foreach(Entity e in es)
                if(EntityManager.HasComponent<Plate>(e) && EntityManager.GetComponentData<Stock>(e).Kind==recipe.Output)
                {target=e;break;}
            if(target==Entity.Null)
            {
                cueFlags|=4;
                int slot=FreePlateSlot();
                target=EntityManager.CreateEntity(typeof(Stock),typeof(Plate));
                EntityManager.SetComponentData(target,new Stock {Kind=recipe.Output});
                EntityManager.SetComponentData(target,new Plate {Recipe=cmd.Recipe,Slot=slot});
            }
            var targetPlate=EntityManager.GetComponentData<Plate>(target);
            targetPlate.Incoming+=plan.Servings;targetPlate.LastPlayer=cmd.Player;targetPlate.Pulse=1;
            EntityManager.SetComponentData(target,targetPlate);
            int targetSlot=targetPlate.Slot,goldenUsed=0;
            // Commit one transaction at a time. The next queued command reads the updated stock.
            using (var es = stocks.ToEntityArray(Allocator.Temp)) foreach (Entity e in es)
            {
                var s = EntityManager.GetComponentData<Stock>(e); int k = (int)s.Kind;
                int take = math.min(s.Quantity,plan.Consume[k]); if (take == 0) continue;
                int goldenTake=math.min(take,s.GoldenQuantity);
                goldenUsed+=goldenTake;s.GoldenQuantity-=goldenTake;
                s.Quantity -= take; plan.Consume[k] -= take; EntityManager.SetComponentData(e,s);
                if (EntityManager.HasComponent<Visual>(e))
                {
                    var v = EntityManager.GetComponentData<Visual>(e); v.Mode = 1; v.Life = 0; v.Start = v.Position;
                    v.Duration = FlightTime; v.Player = cmd.Player; v.Recipe = cmd.Recipe; v.TargetSlot = targetSlot;
                    EntityManager.SetComponentData(e,v);
                }
                else
                {
                    var p = EntityManager.GetComponentData<Plate>(e);
                    if(sourceSlot<0)sourceSlot=p.Slot;
                    for (int j = 0; j < math.min(take,80); j++)
                    {
                        var origin = PlatePosition(p.Slot,state.Angle) + random.NextFloat3(-.25f,.25f);
                        Entity flying = EntityManager.CreateEntity(typeof(Visual));
                        EntityManager.SetComponentData(flying,new Visual { Kind = s.Kind, Mode = 1, Position = origin, Start = origin, Scale = .23f*ArenaGeometry.FoodModelScale, Duration = FlightTime, Player = cmd.Player, Recipe = cmd.Recipe, TargetSlot = targetSlot, Seed = random.NextFloat(0,6.28f) });
                    }
                }
            }
            // Only the raw gold actually consumed grants a bonus; completed dishes do not inherit it.
            int bonusPoints=goldenUsed*recipe.Points*(GoldenScoreMultiplier-1);
            Entity pending = EntityManager.CreateEntity(typeof(Serving));
            EntityManager.SetComponentData(pending,new Serving { Target = target, Player = cmd.Player, Recipe = cmd.Recipe, Count = plan.Servings, Flags = cueFlags, SourceSlot = sourceSlot, BonusPoints = bonusPoints, ReadyAt = state.Elapsed + FlightTime });
            if (cmd.Player == 0) state.CyanScore += plan.Servings*recipe.Points+bonusPoints; else state.OrangeScore += plan.Servings*recipe.Points+bonusPoints;
            RecordDish(cmd.Player,cmd.Recipe,plan.Servings,bonusPoints);
            Discovered[cmd.Recipe] = true;
            if (SaveDiscoveries) UnityEngine.PlayerPrefs.SetInt("feast.discovered."+cmd.Recipe,1);
            SuccessfulCommands++; Cooked?.Invoke(cmd.Player,cmd.Recipe,plan.Servings);
            CookingCue?.Invoke(new CookCue {Player=cmd.Player,Recipe=cmd.Recipe,Count=plan.Servings,Slot=targetSlot,SourceSlot=sourceSlot,Flags=cueFlags,BonusPoints=bonusPoints});
        }
        void Spawn(int count,bool goldEligible,bool openingRain=false)
        {
            for (int i = 0; i < count; i++)
            {
                int kind = random.NextInt(Cookbook.RawCount); float a = random.NextFloat(0,math.PI*2);
                bool onTable=random.NextFloat()<.7f;
                float inner=onTable?ArenaGeometry.IngredientInner:ArenaGeometry.TableRadius+.25f;
                float outer=onTable?ArenaGeometry.TableRadius-.35f:(goldEligible?ArenaGeometry.IngredientOuter:ArenaGeometry.InitialScatterOuter);
                float radius=!onTable && !goldEligible?random.NextFloat(inner,outer):math.sqrt(random.NextFloat(inner*inner,outer*outer));
                int golden=random.NextFloat()<GoldenChance && goldEligible?1:0;
                float scale=random.NextFloat(ArenaGeometry.IngredientMinSize,ArenaGeometry.IngredientMaxSize)*(golden==1?GoldenScale:1);
                float seed=random.NextFloat(0,math.PI*2);
                float floor=radius>ArenaGeometry.SpillEdge(scale)?ArenaGeometry.SpillFloor(seed):random.NextFloat(.45f,1.25f);
                Entity e = EntityManager.CreateEntity(typeof(Stock),typeof(Visual));
                EntityManager.SetComponentData(e,new Stock { Kind = (FoodKind)kind, Quantity = 1, GoldenQuantity = golden });
                float spawnHeight=openingRain?random.NextFloat(10f,17f):(goldEligible?random.NextFloat(8f,11.5f):random.NextFloat(1.2f,3.2f));
                float2 drift=golden==1?new float2(math.sin(seed),math.cos(seed))*random.NextFloat(4f,7f):float2.zero;
                EntityManager.SetComponentData(e,new Visual { Kind = (FoodKind)kind, Position = new float3(math.sin(a)*radius,spawnHeight,math.cos(a)*radius), Floor = floor, Scale = scale, Golden = golden, Seed = seed, Drift=drift });
            }
        }
    }

    public struct FoodWake {public float3 From,To;public float Radius;}
    public struct GoldBody {public float2 Position;public float Height,Radius,Scale;}
    public static class FoodMotion
    {
        public const float GoldRestitution=.98f;
        public static int2 Cell(float2 position)=>(int2)math.floor(position/1.25f);
        public static float ContactRadius(FoodWake wake,float scale)=>wake.Radius+scale*1.45f;
        public static float GoldCollisionRadius(float scale)=>scale*.55f;
        public static float3 GoldWorldPosition(Visual v)=>v.Position;
        public static float3 FlightPosition(Visual v,float life,float angle)
        {
            float delay=math.frac(v.Seed*.618034f)*.16f;
            float t=math.saturate((life/math.max(.001f,v.Duration)-delay)/(1-delay)),ease=t*t*(3-2*t);
            float3 target=FeastSystem.PlatePosition(v.TargetSlot,angle)+new float3(0,.13f,0);
            float3 position=math.lerp(v.Start,target,ease);
            position.y+=math.sin(t*math.PI)*(1.5f+.35f*math.sin(v.Seed));
            float curl=math.sin(t*math.PI)*.32f;
            position.x+=math.sin(t*math.PI*2+v.Seed)*curl;
            position.z+=math.cos(t*math.PI*2+v.Seed)*curl*.65f;
            return position;
        }
    }

    [BurstCompile]
    public partial struct AnimateFoodJob : IJobEntity
    {
        public float Dt, Angle;
        [ReadOnly] public NativeParallelMultiHashMap<int2,FoodWake> Wakes;
        [ReadOnly] public NativeParallelMultiHashMap<int2,GoldBody> GoldBodies;
        void Execute(ref Visual v)
        {
            v.Life += Dt;v.Impact=math.max(0,v.Impact-Dt*4);
            if (v.Mode == 0)
            {
                float step=math.min(Dt,.05f);
                v.ScatterCooldown=math.max(0,v.ScatterCooldown-Dt);
                v.Scatter=math.max(0,v.Scatter-Dt*1.8f);
                v.GoldLaunch=math.max(0,v.GoldLaunch-Dt);
                if(v.ScatterCooldown<=0)
                {
                    float2 kick=0;float strength=0;
                    if(Wakes.TryGetFirstValue(FoodMotion.Cell(v.Position.xz),out var wake,out var iterator))
                    do
                    {
                        float3 line=wake.To-wake.From;
                        float along=math.saturate(math.dot(v.Position-wake.From,line)/math.max(.00001f,math.lengthsq(line)));
                        float3 away=v.Position-(wake.From+line*along);
                        float radius=FoodMotion.ContactRadius(wake,v.Scale),distance=math.length(away);
                        if(distance>=radius)continue;
                        float weight=1-distance/radius;
                        float2 direction=math.normalizesafe(away.xz,new float2(math.sin(v.Seed),math.cos(v.Seed)));
                        kick+=(direction+math.normalizesafe(line.xz)*.3f)*weight;
                        strength=math.max(strength,weight);
                    }while(Wakes.TryGetNextValue(out wake,ref iterator));
                    if(strength>0)
                    {
                        float speed=math.lerp(1.5f,4.8f,strength);
                        v.Drift+=math.normalizesafe(kick)*speed;
                        v.Drift=math.normalizesafe(v.Drift)*math.min(5,math.length(v.Drift));
                        v.Velocity=math.max(v.Velocity,math.lerp(2.2f,4.6f,strength));
                        v.Scatter=math.max(v.Scatter,.45f+strength*.55f);v.ScatterCooldown=.14f;
                    }
                }
                if(v.Golden==0 && GoldBodies.TryGetFirstValue(FoodMotion.Cell(v.Position.xz),out var gold,out var goldIterator))
                {
                    float deepest=0;float2 awayDirection=0;
                    do
                    {
                        if(math.abs(v.Position.y-gold.Height)>(v.Scale+gold.Scale)*.65f)continue;
                        float2 away=v.Position.xz-gold.Position;
                        float distance=math.length(away);
                        float overlap=gold.Radius+v.Scale*.65f-distance;
                        if(overlap<=deepest)continue;
                        deepest=overlap;
                        awayDirection=math.normalizesafe(away,new float2(math.sin(v.Seed),math.cos(v.Seed)));
                    }while(GoldBodies.TryGetNextValue(out gold,ref goldIterator));
                    if(deepest>0)
                    {
                        v.Position.xz+=awayDirection*math.min(deepest,Dt*8f);
                        if(v.ScatterCooldown<=0)
                        {
                            float2 launch=v.Drift+awayDirection*math.min(30f,20f+deepest*4f);
                            v.Drift=math.normalizesafe(launch)*math.min(30f,math.length(launch));
                            v.Velocity=math.max(v.Velocity,6f);
                            v.GoldLaunch=2.4f;
                            v.ScatterCooldown=.6f;
                        }
                        v.Scatter=math.max(v.Scatter,.45f);
                    }
                }
                v.Position.xz+=v.Drift*step;v.Drift*=math.exp(-(v.Golden!=0?.015f:(v.GoldLaunch>0?.7f:3.2f))*step);
                float radiusNow=math.length(v.Position.xz);
                float inner=ArenaGeometry.IngredientInner,edge=ArenaGeometry.SpillEdge(v.Scale);
                if(v.Floor>=0 && radiusNow>edge)v.Floor=ArenaGeometry.SpillFloor(v.Seed);
                // Once food falls off the table, its momentum can carry it farther across the floor.
                if(math.lengthsq(v.Drift)>.0001f && radiusNow<inner && v.Floor>=0)
                {
                    float2 normal=math.normalizesafe(v.Position.xz,new float2(1,0));
                    v.Position.xz=normal*inner;
                    float outward=math.dot(v.Drift,normal);
                    if(outward<0)v.Drift-=normal*outward*1.5f;
                }
                else if(math.lengthsq(v.Drift)>.0001f && radiusNow<edge && v.Floor<0)
                {
                    float2 normal=math.normalizesafe(v.Position.xz,new float2(1,0));
                    v.Position.xz=normal*edge;
                    float outward=math.dot(v.Drift,normal);
                    if(outward<0)v.Drift-=normal*outward*1.5f;
                }
                // The lazy Susan pulls settled stock into a slow circular flow. The
                // influence fades toward the rim; spilled stock is stationary on the floor.
                if(v.Floor>=0 && math.abs(v.Position.y-v.Floor)<.08f)
                {
                    float radius=math.length(v.Position.xz);
                    if(radius>=inner && radius<=edge)
                    {
                        float influence=math.saturate((ArenaGeometry.TableRadius-radius)/(ArenaGeometry.TableRadius-inner));
                        float spin=FeastSystem.RotationSpeed*step*math.lerp(.2f,.9f,influence);
                        math.sincos(spin,out float sn,out float cs);
                        float2 at=v.Position.xz;
                        v.Position.xz=new float2(at.x*cs+at.y*sn,at.y*cs-at.x*sn);
                        v.OrbitAngle+=spin;
                    }
                }
                v.Velocity -= 16*step; v.Position.y += v.Velocity*step;
                if(v.Position.y<=v.Floor)
                {
                    v.Position.y=v.Floor;float speed=math.abs(v.Velocity);
                    if(speed>2){v.Impact=math.saturate(speed/15);v.LandedAt=v.Life;}
                    v.Velocity=v.Golden!=0?math.max(7f,speed*FoodMotion.GoldRestitution):(speed>.5f?speed*.28f:0);
                }
                float3 fromCenter=v.Position-new float3(0,.3f,0);
                float sphereDistance=math.length(fromCenter);
                if(sphereDistance>ArenaGeometry.BounceSphereRadius)
                {
                    float3 normal=fromCenter/sphereDistance;
                    v.Position=new float3(0,.3f,0)+normal*ArenaGeometry.BounceSphereRadius;
                    float3 velocity=new float3(v.Drift.x,v.Velocity,v.Drift.y);
                    float outwardSpeed=math.dot(velocity,normal);
                    if(outwardSpeed>0)velocity-=normal*outwardSpeed*(v.Golden!=0?1f+FoodMotion.GoldRestitution:1.65f);
                    v.Drift=velocity.xz;v.Velocity=velocity.y;
                }
            }
            else if (v.Mode == 1)
            {
                // Slightly stagger the visual launch; every ingredient still lands at the same transaction deadline.
                v.Position=FoodMotion.FlightPosition(v,v.Life,Angle);
                if (v.Life >= v.Duration) v.Mode = 2;
            }
            else if (v.Mode == 3)
            {
                v.Position.y = v.Start.y + v.Life*.7f;
                if (v.Life >= v.Duration) v.Mode = 2;
            }
            else if (v.Mode == 4)
            {
                float step=math.min(Dt,.05f);
                v.Position.xz+=v.Drift*step;
                v.Position.y+=v.Velocity*step;
                v.Velocity-=18f*step;
                v.Drift*=math.exp(-.28f*step);
                if(v.Life>=v.Duration)v.Mode=2;
            }
        }
    }
}
