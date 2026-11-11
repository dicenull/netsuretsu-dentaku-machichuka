using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpinningFeast
{
    // Local prototype diagnostics only. No network, UI, or sampling of user text.
    [DefaultExecutionOrder(200)]
    public sealed class FeastFrameProfile : MonoBehaviour
    {
        [Serializable] public sealed class Distribution
        {public float median,p95,p99,maximum;}
        [Serializable] public sealed class Report
        {
            public string utc,unity,graphics;
            public int width,height,samples,maximumFood,maximumStock,peakParticles,successfulCommands,collections;
            public long managedBytes;
            public Distribution frameMilliseconds,viewCpuMilliseconds,effectsCpuMilliseconds;
        }
        readonly List<float> frames=new List<float>(15000),viewCpu=new List<float>(15000),effectsCpu=new List<float>(15000);
        FeastSystem simulation;FeastView view;FeastEffects effects;
        int epoch=-1,maximumFood,maximumStock,gcStart;
        bool collecting;
        public void Initialize(FeastSystem sim,FeastView renderer,FeastEffects particles)
        {simulation=sim;view=renderer;effects=particles;}
        static Distribution Summarize(List<float> values)
        {
            if(values.Count==0)return new Distribution();values.Sort();
            return new Distribution{median=values[values.Count/2],p95=values[Mathf.Min(values.Count-1,Mathf.FloorToInt(values.Count*.95f))],p99=values[Mathf.Min(values.Count-1,Mathf.FloorToInt(values.Count*.99f))],maximum=values[values.Count-1]};
        }
        void LateUpdate()
        {
            if(simulation==null || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var state=simulation.State;
            if(state.Epoch!=epoch)
            {
                epoch=state.Epoch;frames.Clear();viewCpu.Clear();effectsCpu.Clear();maximumFood=maximumStock=0;
                gcStart=GC.CollectionCount(0);collecting=state.Phase==MatchPhase.Playing;
            }
            if(!collecting)return;
            if(state.Phase==MatchPhase.Playing)
            {
                maximumFood=Mathf.Max(maximumFood,view.VisibleFoodCount);
                maximumStock=Mathf.Max(maximumStock,view.TotalFoodCount);
                if(state.Elapsed>2 && frames.Count<30000)
                {frames.Add(Time.unscaledDeltaTime*1000);viewCpu.Add(view.LastCpuMilliseconds);effectsCpu.Add(effects.LastCpuMilliseconds);}
            }
            else if(state.Phase==MatchPhase.Result)
            {
                collecting=false;
                var report=new Report {utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,samples=frames.Count,maximumFood=maximumFood,maximumStock=maximumStock,peakParticles=effects.PeakParticles,successfulCommands=simulation.SuccessfulCommands,collections=GC.CollectionCount(0)-gcStart,managedBytes=GC.GetTotalMemory(false),frameMilliseconds=Summarize(frames),viewCpuMilliseconds=Summarize(viewCpu),effectsCpuMilliseconds=Summarize(effectsCpu)};
                try{File.WriteAllText(Path.Combine(Application.persistentDataPath,"last-match-profile.json"),JsonUtility.ToJson(report,true));}
                catch(IOException error){Debug.LogWarning("Could not write local frame profile: "+error.Message);}
            }
            else collecting=false;
        }
    }
}
