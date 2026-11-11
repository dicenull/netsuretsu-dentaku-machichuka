using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace SpinningFeast
{
    // Opt-in, isolated runtime verification. It never reads or writes discoveries.
    public sealed partial class FeastApp
    {
        IEnumerator EffectsProbe()
        {
            var report=new System.Text.StringBuilder();int checks=0,failures=0;
            Action<bool,string> check=(ok,label)=>{checks++;if(!ok)failures++;report.AppendLine((ok?"PASS ":"FAIL ")+label);};
            var em=sim.EntityManager;
            Action<FoodKind,int> add=(kind,count)=>
            {
                for(int i=0;i<count;i++)
                {
                    float a=i*2.39996f;var pos=new Unity.Mathematics.float3(Mathf.Sin(a)*5.5f,.8f,Mathf.Cos(a)*5.5f);
                    Entity entity=em.CreateEntity(typeof(Stock),typeof(Visual));
                    em.SetComponentData(entity,new Stock {Kind=kind,Quantity=1});
                    em.SetComponentData(entity,new Visual {Kind=kind,Position=pos,Start=pos,Floor=.8f,Scale=.45f,Seed=a});
                }
            };
            yield return null;
            sim.RainEnabled=false;sim.ResetMatch(initial:0);ResetPresentation();sound.Begin();
            add(FoodKind.Rice,120);add(FoodKind.Egg,180);add(FoodKind.Carrot,60);
            inputs[0]="chahan";yield return new WaitForSeconds(.3f);
            check(view.PreviewServings==60 && view.HighlightedFoodCount==240,"typing previews the exact batch while highlighting all allocated stock");
            check(sim.State.CyanScore==0 && sim.Inventory()[(int)FoodKind.Rice]==120,"preview never reserves or consumes ingredients");
            inputs[0]="";yield return new WaitForSeconds(.05f);
            check(view.PreviewServings==0 && view.HighlightedFoodCount==0,"clearing input clears anticipation glow");
            Send(0,0);yield return new WaitForSeconds(.25f);
            check(effects.CookingEvents==1 && effects.ActiveParticles>0,"bulk cooking emits its spark trail");
            check(effects.ArrivalEvents==0,"arrival effects wait for food to land");
            yield return new WaitForSeconds(.85f);
            check(effects.ArrivalEvents==1 && effects.DiscoveryEvents==1,"new meal gets one discovery burst");
            check(sim.State.CyanScore==6000 && sim.Inventory()[(int)FoodKind.FriedRice]==60,"effects preserve sixty exact servings and points");
            Send(0,0);yield return new WaitForSeconds(.1f);
            int ghosts=0;
            using(var query=em.CreateEntityQuery(typeof(Visual)))using(var items=query.ToComponentDataArray<Visual>(Allocator.Temp))
                foreach(var v in items)if(v.Mode==3)ghosts++;
            check(ghosts>0 && effects.CookingEvents==1,"shortage creates holograms without success particles");
            Send(1,6);yield return new WaitForSeconds(1.1f);
            check(effects.ChainEvents==1 && effects.ArrivalEvents==2 && effects.DiscoveryEvents==2,"opponent reuses shared dish with a chain bridge and discovery");
            check(sim.State.OrangeScore==10800,"chain presentation leaves scoring intact");
            add(FoodKind.Rice,10);add(FoodKind.Egg,5);add(FoodKind.Carrot,5);
            Send(0,0);yield return new WaitForSeconds(1.1f);
            check(effects.DiscoveryEvents==2 && effects.ArrivalEvents==3,"repeat recipe does not repeat discovery fanfare");
            int cyanScore=sim.State.CyanScore,orangeScore=sim.State.OrangeScore;
            effects.Burst(Vector3.up,FeastView.Cyan,10000);
            check(effects.ActiveParticles==FeastEffects.MaxParticles,"particle pool stays bounded under a burst of ten thousand");
            check(sim.State.CyanScore==cyanScore && sim.State.OrangeScore==orangeScore,"particle saturation never changes points");
            yield return new WaitForSeconds(3.2f);
            check(effects.ActiveParticles<100,"transient particles drain after the burst");
            for(int i=0;i<8;i++)
            {
                sim.ResetMatch(initial:3000);ResetPresentation();
                check(effects.ActiveParticles==0 && effects.CookingEvents==0 && effects.ArrivalEvents==0,"rematch clears effect state "+i);
                yield return null;
            }
            sim.RainEnabled=true;sim.ResetMatch();ResetPresentation();sound.Begin();
            var cameraPosition=view.ViewCamera.transform.position;var cameraRotation=view.ViewCamera.transform.rotation;
            var frames=new List<float>(15000);int recipeCursor=0,attempts=0;float nextCook=1;
            while(sim.State.Phase==MatchPhase.Playing)
            {
                var state=sim.State;
                if(state.Elapsed>2)frames.Add(Time.unscaledDeltaTime*1000);
                if(state.Elapsed>=nextCook)
                {
                    nextCook=state.Elapsed+1.3f;
                    var inventory=sim.Inventory();
                    for(int i=0;i<Cookbook.All.Length;i++)
                    {
                        int candidate=(recipeCursor+i)%Cookbook.All.Length;
                        if(RecipeSolver.Solve(Cookbook.All[candidate],inventory).Servings<=0)continue;
                        Send(attempts%2,candidate);attempts++;recipeCursor=(candidate+1)%Cookbook.All.Length;break;
                    }
                }
                yield return null;
            }
            yield return new WaitForSeconds(3);
            check(sim.State.Phase==MatchPhase.Result,"full 120-second match reaches result");
            check(sim.SuccessfulCommands>=40 && effects.CookingEvents==sim.SuccessfulCommands,"all successful commands receive cooking presentation");
            check(effects.ArrivalEvents==sim.SuccessfulCommands,"all last valid servings finish their arrival presentation");
            check(effects.PeakParticles<=FeastEffects.MaxParticles,"full-match presentation remains within particle budget");
            check(sound.CountdownTicks==10,"countdown sounds once per final second");
            check(view.ViewCamera.transform.position==cameraPosition && view.ViewCamera.transform.rotation==cameraRotation,"camera stays fixed through all celebrations");
            report.AppendLine("Successful commands: "+sim.SuccessfulCommands);
            report.AppendLine("Peak particles: "+effects.PeakParticles);
            frames.Sort();if(frames.Count>0)report.AppendLine("Frame interval median / p95 (ms): "+frames[frames.Count/2].ToString("0.00")+" / "+frames[Mathf.Min(frames.Count-1,Mathf.FloorToInt(frames.Count*.95f))].ToString("0.00"));
            report.AppendLine("Graphics: "+SystemInfo.graphicsDeviceName+"; batch mode: "+Application.isBatchMode);
            ShowTitle();yield return null;
            check(effects.ActiveParticles<=6 && effects.CookingEvents==0 && effects.ArrivalEvents==0 && sim.State.CyanScore==0 && sim.State.OrangeScore==0,"return to title clears match effects while keeping subtle lantern ambience");
            check(runtimeErrors==0,"no runtime errors");
            report.AppendLine("Result: "+(failures==0?"PASS":"FAIL")+"; assertions: "+checks+"; failures: "+failures+"; errors: "+runtimeErrors);
            File.WriteAllText(Path.Combine(CapturePath(),"effects-runtime-tests.txt"),report.ToString());
            Debug.Log("FEAST_EFFECTS_PROBE "+(failures==0?"PASS":"FAIL"));Application.Quit(failures==0?0:1);
        }
    }
}
