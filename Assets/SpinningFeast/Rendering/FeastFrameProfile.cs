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
            public string utc,unity,graphics,graphicsApi,deviceModel,operatingSystem;
            public bool mobileShader,gpuTimingAvailable,impostorsReady;
            public int peakImpostors,peakSmallGeometry;
            public bool smallGeometry;
            public long peakVertexInputBytes;
            public long peakSubmittedFoodTriangles,peakSubmittedFoodVertices,peakSubmittedMatrixBytes;
            public float peakShadowQuadScreenCoverage,framesOver33msPercent;
            public int peakSubmittedDrawCalls,peakSubmittedShadowInstances;
            public Distribution gpuMilliseconds,submittedFoodTriangles,submittedDrawCalls;
            public int targetFrameRate;
            public int width,height,samples,maximumFood,maximumStock,peakParticles,successfulCommands,collections;
            public long managedBytes;
            public Distribution frameMilliseconds,viewCpuMilliseconds,effectsCpuMilliseconds;
        }
        readonly List<float> frames=new List<float>(15000),viewCpu=new List<float>(15000),effectsCpu=new List<float>(15000);
        readonly List<float> gpuFrames=new List<float>(15000),triangleCounts=new List<float>(15000),drawCounts=new List<float>(15000);
        readonly FrameTiming[] frameTiming=new FrameTiming[1];
        long peakVertexBytes;
        long peakTriangles,peakVertices,peakMatrixBytes;float peakShadowCoverage;int framesOver33;int peakDraws,peakShadows,peakImpostors,peakSmallGeometry;
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
        public void SaveSnapshot(string path)
        {
                var report=new Report {utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),deviceModel=SystemInfo.deviceModel,operatingSystem=SystemInfo.operatingSystem,mobileShader=view.UsesMobileShader,impostorsReady=view.ImpostorsReady,peakImpostors=peakImpostors,peakSmallGeometry=peakSmallGeometry,smallGeometry=view.UsesSmallGeometry,gpuTimingAvailable=gpuFrames.Count>0,peakVertexInputBytes=peakVertexBytes,peakSubmittedFoodTriangles=peakTriangles,peakSubmittedFoodVertices=peakVertices,peakShadowQuadScreenCoverage=peakShadowCoverage,framesOver33msPercent=frames.Count>0?100f*framesOver33/frames.Count:0,peakSubmittedMatrixBytes=peakMatrixBytes,peakSubmittedDrawCalls=peakDraws,peakSubmittedShadowInstances=peakShadows,gpuMilliseconds=Summarize(gpuFrames),submittedFoodTriangles=Summarize(triangleCounts),submittedDrawCalls=Summarize(drawCounts),targetFrameRate=Application.targetFrameRate,width=Screen.width,height=Screen.height,samples=frames.Count,maximumFood=maximumFood,maximumStock=maximumStock,peakParticles=effects.PeakParticles,successfulCommands=simulation.SuccessfulCommands,collections=GC.CollectionCount(0)-gcStart,managedBytes=GC.GetTotalMemory(false),frameMilliseconds=Summarize(frames),viewCpuMilliseconds=Summarize(viewCpu),effectsCpuMilliseconds=Summarize(effectsCpu)};
                try{File.WriteAllText(path,JsonUtility.ToJson(report,true));}
                catch(IOException error){Debug.LogWarning("Could not write local frame profile: "+error.Message);}
        }
        void LateUpdate()
        {
            if(simulation==null || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var state=simulation.State;
            if(state.Epoch!=epoch)
            {
                epoch=state.Epoch;frames.Clear();viewCpu.Clear();effectsCpu.Clear();maximumFood=maximumStock=0;gpuFrames.Clear();triangleCounts.Clear();drawCounts.Clear();peakTriangles=peakVertices=peakMatrixBytes=peakVertexBytes=0;peakShadowCoverage=0;framesOver33=0;peakDraws=peakShadows=peakImpostors=peakSmallGeometry=0;
                gcStart=GC.CollectionCount(0);collecting=state.Phase==MatchPhase.Playing;
            }
            // A real match enters Playing after Countdown, not necessarily at epoch reset.
            if(!collecting && state.Phase==MatchPhase.Playing){collecting=true;gcStart=GC.CollectionCount(0);}
            if(!collecting)return;
            if(state.Phase==MatchPhase.Playing)
            {
                maximumFood=Mathf.Max(maximumFood,view.VisibleFoodCount);
                maximumStock=Mathf.Max(maximumStock,view.TotalFoodCount);
                peakVertexBytes=Math.Max(peakVertexBytes,view.SubmittedVertexInputBytes);peakTriangles=Math.Max(peakTriangles,view.SubmittedFoodTriangles);peakVertices=Math.Max(peakVertices,view.SubmittedFoodVertices);peakShadowCoverage=Math.Max(peakShadowCoverage,view.ShadowQuadScreenCoverage);peakMatrixBytes=Math.Max(peakMatrixBytes,view.SubmittedMatrixBytes);
                peakSmallGeometry=Math.Max(peakSmallGeometry,view.SmallGeometryCount);peakImpostors=Math.Max(peakImpostors,view.ImpostorCount);peakDraws=Math.Max(peakDraws,view.SubmittedDrawCalls);peakShadows=Math.Max(peakShadows,view.SubmittedShadowInstances);
                if(FrameTimingManager.IsFeatureEnabled())FrameTimingManager.CaptureFrameTimings();
                if(state.Elapsed>2 && frames.Count<30000)
                {if(Time.unscaledDeltaTime>0.0335f)framesOver33++;frames.Add(Time.unscaledDeltaTime*1000);viewCpu.Add(view.LastCpuMilliseconds);effectsCpu.Add(effects.LastCpuMilliseconds);triangleCounts.Add(view.SubmittedFoodTriangles);drawCounts.Add(view.SubmittedDrawCalls);
                    if(FrameTimingManager.IsFeatureEnabled() && FrameTimingManager.GetLatestTimings(1,frameTiming)>0 && frameTiming[0].gpuFrameTime>0)gpuFrames.Add((float)frameTiming[0].gpuFrameTime);}
            }
            else if(state.Phase==MatchPhase.Result)
            {
                collecting=false;
                SaveSnapshot(Path.Combine(Application.persistentDataPath,"last-match-profile.json"));
            }
            else collecting=false;
        }
    }
}
