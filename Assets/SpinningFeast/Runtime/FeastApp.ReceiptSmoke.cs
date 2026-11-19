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
        {public string stage,phase,bookPage,checkoutTab;public int navigationEvents;public string screenshot;}
        [Serializable] sealed class ReceiptSmokeReport
        {
            public string status="RUNNING",error="",scope="Synthetic NavigationSubmitEvent -> real shared UI controls and match state. NOT OS pointer/keyboard delivery, real IME or target-device testing.";
            public string fixture="Discovery and result ledger fixtures are in-memory only; scores are synthetic for tab and scroll coverage.";
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
            ReceiptSmokeRequire(Required<Label>(titleUI,"cpu-heading").text==FeastPlayerNames.OpponentStrength,"Title opponent name differs.");
            yield return ReceiptSmokeCapture("01-title");
            ReceiptSmokePress(titleUI,"settings-open");yield return null;
            var settings=Required<VisualElement>(titleUI,"settings-panel");
            ReceiptSmokeRequire(settings.resolvedStyle.display==DisplayStyle.Flex && !sim.IsPaused,"Title settings should appear without pausing a match.");
            yield return ReceiptSmokeCapture("01-settings");
            ReceiptSmokePress(titleUI,"settings-close");yield return null;
            ReceiptSmokeRequire(settings.resolvedStyle.display==DisplayStyle.None,"Settings did not close.");
            ReceiptSmokePress(titleUI,"cpu-2");yield return null;
            ReceiptSmokeRequire(cpuLevel==2,"CPU button callback did not run.");
            // Do not toggle music: the existing audio component writes PlayerPrefs even in smoke mode.
            ReceiptSmokePress(titleUI,"start");yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Countdown,"Start button did not start Countdown.");
            yield return ReceiptSmokeCapture("02-countdown");
            ReceiptSmokePress(matchUI,"settings-open");
            yield return ReceiptSmokeFrozenState();
            ReceiptSmokePress(matchUI,"settings-close");
            ReceiptSmokeRequire(!sim.IsPaused,"Closing countdown settings should resume play.");
            float deadline=Time.realtimeSinceStartup+10;
            while(sim.State.Phase==MatchPhase.Countdown && Time.realtimeSinceStartup<deadline)yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Playing,"Countdown did not reach Playing.");
            for(int p=0;p<2;p++)ReceiptSmokeRequire(Required<Label>(matchUI,"hud-static-"+(p*2)).text==FeastPlayerNames.For(p),"HUD player name differs.");
            ReceiptSmokeCheckCamera();
            yield return new WaitForSecondsRealtime(.31f);
            yield return ReceiptSmokeCapture("03-playing");
            if(receiptLayout.Portrait)yield return ReceiptSmokeKeyboardFixed();
            // Visual reaction is fired from the same accepted-cook path as live gameplay.
            PlayReceiptStamp(1);
            ReceiptSmokeRequire(orderStampStart<0,"CPU cook shouldn't display the local order stamp.");
            PlayReceiptStamp(0);
            ReceiptSmokeRequire(orderStampStart>=0,"Accepted order did not stamp.");
            yield return ReceiptSmokeCapture("03-order-stamp");
            float firstStamp=orderStampStart;
            PlayReceiptStamp(0);
            ReceiptSmokeRequire(orderStampStart>firstStamp,"A second accepted order should restart the stamp.");
            yield return new WaitForSecondsRealtime(OrderStampDuration+.04f);
            ReceiptSmokeRequire(orderStampStart<0,"Order stamp did not settle.");

            ReceiptSmokeSetRemaining(60.22f);
            ReceiptSmokePress(matchUI,"settings-open");
            yield return ReceiptSmokeFrozenState();
            ReceiptSmokeRequire(deadlineCalled==0 && sim.State.Remaining>60,"Paused time must not fire the one-minute call.");
            yield return ReceiptSmokeCapture("03-paused-settings");
            float pausedAt=sim.State.Remaining;
            ReceiptSmokePress(matchUI,"settings-close");
            ReceiptSmokeRequire(sim.State.Remaining==pausedAt && !sim.IsPaused,"Resume applied a time jump.");
            yield return null;
            deadline=Time.realtimeSinceStartup+1.5f;
            while(deadlineCalled==0 && Time.realtimeSinceStartup<deadline)yield return null;
            ReceiptSmokeRequire(deadlineCalled==1 && sim.State.Remaining>59,"One-minute HUD call should occur once after resume.");
            ReceiptSmokeSetRemaining(29);yield return null;yield return null;
            ReceiptSmokeRequire(deadlineCalled==2,"Thirty-second HUD call missing.");
            yield return new WaitForSecondsRealtime(.14f);
            yield return ReceiptSmokeCapture("03-last-order");
            ReceiptSmokeSetRemaining(9);yield return null;yield return null;
            ReceiptSmokeRequire(deadlineCalled==3,"Ten-second HUD call missing.");

            for(int i=0;i<Mathf.Min(40,sim.Discovered.Length);i++)sim.Discovered[i]=true;
            ReceiptSmokeSetRemaining(.18f);
            receiptDataDirty=true;ReceiptSmokePress(matchUI,"book-open");yield return null;
            ReceiptSmokeRequire(codex && sim.IsPaused,"Book did not pause the live match.");
            yield return ReceiptSmokeFrozenState();
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Playing,"Match ended behind the book.");
            ReceiptSmokeSetRemaining(8); // Leave room for the remaining navigation fixtures.
            yield return ReceiptSmokeCapture("04-book-page-1");
            ReceiptSmokePress(bookUI,"book-next");yield return null;
            ReceiptSmokeRequire(bookPage==1 && receiptBookPage.text.StartsWith("2 /"),"Book next page did not bind.");
            ReceiptSmokeRequire(receiptRecipes[0].text==Cookbook.All[20].Name,"Second page recipe index mismatch.");
            yield return ReceiptSmokeCapture("05-book-page-2");
            ReceiptSmokePress(bookUI,"recipe-0");yield return null;
            ReceiptSmokeRequire(!codex && !sim.IsPaused && inputs[0]==Cookbook.All[20].Name,"Recipe selection should preserve input, close the book, and unpause.");
            ReceiptSmokePress(matchUI,"book-open");yield return null;
            ReceiptSmokePress(bookUI,"book-prev");yield return null;
            ReceiptSmokeRequire(bookPage==0,"Book previous page did not work.");
            ReceiptSmokePress(bookUI,"book-close");yield return null;
            ReceiptSmokeRequire(!codex && !sim.IsPaused,"Repeated book close did not resume the match.");
            SetDishInput("");ReceiptSmokePress(matchUI,"submit");yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Playing && inputs[0]=="","Empty submit changed match/input unexpectedly.");

            // A temporary fixture: long unequal receipts with disjoint dishes.
            var ledger=sim.EntityManager.GetBuffer<CookedDish>(sim.MatchEntity);ledger.Clear();
            for(int p=0;p<2;p++)for(int i=0;i<(p==0?24:17);i++)
                ledger.Add(new CookedDish{Player=p,Recipe=p*40+i,Count=i+1});
            var state=sim.State;state.CyanScore=12345;state.OrangeScore=9876;state.Remaining=.001f;
            sim.EntityManager.SetComponentData(sim.MatchEntity,state);
            deadline=Time.realtimeSinceStartup+5;
            while(sim.State.Phase!=MatchPhase.Result && Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Result && endingPlayed,"Result transition did not initialize presentation.");
            ReceiptSmokeRequire(checkoutPlayer==0 && checkoutScores[0].text==(12345).ToString("N0") &&
                checkoutScores[1].text==(9876).ToString("N0"),"Result must open on self and bind both scores.");
            for(int p=0;p<2;p++)ReceiptSmokeRequire(checkoutTabs[p].text==FeastPlayerNames.For(p) &&
                Required<Label>(checkoutScroll[p],"checkout-owner").text==checkoutTabs[p].text &&
                checkoutOutcomes[p].text==FeastPlayerNames.Outcome(0),"Receipt player name differs.");
            ReceiptSmokeRequire(checkoutDishes[0].childCount==24 && checkoutDishes[1].childCount==17,
                "Receipt lists must include all dishes without pagination.");
            ReceiptSmokeRequire(checkoutDishes[0][23].Q<Label>("checkout-dish").text==Cookbook.All[23].Name &&
                checkoutDishes[1][16].Q<Label>("checkout-dish").text==Cookbook.All[56].Name,
                "A player's receipt must not contain the other player's dishes.");
            ReceiptSmokeRequire(receiptFeedStart>=0,"Receipt did not begin feeding when Result opened.");
            yield return new WaitForSecondsRealtime(ReceiptFeedDuration+.07f);
            ReceiptSmokeRequire(receiptFeedStart<0 && deadlineStart<0,"Receipt feed or remaining-time call did not settle.");
            var self=checkoutScroll[0];var rival=checkoutScroll[1];
            ReceiptSmokeRequire(self.verticalScrollerVisibility==ScrollerVisibility.Hidden &&
                rival.verticalScrollerVisibility==ScrollerVisibility.Hidden && self.verticalScroller.highValue>80,
                "Receipt needs overflow without a visible scroller.");
            yield return ReceiptSmokeCapture("06-checkout-self-top");
            float bottom=self.verticalScroller.highValue;
            self.scrollOffset=new Vector2(0,bottom);yield return null;
            ReceiptSmokeRequire(self.scrollOffset.y>80 &&
                self.Q<Label>("checkout-total").worldBound.yMax<=self.contentViewport.worldBound.yMax+1,
                "Full-receipt scroll should reach totals.");
            yield return ReceiptSmokeCapture("06-checkout-self-bottom");
            ReceiptSmokePress(resultUI,"checkout-tab-1");yield return null;yield return null;
            ReceiptSmokeRequire(checkoutPlayer==1 && rival.scrollOffset.y<1 && receiptFeedStart<0 &&
                self.resolvedStyle.visibility==Visibility.Hidden && rival.resolvedStyle.visibility==Visibility.Visible,
                "Opponent tab should open at its own top without replaying the feed.");
            yield return ReceiptSmokeCapture("07-checkout-rival-top");
            rival.scrollOffset=new Vector2(0,92);yield return null;
            ReceiptSmokePress(resultUI,"checkout-tab-0");yield return null;yield return null;
            ReceiptSmokeRequire(checkoutPlayer==0 && Mathf.Abs(self.scrollOffset.y-bottom)<1,
                "Self receipt should preserve its scroll position.");
            ReceiptSmokePress(resultUI,"checkout-tab-1");yield return null;yield return null;
            ReceiptSmokeRequire(Mathf.Abs(rival.scrollOffset.y-92)<1,
                "Opponent receipt should preserve its own scroll position.");
            ReceiptSmokePress(resultUI,"replay");yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Countdown && checkoutPlayer==0 && inputs[0]=="",
                "Replay did not reset presentation and receipt tab.");

            sim.ResetMatch(initial:0,phase:MatchPhase.Result);endingPlayed=false;yield return null;yield return null;
            ReceiptSmokeRequire(checkoutPlayer==0 && self.scrollOffset.y<1 &&
                checkoutDishes[0].Q<Label>("checkout-empty")!=null && checkoutDishes[1].Q<Label>("checkout-empty")!=null,
                "New empty receipts must not keep previous rows or scroll position.");
            ReceiptSmokePress(resultUI,"title");yield return null;
            ReceiptSmokeRequire(sim.State.Phase==MatchPhase.Title,"Title button did not return to menu.");
            yield return ReceiptSmokeCapture("09-title-return");
            ReceiptSmokeRequire(receiptSmokeNavigationCount>=20,"Navigation events did not traverse the actual UI root.");
        }
        IEnumerator ReceiptSmokeFrozenState()
        {
            ReceiptSmokeRequire(sim.IsPaused,"A match panel should pause immediately.");
            var before=sim.State;
            var stock=sim.Inventory();
            int turns=cpuTurn,accepted=sim.SuccessfulCommands,rejected=sim.RejectedCommands;
            int pending=sim.EntityManager.GetBuffer<CookCommand>(sim.MatchEntity).Length;
            bool oldSmoke=smoke;float oldBot=botAt;
            try
            {
                // Make the CPU due if its gate regresses, without touching any saved settings.
                smoke=false;botAt=before.Elapsed-.05f;
                Send(0,0);sim.Submit(1,0,900,before.Epoch);
                yield return new WaitForSecondsRealtime(.34f);
                var after=sim.State;
                ReceiptSmokeRequire(sim.IsPaused && before.Phase==after.Phase && before.Remaining==after.Remaining &&
                    before.CountdownRemaining==after.CountdownRemaining && before.Elapsed==after.Elapsed &&
                    before.Angle==after.Angle && before.CyanScore==after.CyanScore && before.OrangeScore==after.OrangeScore,
                    "Clock, score or board advanced behind a panel.");
                ReceiptSmokeRequire(cpuTurn==turns && accepted==sim.SuccessfulCommands && rejected==sim.RejectedCommands &&
                    sim.EntityManager.GetBuffer<CookCommand>(sim.MatchEntity).Length==pending,"Orders or CPU advanced during pause.");
                if(after.Phase==MatchPhase.Playing)
                {
                    int shown=Mathf.CeilToInt(before.Remaining);
                    ReceiptSmokeRequire(receiptTimer.text==$"{shown/60:00}:{shown%60:00}","Visible clock changed during pause.");
                }
                var afterStock=sim.Inventory();
                for(int i=0;i<stock.Length;i++)ReceiptSmokeRequire(stock[i]==afterStock[i],"Stock changed during pause.");
            }
            finally {smoke=oldSmoke;botAt=oldBot;}
        }
        void ReceiptSmokeSetRemaining(float value)
        {
            var state=sim.State;state.Remaining=value;
            sim.EntityManager.SetComponentData(sim.MatchEntity,state);
        }
        void ReceiptSmokeCheckCamera()
        {
            var camera=view.ViewCamera;
            if(!receiptLayout.Portrait)
            {
                ReceiptSmokeRequire((camera.transform.position-new Vector3(0,13.6f,-17.8f)).sqrMagnitude<.0001f,"Wide camera position changed.");
                return;
            }
            const float r=FeastPortraitFrame.TableRadius;
            var center=new Vector3(0,.3f,0);
            Vector3 right=camera.transform.right*r,depth=camera.transform.forward*FeastPortraitFrame.ArenaExtent;
            Vector3 lo=camera.WorldToScreenPoint(center-right-depth),hi=camera.WorldToScreenPoint(center+right+depth);
            var safe=FeastKeyboardViewport.Read().SafeArea;
            ReceiptSmokeRequire(lo.x>=safe.xMin-.2f && hi.x<=safe.xMax+.2f &&
                hi.x-lo.x>=safe.width*.91f && lo.z>camera.nearClipPlane && hi.z<camera.farClipPlane,
                "Portrait table should fill nearly the safe width without side crop.");
            ReceiptSmokeRequire(camera.WorldToScreenPoint(center).y>Screen.height*.55f,"Table should center in the upper screen.");
            Ray touch=camera.ScreenPointToRay(camera.WorldToScreenPoint(center+right*.4f));
            ReceiptSmokeRequire(Vector3.Cross(center+right*.4f-touch.origin,touch.direction).sqrMagnitude<.0001f,
                "Camera touch ray no longer matches the world projection.");
        }
        IEnumerator ReceiptSmokeKeyboardFixed()
        {
            var camera=view.ViewCamera;
            var originalCamera=camera.transform.position;
            float originalScale=camera.orthographicSize,dockTop=inputDock.worldBound.yMin;
            Rect originalHUD=receiptStage.worldBound;
            try
            {
                FeastKeyboardViewport.SetSmokeKeyboard(Screen.height*.54f);
                yield return null;yield return null;yield return null;
                ReceiptSmokeRequire(receiptKeyboardOpen && inputDock.worldBound.yMin<dockTop-20,
                    "The input dock did not move above the simulated keyboard.");
                ReceiptSmokeRequire(bottomActions.resolvedStyle.display==DisplayStyle.Flex &&
                    receiptStage.worldBound==originalHUD,"The keyboard changed the HUD layout.");
                ReceiptSmokeRequire((camera.transform.position-originalCamera).sqrMagnitude<.0001f &&
                    Mathf.Abs(camera.orthographicSize-originalScale)<.0001f,"The keyboard moved or rescaled the board.");
                yield return ReceiptSmokeCapture("03-simulated-ime");
            }
            finally {FeastKeyboardViewport.SetSmokeKeyboard(null);}
            yield return null;yield return null;
            ReceiptSmokeRequire(!receiptKeyboardOpen && Mathf.Abs(inputDock.worldBound.yMin-dockTop)<1,
                "The input dock did not return to its original position after the keyboard.");
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
            if(stage.StartsWith("01-") || stage.StartsWith("09-"))
                yield return new WaitForSecondsRealtime(.32f);
            yield return null;yield return new WaitForEndOfFrame();
            string file=stage+".png",path=Path.Combine(receiptSmokePath,file);
            // Android CaptureScreenshot prepends persistentDataPath (unlike the Mac player).
            string capture=Application.platform==RuntimePlatform.Android?Path.GetRelativePath(Application.persistentDataPath,path):path;
            ScreenCapture.CaptureScreenshot(capture);
            float until=Time.realtimeSinceStartup+5;
            while(!File.Exists(path) && Time.realtimeSinceStartup<until)yield return null;
            ReceiptSmokeRequire(File.Exists(path),"Screenshot missing: "+file);
            receiptSmokeReport.observations.Add(new ReceiptSmokeObservation{stage=stage,phase=sim.State.Phase.ToString(),
                bookPage=receiptBookPage.text,checkoutTab=FeastPlayerNames.For(checkoutPlayer),navigationEvents=receiptSmokeNavigationCount,screenshot=file});
        }
    }
}
