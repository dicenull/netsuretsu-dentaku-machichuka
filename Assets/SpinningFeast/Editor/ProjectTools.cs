using System;
using System.IO;
using Unity.Core;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpinningFeast.Editor
{
    public static class ProjectTools
    {
        [MenuItem("熱烈電卓町中華/円卓シーンを作成・開く")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            const string path="Assets/Scenes/Banquet.unity";
            if(!File.Exists(path))
            {
                Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene,path);
            }
            else EditorSceneManager.OpenScene(path);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
        }
        public static void ValidateAndBuild()
        {
            CreateScene();RunSimulationTests();RunVisualDataTests();RunAudioChecks();
            PlayerSettings.companyName="SpinningFeast";PlayerSettings.productName="熱烈電卓町中華";
            PlayerSettings.bundleVersion="0.16.0";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"local.spinningfeast.prototype");
            QualitySettings.vSyncCount=1;
            AssetDatabase.SaveAssets();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/Banquet.unity"},locationPathName="Builds/熱烈電卓町中華.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Player build failed: "+report.summary.result);
            Debug.Log("FEAST_BUILD_PASS bytes="+report.summary.totalSize);
        }
        [MenuItem("熱烈電卓町中華/Windows向けに検証・ビルド")]
        public static void ValidateAndBuildWindows()
        {
            CreateScene();RunSimulationTests();RunVisualDataTests();RunAudioChecks();
            PlayerSettings.companyName="SpinningFeast";PlayerSettings.productName="熱烈電卓町中華";
            PlayerSettings.bundleVersion="0.16.0";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"local.spinningfeast.prototype");
            QualitySettings.vSyncCount=1;
            AssetDatabase.SaveAssets();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/Scenes/Banquet.unity"},
                locationPathName="Builds/Windows/SpinningFeast.exe",
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.None
            });
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows player build failed: "+report.summary.result);
            Debug.Log("FEAST_WINDOWS_BUILD_PASS bytes="+report.summary.totalSize);
        }
        [MenuItem("熱烈電卓町中華/unityroom向けWebGLをビルド")]
        public static void BuildUnityroomWeb()
        {
            CreateScene();RunSimulationTests();RunVisualDataTests();RunAudioChecks();
            PlayerSettings.companyName="SpinningFeast";
            PlayerSettings.productName="熱烈電卓町中華";
            PlayerSettings.bundleVersion="0.16.0";
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback=false;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.WebGL,"local.spinningfeast.prototype");
            AssetDatabase.SaveAssets();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/Scenes/Banquet.unity"},
                locationPathName="Builds/unityroom-web",
                target=BuildTarget.WebGL,
                options=BuildOptions.None
            });
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("WebGL player build failed: "+report.summary.result);
            string indexPath="Builds/unityroom-web/index.html";
            string html=File.ReadAllText(indexPath);
            string defaultTitle="<title>Unity Web Player | "+PlayerSettings.productName+"</title>";
            if(!html.Contains(defaultTitle))throw new Exception("Unexpected WebGL page title template");
            File.WriteAllText(indexPath,html.Replace(defaultTitle,"<title>"+PlayerSettings.productName+"</title>"));
            Debug.Log("FEAST_WEB_BUILD_PASS bytes="+report.summary.totalSize);
        }
        public static void RunAudioChecks()
        {
            var report=new System.Text.StringBuilder();
            var names=new List<string>(FeastSound.CueNames);names.AddRange(FeastSound.MusicNames);
            foreach(string name in names)
            {
                var clip=Resources.Load<AudioClip>("Audio/"+name);
                // Unity may optimize the 48 kHz WAV source to 44.1 kHz on some targets.
                if(clip==null || clip.channels!=2 || (clip.frequency!=44100 && clip.frequency!=48000) || clip.samples<clip.frequency/10)
                    throw new Exception("Invalid sound asset: "+name+" ("+(clip==null?"not loaded":$"{clip.channels}ch, {clip.frequency}Hz, {clip.samples} samples")+")");
                report.AppendLine(name+": "+clip.channels+" channels, "+clip.frequency+" Hz, "+clip.length.ToString("0.00")+" sec");
            }
            if(Resources.Load<AudioClip>("Audio/music_play").samples!=Resources.Load<AudioClip>("Audio/music_rush").samples)throw new Exception("Adaptive music layers must have identical loop lengths");
            report.AppendLine("Adaptive music loops have identical sample counts.");
            Directory.CreateDirectory("Docs");File.WriteAllText("Docs/audio-import-tests.txt",report.ToString()+"PASS: "+FeastSound.CueNames.Length+" audio cues and "+FeastSound.MusicNames.Length+" music loops imported.\n");
            Debug.Log("FEAST_AUDIO_PASS");
        }
        public static void RunVisualDataTests()
        {
            var report=new System.Text.StringBuilder("Triangle counts (base detail -> refined detail):\n");
            Action<Mesh,Mesh,string> check=(before,after,label)=>
            {
                int oldCount=before.triangles.Length/3,newCount=after.triangles.Length/3;
                if(newCount!=oldCount*2)throw new Exception("Mesh triangle doubling failed: "+label);
                report.AppendLine(label+": "+oldCount+" -> "+newCount);
                UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);
            };
            for(int k=0;k<Cookbook.KindCount;k++)check(FeastMeshes.Food((FoodKind)k,false),FeastMeshes.Food((FoodKind)k),((FoodKind)k).ToString());
            check(FeastMeshes.Disc(false),FeastMeshes.Disc(),"Plate");
            check(FeastMeals.Plate(false),FeastMeals.Plate(),"Porcelain serving plate");
            check(FeastMeshes.Ring(1,.025f,96,false),FeastMeshes.Ring(1,.025f),"Rim");
            foreach(var type in new[]{PrimitiveType.Cube,PrimitiveType.Cylinder,PrimitiveType.Sphere})
            {
                var go=GameObject.CreatePrimitive(type);var original=UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
                check(original,FeastMeshes.DoubleTriangles(original),type.ToString());UnityEngine.Object.DestroyImmediate(go);
            }
            Directory.CreateDirectory("Docs");File.WriteAllText("Docs/mesh-tests.txt",report.ToString()+"PASS: every refined mesh has twice the triangles of its base-detail counterpart. Raw ingredients include new silhouette and surface details.\n");
            Debug.Log("FEAST_MESH_PASS");
        }
        [MenuItem("熱烈電卓町中華/シミュレーションを検査")]
        public static void RunSimulationTests()
        {
            int checks=0;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("FEAST_TEST_FAIL "+label);checks++;};
            using(var world=new World("Feast isolated tests"))
            {
                var s=world.GetOrCreateSystemManaged<FeastSystem>();s.SaveDiscoveries=false;s.RainEnabled=false;s.AutoEatEnabled=false;s.ResetMatch(42,0,120);
                var em=world.EntityManager;
                var servedActors=new List<int>();s.Served+=(player,recipe,count)=>servedActors.Add(player);
                var cookingCues=new List<CookCue>();var arrivalCues=new List<CookCue>();
                s.CookingCue+=cue=>cookingCues.Add(cue);s.ArrivalCue+=cue=>arrivalCues.Add(cue);
                Action<FoodKind,int> add=(k,n)=>{for(int i=0;i<n;i++){Entity e=em.CreateEntity(typeof(Stock),typeof(Visual));em.SetComponentData(e,new Stock{Kind=k,Quantity=1});em.SetComponentData(e,new Visual{Kind=k,Scale=.2f});}};
                Func<int,int,int> recordedPortions=(player,recipe)=>{foreach(var row in s.MatchDishes())if(row.Player==player && row.Recipe==recipe)return row.Count;return 0;};
                Func<int> goldStock=()=>{int total=0;using(var q=em.CreateEntityQuery(typeof(Stock)))using(var all=q.ToComponentDataArray<Stock>(Allocator.Temp))foreach(var stock in all)total+=stock.GoldenQuantity;return total;};
                Action<FoodKind> addGold=k=>{Entity e=em.CreateEntity(typeof(Stock),typeof(Visual));em.SetComponentData(e,new Stock {Kind=k,Quantity=1,GoldenQuantity=1});em.SetComponentData(e,new Visual {Kind=k,Golden=1,Scale=.5f*FeastSystem.GoldenScale});};
                double time=0;Action<float> tick=dt=>{time+=dt;world.SetTime(new TimeData(time,dt));s.Update();em.CompleteAllTrackedJobs();};
                add(FoodKind.Rice,20);add(FoodKind.Egg,30);add(FoodKind.Carrot,5);add(FoodKind.Onion,5);
                s.Submit(0,0,1,s.State.Epoch);s.Submit(1,0,1,s.State.Epoch);tick(.02f);
                check(s.State.CyanScore==1000 && s.State.OrangeScore==0,"serialized contention, exact max batch");
                check(recordedPortions(0,0)==10 && recordedPortions(1,0)==0 && s.MatchDishes().Length==1,"result ledger records successful batch only, before arrival");
                check(s.Inventory()[(int)FoodKind.Rice]==0,"atomic raw consumption");
                check(s.Inventory()[(int)FoodKind.FriedRice]==0,"pending dishes cannot be consumed");
                check(servedActors.Count==0,"serving sound event waits for arrival");
                check(cookingCues.Count==1 && arrivalCues.Count==0,"presentation cue emitted once for success, never for failed contention");
                check(cookingCues[0].Player==0 && cookingCues[0].Recipe==0 && cookingCues[0].Count==10 && cookingCues[0].Discovery && !cookingCues[0].Chain && cookingCues[0].SourceSlot==-1,"first raw recipe cue carries actor, portions and discovery");
                using(var q=em.CreateEntityQuery(typeof(Visual)))
                using(var effects=q.ToComponentDataArray<Visual>(Allocator.Temp))
                {
                    var missing=new HashSet<FoodKind>();
                    foreach(var v in effects) if(v.Mode==3) {missing.Add(v.Kind);check(v.Player==1 && v.EffectCount==2,"missing effects identify player and slots");}
                    check(missing.SetEquals(new[]{FoodKind.Rice,FoodKind.Carrot}),"only missing requirements create effects");
                }
                tick(.9f);tick(.01f);
                using(var q=em.CreateEntityQuery(typeof(Visual)))
                using(var effects=q.ToComponentDataArray<Visual>(Allocator.Temp))
                    foreach(var v in effects)check(v.Mode!=3,"missing effects expire");
                check(s.Inventory()[(int)FoodKind.FriedRice]==10,"serving commits ten portions");
                check(servedActors.Count==1 && servedActors[0]==0,"arrival event belongs to cyan cook");
                check(arrivalCues.Count==1 && arrivalCues[0].Player==0 && arrivalCues[0].Recipe==0 && arrivalCues[0].Count==10 && arrivalCues[0].Slot==cookingCues[0].Slot && arrivalCues[0].Discovery,"arrival preserves its originating presentation metadata");
                s.Submit(1,6,2,s.State.Epoch);tick(.02f);
                check(s.State.OrangeScore==1800,"opponent consumes shared completed dish");
                check(cookingCues.Count==2 && cookingCues[1].Chain && cookingCues[1].Discovery && cookingCues[1].SourceSlot==cookingCues[0].Slot && cookingCues[1].Player==1,"shared finished dish creates a chain bridge from its source plate");
                check(recordedPortions(0,0)==10 && recordedPortions(1,6)==10,"result ledger retains consumed dishes and credits the actual chain cook");
                check(s.Inventory()[(int)FoodKind.FriedRice]==0,"plate count reduced");
                tick(.9f);check(s.Inventory()[(int)FoodKind.Omurice]==10,"chain produces exact portions");
                check(servedActors.Count==2 && servedActors[1]==1,"arrival event belongs to orange cook");
                check(s.Discovered[0] && s.Discovered[6],"unseen recipe succeeds and is discovered");
                add(FoodKind.Rice,2);add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                s.Submit(0,0,1,s.State.Epoch);tick(.02f);check(s.Inventory()[(int)FoodKind.Rice]==2,"duplicate sequence ignored");
                s.Submit(0,0,2,s.State.Epoch-1);tick(.02f);check(s.Inventory()[(int)FoodKind.Rice]==2,"stale match ignored");
                check(cookingCues.Count==2 && arrivalCues.Count==2,"duplicate and stale commands create no new presentation events");
                check(recordedPortions(0,0)==10 && s.MatchDishes().Length==2,"duplicate and stale commands do not change the result ledger");
                s.Submit(0,0,3,s.State.Epoch);tick(.02f);tick(.9f);
                add(FoodKind.Rice,2);add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                s.Submit(0,0,4,s.State.Epoch);tick(.02f);tick(.9f);
                check(s.Inventory()[(int)FoodKind.FriedRice]==2,"same recipe merges into one platter");
                check(cookingCues[0].NewPlatter && cookingCues[1].NewPlatter && cookingCues[2].NewPlatter && !cookingCues[3].NewPlatter,"platter entrance fires for a new shared plate, never for an added serving");
                check(!cookingCues[2].Discovery && !cookingCues[3].Discovery && !arrivalCues[2].Discovery && !arrivalCues[3].Discovery,"rediscovery does not replay first-discovery fanfare");
                using(var q=em.CreateEntityQuery(typeof(Plate))) check(q.CalculateEntityCount()==2,"two distinct recipes create exactly two platters");
                for(int batch=0;batch<36;batch++)
                {
                    add(FoodKind.Rice,2);add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                    s.Submit(batch%2,0,100+batch,s.State.Epoch);tick(.02f);
                }
                using(var q=em.CreateEntityQuery(typeof(Serving)))
                using(var pending=q.ToComponentDataArray<Serving>(Allocator.Temp))
                {
                    var targets=new HashSet<Entity>();foreach(var serving in pending)targets.Add(serving.Target);
                    check(pending.Length==36 && targets.Count==1,"both players' pending batches share one platter");
                }
                add(FoodKind.Egg,4);s.Submit(1,6,1000,s.State.Epoch);tick(.02f);
                check(s.Inventory()[(int)FoodKind.FriedRice]==0,"finished portions can be consumed while more are incoming");
                using(var q=em.CreateEntityQuery(typeof(Plate)))check(q.CalculateEntityCount()==2,"empty platter with incoming portions is retained");
                tick(.9f);
                using(var q=em.CreateEntityQuery(typeof(Plate)))
                using(var plates=q.ToComponentDataArray<Plate>(Allocator.Temp))
                {
                    check(plates.Length==2,"repeated batches never duplicate a recipe platter");
                    var slots=new HashSet<int>();foreach(var plate in plates)slots.Add(plate.Slot);
                    check(slots.Count==2,"different recipes have distinct seats");
                }
                check(s.Inventory()[(int)FoodKind.FriedRice]==36 && s.Inventory()[(int)FoodKind.Omurice]==12,"arrivals merge exactly into their existing platters");
                check(recordedPortions(0,0)==30 && recordedPortions(1,0)==18 && recordedPortions(1,6)==12 && s.MatchDishes().Length==3,"same recipe merges per cook, separate from shared platter ownership");
                int cyanLedgerScore=0,orangeLedgerScore=0;
                foreach(var row in s.MatchDishes())if(row.Player==0)cyanLedgerScore+=row.Count*Cookbook.All[row.Recipe].Points+row.BonusPoints+row.EatenPoints;else orangeLedgerScore+=row.Count*Cookbook.All[row.Recipe].Points+row.BonusPoints+row.EatenPoints;
                check(cyanLedgerScore==s.State.CyanScore && orangeLedgerScore==s.State.OrangeScore,"result portions reconcile to both final scores");
                var positions=new HashSet<Unity.Mathematics.float3>();
                for(int slot=0;slot<260;slot++)positions.Add(FeastSystem.PlatePosition(slot,0));
                check(positions.Count==260,"layout extends beyond the current recipe catalogue without a plate cap");
                bool fits=true;float plateRadius=.72f*ArenaGeometry.FoodModelScale*ArenaGeometry.TableScale;
                for(int seat=0;seat<7;seat++)
                {
                    float3 at=FeastSystem.PlatePosition(seat,0);
                    fits &= math.length(at.xz)+plateRadius<ArenaGeometry.TurntableRadius;
                    for(int other=0;other<seat;other++)fits &= math.distance(at.xz,FeastSystem.PlatePosition(other,0).xz)>plateRadius*2;
                }
                check(fits,"seven enlarged platters fit on each tier without intersecting each other");
                int score=s.State.CyanScore;tick(130);
                s.Submit(0,0,5,s.State.Epoch);tick(.02f);check(s.State.CyanScore==score && s.State.Remaining==0,"no cooking after deadline");
                foreach(int n in s.Inventory())check(n>=0,"nonnegative inventory");
                int epoch=s.State.Epoch;s.ResetMatch(42,3000,120);
                check(s.State.Epoch>epoch && s.State.CyanScore==0,"reset isolates match and clears scores");
                check(s.MatchDishes().Length==0,"new match clears the result ledger");
                int raw=0;foreach(int n in s.Inventory())raw+=n;check(raw==3000,"3000 starting ingredients");
                tick(.016f);check(s.State.Remaining>119,"Burst job runs on 3000 entities");
                s.RainEnabled=true;s.ResetMatch(42,0,120,MatchPhase.Title);
                add(FoodKind.Rice,2);add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                s.Submit(0,0,1,s.State.Epoch);tick(150);
                check(s.State.Phase==MatchPhase.Title && s.State.Remaining==120 && s.State.Elapsed==0,"title never starts the match clock");
                check(s.Inventory()[(int)FoodKind.Rice]==2 && s.SuccessfulCommands==0,"title rejects cooking and disables ingredient replenishment");
                check(em.GetBuffer<CookCommand>(s.MatchEntity).Length==0,"title does not retain submitted commands");
                epoch=s.State.Epoch;s.RainEnabled=false;s.ResetMatch(42,0,1);
                add(FoodKind.Rice,2);add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                s.Submit(0,0,1,s.State.Epoch);tick(.3f);
                check(s.State.Phase==MatchPhase.Playing && s.State.CyanScore==100,"starting from title permits normal cooking");
                tick(.71f);
                check(s.State.Phase==MatchPhase.Result && s.State.Remaining==0,"deadline transitions automatically to result");
                check(recordedPortions(0,0)==1 && s.MatchDishes().Length==1,"last-second scored dish is already listed at the result transition");
                score=s.State.CyanScore;s.Submit(1,0,1,s.State.Epoch);tick(.2f);
                check(s.State.CyanScore==score && s.State.OrangeScore==0,"result scores stay final");
                check(s.Inventory()[(int)FoodKind.FriedRice]==1,"last valid batch finishes serving after deadline");
                s.ResetMatch(42,0,120);
                check(s.State.Phase==MatchPhase.Playing && s.State.Epoch>epoch && s.State.CyanScore==0 && s.State.OrangeScore==0 && s.State.Remaining==120,"rematch clears scores and restores full time");
                using(var q=em.CreateEntityQuery(typeof(Plate)))check(q.CalculateEntityCount()==0,"rematch clears old platters");
                s.ResetMatch(42,0,120,MatchPhase.Title);tick(1);
                check(s.State.Phase==MatchPhase.Title && s.State.Remaining==120,"return to title restores idle state");
                check(Cookbook.All.Length==400 && Cookbook.RawCount==35,"expanded catalogue counts");
                s.ResetMatch(42,0,120);add(FoodKind.Rice,2);add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                s.Submit(0,0,1,s.State.Epoch);tick(.02f);int oldArrivals=arrivalCues.Count;
                s.ResetMatch(42,0,120);tick(1);
                check(arrivalCues.Count==oldArrivals,"reset discards pending arrival fanfares from the previous match");
                check(FeastEffects.BurstCount(1)<FeastEffects.BurstCount(20) && FeastEffects.BurstCount(int.MaxValue)==180 && FeastEffects.BurstCount(-5)>=0,"large-batch sparkle density is bounded independently of portions");
                check(FeastEffects.GradeForPortions(1)==0 && FeastEffects.GradeForPortions(10)==1 && FeastEffects.GradeForPortions(40)==2 && FeastEffects.GradeForPortions(120)==3,"completion grade follows serving count at each threshold");
                check(FeastHudEffects.EatingSplashOpacity(0)>0 && FeastHudEffects.EatingSplashOpacity(1)>.9f && FeastHudEffects.EatingSplashOpacity(2f)>0 && FeastHudEffects.EatingSplashOpacity(2.4f)==0,"eating screen flash remains visible before fading out");
                s.ResetMatch(42,0,120);
                Func<float3,Entity> loose=position=>
                {
                    var entity=em.CreateEntity(typeof(Stock),typeof(Visual));
                    em.SetComponentData(entity,new Stock {Kind=FoodKind.Tomato,Quantity=1});
                    em.SetComponentData(entity,new Visual {Kind=FoodKind.Tomato,Position=position,Floor=position.y,Scale=.5f,Seed=1});
                    return entity;
                };
                float nearRadius=ArenaGeometry.IngredientInner+1f,farRadius=ArenaGeometry.TableRadius-.8f,floorRadius=ArenaGeometry.TableRadius+1f;
                var nearFlow=loose(new float3(0,.7f,nearRadius));
                var farFlow=loose(new float3(0,.7f,farRadius));
                var floorStock=loose(new float3(0,-1f,floorRadius));
                for(int frame=0;frame<60;frame++)tick(1f/60);
                var nearAfter=em.GetComponentData<Visual>(nearFlow);
                var farAfter=em.GetComponentData<Visual>(farFlow);
                var floorAfter=em.GetComponentData<Visual>(floorStock);
                check(nearAfter.Position.x>.3f && nearAfter.OrbitAngle>farAfter.OrbitAngle && math.abs(math.length(nearAfter.Position.xz)-nearRadius)<.001f,"the rotating center carries and turns nearby table ingredients around the same radius");
                check(farAfter.Position.x>.1f && farAfter.OrbitAngle>0 && math.abs(math.length(farAfter.Position.xz)-farRadius)<.001f,"outer table ingredients follow a slower circular flow");
                check(math.lengthsq(floorAfter.Position.xz-new float2(0,floorRadius))<.000001f && floorAfter.OrbitAngle==0,"ingredients beyond the table do not orbit on the floor");
                s.ResetMatch(42,0,120);
                var neighbour=loose(new float3(ArenaGeometry.TableRadius-1.8f,.7f,0));
                var edgeNeighbour=loose(new float3(ArenaGeometry.TableRadius+.05f,.7f,0));
                var distant=loose(new float3(-ArenaGeometry.TableRadius+1.6f,.7f,0));
                var overhead=loose(new float3(ArenaGeometry.TableRadius-1.8f,8,0));
                var flyer=em.CreateEntity(typeof(Visual));
                em.SetComponentData(flyer,new Visual {Mode=1,Start=new float3(ArenaGeometry.TableRadius-1.3f,.7f,0),Position=new float3(ArenaGeometry.TableRadius-1.3f,.7f,0),Scale=.5f,Duration=FeastSystem.FlightTime});
                tick(.08f);
                var bumped=em.GetComponentData<Visual>(neighbour);
                check(bumped.Velocity>0 && math.lengthsq(bumped.Drift)>.1f && bumped.Scatter>0,"moving cooking ingredients launch nearby loose stock sideways and upward");
                check(em.GetComponentData<Visual>(edgeNeighbour).Scatter>0,"expanded cooking collision reaches ingredients across the table edge");
                check(math.lengthsq(em.GetComponentData<Visual>(distant).Drift)==0 && em.GetComponentData<Visual>(distant).Scatter==0,"distant stock is not affected by cooking wakes");
                check(em.GetComponentData<Visual>(overhead).Scatter==0,"wakes use height as well as table position");
                check(s.Inventory()[(int)FoodKind.Tomato]==4 && s.State.CyanScore==0,"scattering changes neither ingredient quantities nor score");
                for(int frame=0;frame<240;frame++)tick(1f/60);
                bumped=em.GetComponentData<Visual>(neighbour);
                check(bumped.Scatter==0 && math.length(bumped.Drift)<.02f && math.abs(bumped.Position.y-bumped.Floor)<.02f,"scattered stock settles without endless jitter");
                bumped.Position=new float3(ArenaGeometry.TableRadius-.4f,.7f,0);bumped.Drift=new float2(20,0);
                em.SetComponentData(neighbour,bumped);tick(.05f);
                bumped=em.GetComponentData<Visual>(neighbour);
                check(math.length(bumped.Position.xz)>ArenaGeometry.TableRadius && bumped.Floor<0 && bumped.Drift.x>0,"scattered ingredients can spill past the table edge onto the floor");
                bumped.Drift=new float2(40,0);
                em.SetComponentData(neighbour,bumped);tick(.05f);
                bumped=em.GetComponentData<Visual>(neighbour);
                check(math.length(bumped.Position.xz)>ArenaGeometry.IngredientOuter && bumped.Drift.x>0,"spilled food can travel beyond the initial camera arena");
                bumped.Position=new float3(ArenaGeometry.BounceSphereRadius+.2f,-1f,0);bumped.Drift=new float2(40,0);
                em.SetComponentData(neighbour,bumped);tick(.05f);
                bumped=em.GetComponentData<Visual>(neighbour);
                check(math.length(bumped.Position-new float3(0,.3f,0))<=ArenaGeometry.BounceSphereRadius+.001f && bumped.Drift.x<0,"offscreen food rebounds from the three-dimensional sphere");
                bumped.Floor=.7f;
                bumped.Position=new float3(ArenaGeometry.IngredientInner+.01f,.7f,0);bumped.Drift=new float2(-10,0);
                em.SetComponentData(neighbour,bumped);tick(.05f);
                bumped=em.GetComponentData<Visual>(neighbour);
                check(math.length(bumped.Position.xz)>=ArenaGeometry.IngredientInner-.001f && bumped.Drift.x>0,"turntable border keeps loose stock outside finished platters");
                s.ResetMatch(42,0,120);
                var fast=new Visual {Mode=1,Start=new float3(7,.7f,0),Position=new float3(7,.7f,0),Scale=.5f,Duration=FeastSystem.FlightTime};
                float3 end=FoodMotion.FlightPosition(fast,.35f,.35f*FeastSystem.RotationSpeed);
                neighbour=loose(math.lerp(fast.Position,end,.5f));
                flyer=em.CreateEntity(typeof(Visual));em.SetComponentData(flyer,fast);tick(.35f);
                check(em.GetComponentData<Visual>(neighbour).Scatter>0,"swept flight paths hit stock between frames even at low frame rates");
                s.ResetMatch(42,0,120);
                Entity goldCollider=em.CreateEntity(typeof(Visual));
                em.SetComponentData(goldCollider,new Visual {Kind=FoodKind.Egg,Golden=1,Scale=1.5f,Position=new float3(15f,.72f,0),Floor=.7f,Velocity=-8f,Drift=new float2(4,2)});
                tick(1f/60);
                var bounced=em.GetComponentData<Visual>(goldCollider);
                check(bounced.Velocity>7f && bounced.Position.y>=bounced.Floor && bounced.Position.x>15f,"gold physically rebounds from the floor with near-unit restitution and sideways momentum");
                Entity contact=em.CreateEntity(typeof(Visual));
                em.SetComponentData(contact,new Visual {Kind=FoodKind.Rice,Scale=.8f,Position=new float3(15.6f,.7f,0),Floor=.7f,Life=1});
                tick(.02f);
                var contactAfter=em.GetComponentData<Visual>(contact);
                check(contactAfter.Position.x>15.6f && contactAfter.Scatter>0 && contactAfter.Drift.x>0 && contactAfter.GoldLaunch>0,"bouncing gold launches nearby food toward the offscreen boundary");
                for(int frame=0;frame<240;frame++)tick(1f/60);
                contactAfter=em.GetComponentData<Visual>(contact);
                check(contactAfter.GoldLaunch==0 && math.abs(contactAfter.Position.y-contactAfter.Floor)<.05f,"gold collision settles after one ballistic launch");
                s.ResetMatch(42,3100,120);
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))
                using(var all=q.ToComponentDataArray<Visual>(Allocator.Temp))
                {
                    int spilled=0,onTable=0;float farthest=0;
                    foreach(var v in all)
                    {
                        float radius=math.length(v.Position.xz);farthest=math.max(farthest,radius);
                        if(v.Floor<0 && radius>ArenaGeometry.TableRadius)spilled++;
                        else if(v.Floor>=0 && radius<ArenaGeometry.TableRadius)onTable++;
                    }
                    check(all.Length==3100 && onTable>2000 && spilled>800,"all stock is drawn and most ingredients spawn on the table");
                    check(farthest>30f && farthest<=ArenaGeometry.InitialScatterOuter+.001f,"initial ingredients scatter well beyond the camera-sized arena");
                }
                s.RainEnabled=true;tick(1);s.RainEnabled=false;
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))check(q.CalculateEntityCount()==3165,"ingredient rain keeps adding stock beyond the former 3000 limit");
                using(var q=em.CreateEntityQuery(typeof(Visual)))using(var all=q.ToComponentDataArray<Visual>(Allocator.Temp))
                {
                    int high=0;foreach(var v in all)if(v.Position.y>6f)high++;
                    check(high==65,"new ingredients rain from far above the initial stock");
                }
                s.ResetMatch();
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))check(q.CalculateEntityCount()==10000,"a normal match begins with 10000 ingredients");
                check(goldStock()==0,"a normal match starts with no gold ingredients");
                s.RainEnabled=false;s.ResetMatch(42,300,120,MatchPhase.Countdown);
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))check(q.CalculateEntityCount()==0,"countdown starts before any ingredients have fallen");
                int countdownEpoch=s.State.Epoch;s.Submit(0,0,1,countdownEpoch);
                tick(1);
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))check(q.CalculateEntityCount()==100,"the first countdown second rains one third of the opening stock");
                check(s.State.Phase==MatchPhase.Countdown && s.State.Remaining==120 && s.State.Elapsed==0 && s.SuccessfulCommands==0,"countdown blocks cooking and preserves the match clock");
                tick(1);
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))check(q.CalculateEntityCount()==200,"the second countdown second continues the ingredient rain");
                tick(1);
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))check(q.CalculateEntityCount()==300,"the countdown completes all opening ingredients");
                check(s.State.Phase==MatchPhase.Playing && s.State.Remaining==120 && goldStock()==0,"play begins with full time and no opening gold");
                s.RainEnabled=true;tick(1);s.RainEnabled=false;
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))check(q.CalculateEntityCount()==365,"ingredient rain continues after the countdown");
                s.ResetMatch();s.RainEnabled=true;tick(1);s.RainEnabled=false;
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))check(q.CalculateEntityCount()==10065,"ingredient rain continues beyond 10000 without an overall cap");
                s.ResetMatch(42,3000,120);tick(.016f);
                using(var q=em.CreateEntityQuery(typeof(Visual)))using(var all=q.ToComponentDataArray<Visual>(Allocator.Temp))
                {
                    bool clean=true;foreach(var v in all)clean &= v.Scatter==0 && math.lengthsq(v.Drift)==0;
                    check(clean,"reset removes all old flight wakes and scatter velocities");
                }
                for(int level=0;level<3;level++)
                {
                    s.ResetMatch(42,3000,120);
                    int cooked=0;
                    for(int turn=0;turn<8;turn++)
                    {
                        var stock=s.Inventory();int chosen=FeastCpu.Choose(level,stock,turn);
                        if(chosen<0)continue;
                        var recipe=Cookbook.All[chosen];var plan=RecipeSolver.Solve(recipe,stock);
                        int before=s.State.OrangeScore,quantity=stock[(int)recipe.Output],goldBefore=goldStock();
                        s.Submit(1,chosen,turn+1,s.State.Epoch);tick(.02f);
                        check(s.State.OrangeScore==before+(plan.Servings+(goldBefore-goldStock())*(FeastSystem.GoldenScoreMultiplier-1))*recipe.Points && s.State.CyanScore==0,"CPU level uses exact base and consumed-gold scoring: "+level);
                        tick(.9f);
                        check(s.Inventory()[(int)recipe.Output]==quantity+plan.Servings,"CPU level receives normal shared servings: "+level);
                        cooked++;
                    }
                    check(cooked>0,"CPU level can play against actual ECS stock: "+level);
                }
                // Gold is ordinary recipe stock with a single-use scoring bonus.
                s.ResetMatch(42,10000,120);
                check(goldStock()==0,"even a seeded match starts with zero gold ingredients");
                s.ResetMatch(42,0,120);s.RainEnabled=true;tick(100);s.RainEnabled=false;
                int spawnedGold=goldStock();
                check(spawnedGold>0 && spawnedGold<25,"gold appears randomly in ongoing rain at about one per ten seconds: "+spawnedGold);
                using(var q=em.CreateEntityQuery(typeof(Stock),typeof(Visual)))using(var entities=q.ToEntityArray(Allocator.Temp))
                {
                    bool valid=true;int marked=0;
                    foreach(var entity in entities)
                    {
                        var stock=em.GetComponentData<Stock>(entity);var visual=em.GetComponentData<Visual>(entity);
                        float scale=visual.Golden!=0?FeastSystem.GoldenScale:1;
                        marked+=visual.Golden;
                        valid &= stock.Quantity==1 && stock.GoldenQuantity==visual.Golden && (int)stock.Kind<Cookbook.RawCount;
                        valid &= visual.Scale>=ArenaGeometry.IngredientMinSize*scale && visual.Scale<=ArenaGeometry.IngredientMaxSize*scale;
                    }
                    check(valid && marked==spawnedGold,"gold appearance, larger size and authoritative stock agree");
                }
                s.ResetMatch(42,0,120);s.RainEnabled=true;tick(100);s.RainEnabled=false;
                check(goldStock()==spawnedGold,"gold rain is repeatable for a match seed");
                s.ResetMatch(42,0,120);cookingCues.Clear();arrivalCues.Clear();
                addGold(FoodKind.Rice);addGold(FoodKind.Rice);addGold(FoodKind.Tomato);
                add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                s.Submit(0,0,1,s.State.Epoch);s.Submit(1,0,1,s.State.Epoch);tick(.02f);
                check(s.State.CyanScore==500 && s.State.OrangeScore==0,"two gold ingredients triple their serving-value only for their cook");
                check(goldStock()==1 && s.Inventory()[(int)FoodKind.Tomato]==1,"unrelated gold stock grants no bonus and remains available");
                check(cookingCues.Count==1 && cookingCues[0].BonusPoints==400 && s.MatchDishes()[0].BonusPoints==400,"gold bonus reaches score effects and result ledger once");
                using(var q=em.CreateEntityQuery(typeof(Visual)))using(var all=q.ToComponentDataArray<Visual>(Allocator.Temp))
                {
                    int goldenFlights=0;foreach(var v in all)if(v.Mode==1 && v.Golden==1)goldenFlights++;
                    check(goldenFlights==2,"consumed gold keeps its appearance while flying to the platter");
                }
                tick(.9f);
                check(arrivalCues.Count==1 && arrivalCues[0].BonusPoints==400 && s.State.CyanScore==500,"arrival preserves bonus metadata without scoring twice");
                add(FoodKind.Egg,2);s.Submit(1,6,2,s.State.Epoch);tick(.02f);
                check(s.State.OrangeScore==180 && cookingCues[1].BonusPoints==0,"shared chain dishes do not recycle a spent gold bonus");
                addGold(FoodKind.Rice);add(FoodKind.Rice,1);add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                s.Submit(0,0,2,s.State.Epoch);tick(.02f);
                check(s.State.CyanScore==800 && s.MatchDishes()[0].Count==2 && s.MatchDishes()[0].BonusPoints==600,"repeated batches accumulate portions and their exact gold bonuses");
                s.Submit(0,0,2,s.State.Epoch);s.Submit(1,0,3,s.State.Epoch-1);tick(.02f);
                check(s.State.CyanScore==800 && s.State.OrangeScore==180 && goldStock()==1,"duplicate, failed and stale requests cannot award or consume gold");
                s.ResetMatch(42,0,120);
                check(goldStock()==0 && s.MatchDishes().Length==0 && s.State.CyanScore==0,"reset clears gold stock and bonus records");
                for(int i=0;i<Cookbook.All.Length;i++)
                {
                    s.ResetMatch(42,0,120);var recipe=Cookbook.All[i];
                    foreach(var need in recipe.Needs)add(need.Any[0],need.Units*3);
                    s.Submit(0,i,1,s.State.Epoch);tick(.02f);
                    check(s.State.CyanScore==recipe.Points*3 && s.SuccessfulCommands==1,"catalogue recipe scores: "+recipe.Name);
                    tick(.9f);
                    check(s.Inventory()[(int)recipe.Output]==3 && s.Discovered[i],"catalogue recipe arrives and unlocks: "+recipe.Name);
                }
                s.ResetMatch(42,0,120);s.AutoEatEnabled=true;
                var eatingCues=new List<EatingCue>();s.Eating+=cue=>eatingCues.Add(cue);
                add(FoodKind.Rice,8);add(FoodKind.Egg,4);add(FoodKind.Carrot,4);
                s.Submit(0,0,1,s.State.Epoch);tick(.02f);tick(.9f);
                check(s.Inventory()[(int)FoodKind.FriedRice]==4,"first chef's finished dish waits on the shared plate");
                add(FoodKind.Rice,2);add(FoodKind.Egg,1);add(FoodKind.Carrot,1);
                s.Submit(1,0,1,s.State.Epoch);tick(.02f);tick(.9f);
                check(s.Inventory()[(int)FoodKind.FriedRice]==5,"last chef's serving merges into the same plate");
                int firstScore=s.State.CyanScore,lastScore=s.State.OrangeScore;
                for(int i=0;i<9;i++)tick(1);
                check(eatingCues.Count==0 && s.Inventory()[(int)FoodKind.FriedRice]==5,"dishes are not eaten before ten seconds on the plate");
                for(int i=0;i<15 && eatingCues.Count==0;i++)tick(1);
                check(eatingCues.Count==1,"an eligible completed dish is selected at a random interval");
                var eaten=eatingCues[0];
                check(eaten.Player==1 && eaten.Recipe==0 && eaten.Count==5 && eaten.Points==5*Cookbook.All[0].Points,"eating credits the last cook for the whole shared platter");
                check(s.State.CyanScore==firstScore && s.State.OrangeScore==lastScore+eaten.Points && s.Inventory()[(int)FoodKind.FriedRice]==0,"eating removes all servings from shared stock");
                using(var q=em.CreateEntityQuery(typeof(Plate)))check(q.CalculateEntityCount()==0,"eaten dish and its platter disappear from the table");
                var cookedRows=s.MatchDishes();
                check(cookedRows.Length==2 && cookedRows[0].EatenPoints==0 && cookedRows[1].EatenCount==eaten.Count && cookedRows[1].EatenPoints==eaten.Points,"result ledger preserves who cooked and who earned the eating bonus");
                using(var q=em.CreateEntityQuery(typeof(Visual)))using(var all=q.ToComponentDataArray<Visual>(Allocator.Temp))
                {
                    int shower=0;foreach(var v in all)if(v.Mode==4){shower++;check(v.Kind==FoodKind.FriedRice && v.Player==1 && v.Recipe==0,"dish shower uses the eaten meal model and final cook color");}
                    check(shower>=60 && shower<=96,"eating launches a screen-filling but bounded three-dimensional dish shower");
                }
                s.AutoEatEnabled=false;
                for(int i=0;i<4;i++)tick(1);
                using(var q=em.CreateEntityQuery(typeof(Visual)))using(var all=q.ToComponentDataArray<Visual>(Allocator.Temp))
                {int shower=0;foreach(var v in all)if(v.Mode==4)shower++;check(shower==0,"eaten-dish shower expires without becoming recipe stock");}
                s.ResetMatch(42,0,120);check(s.MatchDishes().Length==0 && s.State.CyanScore==0 && s.State.OrangeScore==0,"rematch clears eating awards and scores");

            }
            Directory.CreateDirectory("Docs");File.WriteAllText("Docs/simulation-tests.txt","PASS: "+checks+" assertions\nContention, maximum batches, reservation, sharing, chaining, discovery, replay rejection, merging, deadline, reset, 10000 starting ingredients, lazy-Susan stock flow, uncapped rain and table-edge spill.\n");
            Debug.Log("FEAST_TEST_PASS checks="+checks);
        }
    }
}
