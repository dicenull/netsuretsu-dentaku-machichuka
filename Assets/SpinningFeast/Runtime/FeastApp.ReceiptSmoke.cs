using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpinningFeast
{
    public sealed partial class FeastApp
    {
        // Explicit opt-in. Start must include this flag in smoke BEFORE ResetMatch or loading saves.
        static bool ReceiptUISmokeRequested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-ui-smoke")>=0;

        [Serializable] sealed class ReceiptSmokeObservation
        {public string stage,phase,bookPage,resultPage;public int navigationEvents;public string screenshot;}
        [Serializable] sealed class ReceiptSmokeReport
        {
            public string status="RUNNING",error="",scope="Synthetic NavigationSubmitEvent -> real shared UI controls and match state. NOT OS pointer/keyboard delivery, real IME or target-device testing.";
            public string fixture="Discovery and result ledger fixtures are in-memory only; scores are synthetic for pagination coverage.";
            public bool savesDisabled,initialTitleFocusValid;public string initialFocusedElement="";public int runtimeErrors,navigationEvents;public List<ReceiptSmokeObservation> observations=new List<ReceiptSmokeObservation>();
        }
        ReceiptSmokeReport receiptSmokeReport;
        string receiptSmokePath;
        int receiptSmokeNavigationCount;

        IEnumerator ReceiptUISmoke()
        {
            receiptSmokeReport=new ReceiptSmokeReport();receiptSmokePath=Path.Combine(CapturePath(),"receipt-ui-smoke-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(receiptSmokePath);
            Exception failure=null;var stack=new Stack<IEnumerator>();stack.Push(ReceiptUISmokeSteps());
            // Flatten nested coroutines so exceptions from capture/waits produce a failure report.
            while(stack.Count>0)
            {
                object current=null;bool moved=false;
                try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception error){failure=error;break;}
                if(!moved){stack.Pop();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}
                yield return current;
            }
            receiptSmokeReport.runtimeErrors=runtimeErrors;receiptSmokeReport.navigationEvents=receiptSmokeNavigationCount;
            receiptSmokeReport.savesDisabled=smoke && !sim.SaveDiscoveries;
            bool passed=failure==null && runtimeErrors==0 && receiptSmokeReport.savesDisabled && receiptSmokeReport.initialTitleFocusValid;
            receiptSmokeReport.status=passed?"PASS":"FAIL";
            receiptSmokeReport.error=failure?.ToString() ?? (runtimeErrors>0?"Runtime error count was nonzero.":!receiptSmokeReport.initialTitleFocusValid?"Initial title focus was missing after layout.":"");
            File.WriteAllText(Path.Combine(receiptSmokePath,"receipt-ui-smoke.json"),JsonUtility.ToJson(receiptSmokeReport,true));
            File.WriteAllText(Path.Combine(receiptSmokePath,"receipt-ui-smoke.txt"),$"{receiptSmokeReport.status}\nSynthetic UI navigation events: {receiptSmokeNavigationCount}\nRuntime errors: {runtimeErrors}\nSaves disabled: {receiptSmokeReport.savesDisabled}\n{receiptSmokeReport.scope}\n{receiptSmokeReport.error}\n");
            if(passed)Debug.Log("FEAST_RECEIPT_UI_SMOKE_PASS "+receiptSmokeNavigationCount+" synthetic navigation events; OS input/IME not validated.");
            else Debug.LogError("FEAST_RECEIPT_UI_SMOKE_FAIL "+receiptSmokeReport.error);
            yield return null;Application.Quit(passed?0:1);
        }
        void ReceiptSmokeRequire(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("Receipt UI smoke: "+message);}
        IEnumerator ReceiptUISmokeSteps()
        {
            ReceiptSmokeRequire(smoke && !sim.SaveDiscoveries,"Start must disable saves before this test runs.");
            ReceiptSmokeRequire(receiptReady,"Shared UI not initialized.");
            ReceiptSmokeRequire(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null,"Screenshots require a graphical player, not -nographics.");
            receiptDocument.rootVisualElement.RegisterCallback<NavigationSubmitEvent>(_=>receiptSmokeNavigationCount++,TrickleDown.TrickleDown);
            yield return null;yield return null;
            var focused=receiptDocument.rootVisualElement.panel?.focusController.focusedElement as VisualElement;
            receiptSmokeReport.initialFocusedElement=focused==null?"<null>":focused.GetType().Name+"#"+focused.name;
            receiptSmokeReport.initialTitleFocusValid=focused!=null && titleUI.Contains(focused);
            Debug.Log("FEAST_RECEIPT_INITIAL_FOCUS "+receiptSmokeReport.initialFocusedElement+" valid="+receiptSmokeReport.initialTitleFocusValid);
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Title,"Initial Title phase missing.");
            yield return ReceiptSmokeCapture("01-title");
            ReceiptSmokePress(titleUI,"cpu-2");yield return null;
            ReceiptSmokeRequire(cpuLevel==2,"CPU button callback did not run.");
            // Do not toggle music: the existing audio component writes PlayerPrefs even in smoke mode.
            ReceiptSmokePress(titleUI,"start");yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Countdown,"Start button did not start Countdown.");
            yield return ReceiptSmokeCapture("02-countdown");
            float deadline=Time.realtimeSinceStartup+10;
            while(sim.State.Phase==MatchPhase.Countdown && Time.realtimeSinceStartup<deadline)yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Playing,"Countdown did not reach Playing.");
            yield return ReceiptSmokeCapture("03-playing");

            for(int i=0;i<Mathf.Min(40,sim.Discovered.Length);i++)sim.Discovered[i]=true;
            receiptDataDirty=true;ReceiptSmokePress(matchUI,"book-open");yield return null;
            ReceiptSmokeRequire(codex,"Book button did not open cookbook.");
            yield return ReceiptSmokeCapture("04-book-page-1");
            ReceiptSmokePress(bookUI,"book-next");yield return null;
            ReceiptSmokeRequire(bookPage==1 && receiptBookPage.text.StartsWith("2 /"),"Book next page did not bind.");
            ReceiptSmokeRequire(receiptRecipes[0].text==Cookbook.All[20].Name,"Second page recipe index mismatch.");
            yield return ReceiptSmokeCapture("05-book-page-2");
            ReceiptSmokePress(bookUI,"recipe-0");yield return null;
            ReceiptSmokeRequire(!codex && inputs[0]==Cookbook.All[20].Name,"Recipe selection did not fill shared input and close book.");
            ReceiptSmokePress(matchUI,"book-open");yield return null;
            ReceiptSmokePress(bookUI,"book-prev");yield return null;
            ReceiptSmokeRequire(bookPage==0,"Book previous page did not work.");
            ReceiptSmokePress(bookUI,"book-close");yield return null;
            ReceiptSmokeRequire(!codex,"Book close did not work.");
            SetDishInput("");ReceiptSmokePress(matchUI,"submit");yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Playing && inputs[0]=="","Empty submit changed match/input unexpectedly.");

            // Seed only a temporary ledger to cover unequal 7-vs-8 dishes and a partial final page.
            sim.MatchDishes();var ledger=sim.EntityManager.GetBuffer<CookedDish>(sim.MatchEntity);ledger.Clear();
            for(int p=0;p<2;p++)for(int i=0;i<7+p;i++)ledger.Add(new CookedDish{Player=p,Recipe=i,Count=i+1});
            var state=sim.State;state.CyanScore=12345;state.OrangeScore=9876;state.Remaining=.001f;
            sim.EntityManager.SetComponentData(sim.MatchEntity,state);
            deadline=Time.realtimeSinceStartup+5;
            while(sim.State.Phase!=MatchPhase.Result && Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Result && endingPlayed,"Result transition did not initialize presentation.");
            ReceiptSmokeRequire(ResultPageCount()==2 && receiptResultScores[0].text==(12345).ToString("N0"),"Result fixture did not bind.");
            yield return ReceiptSmokeCapture("06-result-page-1");
            ReceiptSmokePress(resultUI,"result-next");yield return null;
            ReceiptSmokeRequire(resultPage==1 && receiptDishNames[0,0].text==Cookbook.All[6].Name && receiptDishNames[0,1].text=="","Partial result page mismatch.");
            yield return ReceiptSmokeCapture("07-result-page-2");
            ReceiptSmokePress(resultUI,"result-prev");yield return null;
            ReceiptSmokeRequire(resultPage==0,"Result previous page did not work.");
            ReceiptSmokePress(resultUI,"replay");yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Countdown && resultPage==0 && inputs[0]=="","Replay did not reset presentation.");
            yield return ReceiptSmokeCapture("08-replay");

            // Second empty result exercises zero-row rendering and the actual title button callback.
            sim.ResetMatch(initial:0,phase:MatchPhase.Result);endingPlayed=false;yield return null;yield return null;
            ReceiptSmokeRequire(ResultPageCount()==1 && !receiptResultNext.enabledSelf,"Empty result paging must be disabled.");
            ReceiptSmokePress(resultUI,"title");yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Title,"Title button did not return to menu.");
            yield return ReceiptSmokeCapture("09-title-return");
            ReceiptSmokeRequire(receiptSmokeNavigationCount>=13,"Navigation events did not traverse the actual UI root.");
        }
        void ReceiptSmokePress(VisualElement screen,string name)
        {
            Button button=Required<Button>(screen,name);
            ReceiptSmokeRequire(button.panel!=null && button.enabledInHierarchy && button.visible,"Button unavailable: "+name);
            button.Focus();
            using(var submit=NavigationSubmitEvent.GetPooled())button.SendEvent(submit);
        }
        IEnumerator ReceiptSmokeCapture(string stage)
        {
            yield return null;yield return new WaitForEndOfFrame();
            string file=stage+".png",path=Path.Combine(receiptSmokePath,file);ScreenCapture.CaptureScreenshot(path);
            float until=Time.realtimeSinceStartup+5;
            while(!File.Exists(path) && Time.realtimeSinceStartup<until)yield return null;
            ReceiptSmokeRequire(File.Exists(path),"Screenshot missing: "+file);
            receiptSmokeReport.observations.Add(new ReceiptSmokeObservation{stage=stage,phase=sim.State.Phase.ToString(),
                bookPage=receiptBookPage.text,resultPage=receiptResultPage.text,navigationEvents=receiptSmokeNavigationCount,screenshot=file});
        }
    }
}
