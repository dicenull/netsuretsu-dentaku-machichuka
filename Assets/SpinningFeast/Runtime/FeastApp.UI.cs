using System;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SpinningFeast
{
    public sealed partial class FeastApp
    {
        static readonly Color Ink=new Color32(62,37,28,255);
        UIDocument receiptDocument;
        PanelSettings receiptPanel;
        VisualElement receiptStage,titleUI,matchUI,resultUI,bookUI,inputDock,bottomActions,cashierFeed;
        Label orderedStamp,deadlineCall;
        int deadlineCallEpoch=-1,deadlineCalled;
        float deadlinePrevious,deadlineStart=-1;
        const float DeadlineDuration=2.15f;
        float orderStampStart=-1,receiptFeedStart=-1;
        const float OrderStampDuration=.46f,ReceiptFeedDuration=.24f;
        TextField receiptInput;
        readonly Button[] receiptCpu=new Button[3],receiptRecipes=new Button[BookPageSize];
        readonly Label[] receiptScores=new Label[2];
        readonly Label[,] receiptHudDishes=new Label[2,3];
        readonly int[] receiptShownScore=new int[2];
        bool receiptTextInitialized;
        int receiptShownSeconds;
        Label receiptTimer,receiptBookCount,receiptBookPage,receiptStamp;
        Button receiptSubmit,receiptBookPrevious,receiptBookNext;
        VisualElement activeSettingsPanel;
        Button activeSettingsOpener;
        readonly Button[] settingsAudio=new Button[2];
        ReceiptUILayout.Layout receiptLayout;
        bool receiptDataDirty=true,receiptReady,receiptLayoutDirty=true,receiptKeyboardOpen,receiptWasBook;
        MatchPhase receiptPhase=(MatchPhase)(-1);
        int receiptBookIndex=-1,receiptWidth=-1,receiptHeight=-1;
        Rect receiptSafe;
        CookedDish[] receiptLiveDishes=Array.Empty<CookedDish>();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int FeastWebInput_IsComposing();
        [DllImport("__Internal")] static extern void FeastWebInput_Focus();
        [DllImport("__Internal")] static extern void FeastWebInput_PlaceResponsive(float x,float y,float width,float height,float fontHeight);
#endif
        static T Required<T>(VisualElement parent,string name) where T:VisualElement
        {
            var result=parent.Q<T>(name);if(result==null)throw new InvalidOperationException("Receipt UI missing "+typeof(T).Name+" #"+name);return result;
        }
        static void Stretch(VisualElement v)
        {v.style.position=Position.Absolute;v.style.left=v.style.top=v.style.right=v.style.bottom=0;}
        static void Show(VisualElement v,bool visible)
        {v.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;}
        static void SetLabel(Label label,string value){if(label!=null && label.text!=value)label.text=value;}
        VisualElement LoadReceiptScreen(string name)
        {
            var asset=Resources.Load<VisualTreeAsset>("FeastUI/"+name);
            if(asset==null)throw new InvalidOperationException("Missing FeastUI/"+name+".uxml");
            var container=new VisualElement{name=name+"-screen",pickingMode=PickingMode.Ignore};Stretch(container);asset.CloneTree(container);
            receiptStage.Add(container);
            // Decorative labels must not block board input. Scrollers and controls keep picking.
            container.Query<Label>().ForEach(l=>l.pickingMode=PickingMode.Ignore);
            return container;
        }
        void Bind(VisualElement root,string name,Action action)
        {Required<Button>(root,name).clicked+=action;}
        void InitializeReceiptUI()
        {
            receiptPanel=ScriptableObject.CreateInstance<PanelSettings>();receiptPanel.name="Receipt runtime panel";
            receiptPanel.scaleMode=PanelScaleMode.ConstantPixelSize;receiptPanel.sortingOrder=20;
            receiptPanel.themeStyleSheet=Resources.Load<ThemeStyleSheet>("FeastUI/Theme");
            receiptDocument=gameObject.AddComponent<UIDocument>();receiptDocument.panelSettings=receiptPanel;
            var root=receiptDocument.rootVisualElement;Stretch(root);root.pickingMode=PickingMode.Ignore;
            receiptStage=new VisualElement{name="receipt-stage",pickingMode=PickingMode.Ignore,focusable=true,tabIndex=-1};
            receiptStage.style.position=Position.Absolute;root.Add(receiptStage);
            if(font!=null)receiptStage.style.unityFont=font;
            root.RegisterCallback<GeometryChangedEvent>(_=>receiptLayoutDirty=true);
            root.RegisterCallback<KeyDownEvent>(ReceiptKey,TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationCancelEvent>(e=>{
                if(activeSettingsPanel==null)return;
                CloseReceiptSettings(true);e.StopImmediatePropagation();e.PreventDefault();
            },TrickleDown.TrickleDown);
            titleUI=LoadReceiptScreen("Title");matchUI=LoadReceiptScreen("MatchHUD");resultUI=LoadReceiptScreen("Result");bookUI=LoadReceiptScreen("Book");
            Required<Label>(titleUI,"cpu-heading").text=FeastPlayerNames.OpponentStrength;
            for(int p=0;p<3;p++){int level=p;receiptCpu[p]=Required<Button>(titleUI,"cpu-"+p);receiptCpu[p].text=FeastCpu.Name(p);receiptCpu[p].clicked+=()=>SelectCpu(level);}
            Bind(titleUI,"start",BeginMatch);BindReceiptSettings(titleUI,0);
            for(int p=0;p<2;p++)
            {
                receiptScores[p]=Required<Label>(matchUI,"score-"+p);
                Required<Label>(matchUI,"hud-static-"+(p*2)).text=FeastPlayerNames.For(p);
                for(int s=0;s<3;s++)receiptHudDishes[p,s]=matchUI.Q<Label>($"hud-dish-{p}-{s}");
            }
            orderedStamp=Required<Label>(matchUI,"ordered-stamp");
            deadlineCall=Required<Label>(matchUI,"deadline-call");
            cashierFeed=Required<VisualElement>(resultUI,"cashier-feed");
            receiptTimer=Required<Label>(matchUI,"timer");receiptInput=Required<TextField>(matchUI,"dish-input");receiptInput.maxLength=48;receiptInput.isDelayed=false;
            receiptInput.RegisterValueChangedCallback(e=>{inputs[activePlayer]=e.newValue;});
            inputDock=Required<VisualElement>(matchUI,"input-dock");bottomActions=matchUI.Q<VisualElement>("bottom-actions");
            receiptSubmit=Required<Button>(matchUI,"submit");receiptSubmit.clicked+=SubmitReceiptInput;
            Bind(matchUI,"book-open",()=>SetReceiptBook(true));
            BindReceiptSettings(matchUI,1);
            Bind(bookUI,"book-close",()=>SetReceiptBook(false));
            receiptBookCount=Required<Label>(bookUI,"book-count");receiptBookPage=Required<Label>(bookUI,"book-page");
            receiptBookPrevious=Required<Button>(bookUI,"book-prev");receiptBookNext=Required<Button>(bookUI,"book-next");
            receiptBookPrevious.clicked+=()=>TurnReceiptBook(-1);receiptBookNext.clicked+=()=>TurnReceiptBook(1);
            for(int s=0;s<BookPageSize;s++)
            {
                int slot=s;receiptRecipes[s]=Required<Button>(bookUI,"recipe-"+s);
                receiptRecipes[s].clicked+=()=>{int index=bookPage*BookPageSize+slot;
                    if(index>=Cookbook.All.Length || !sim.Discovered[index])return;
                    SetDishInput(Cookbook.All[index].Name);SetReceiptBook(false);sound.Select();};
            }
            receiptStamp=resultUI.Q<Label>("result-stamp");
            InitializeCheckout();
            Bind(resultUI,"replay",BeginMatch);Bind(resultUI,"title",ShowTitle);
            bookUI.RegisterCallback<WheelEvent>(e=>{if(!receiptLayout.Portrait){TurnReceiptBook(e.delta.y>0?1:-1);e.StopPropagation();e.PreventDefault();}},TrickleDown.TrickleDown);
#if UNITY_WEBGL && !UNITY_EDITOR
            // Keep layout/bounds but HTML input owns browser IME; never render a duplicate string.
            receiptInput.style.opacity=0;receiptInput.focusable=false;receiptInput.pickingMode=PickingMode.Ignore;
            receiptInput.Query<VisualElement>().ForEach(v=>{v.pickingMode=PickingMode.Ignore;v.focusable=false;});
#endif
            if(hud!=null)hud.ScoreTarget=ReceiptScoreTarget;
            receiptReady=true;SyncReceiptUI();
        }
        void BindReceiptSettings(VisualElement screen,int slot)
        {
            var opener=Required<Button>(screen,"settings-open");
            var panel=Required<VisualElement>(screen,"settings-panel");
            var audio=settingsAudio[slot]=Required<Button>(panel,"settings-audio");
            var icon=new VisualElement {pickingMode=PickingMode.Ignore};
            icon.AddToClassList("settings-icon");
            icon.generateVisualContent+=DrawSettingsIcon;opener.Add(icon);
            opener.clicked+=()=>{
                if(codex || activeSettingsPanel!=null)return;
                activeSettingsPanel=panel;activeSettingsOpener=opener;
                SyncMatchPause();
                RefreshReceiptAudio();Show(panel,true);audio.Focus();
#if UNITY_WEBGL && !UNITY_EDITOR
                HideWebInput();
#endif
            };
            audio.clicked+=ToggleReceiptAudio;
            Bind(panel,"settings-close",()=>CloseReceiptSettings(true));
            Required<VisualElement>(panel,"settings-dismiss").RegisterCallback<PointerDownEvent>(e=>{
                CloseReceiptSettings(true);e.StopImmediatePropagation();
            });
        }
        static void DrawSettingsIcon(MeshGenerationContext context)
        {
            var painter=context.painter2D;
            painter.strokeColor=Ink;painter.lineWidth=1.8f;
            Vector2 center=new Vector2(15,15);
            for(int outline=0;outline<2;outline++)
            {
                painter.BeginPath();
                for(int i=0;i<32;i++)
                {
                    float angle=(i+.5f)*Mathf.PI/16;
                    float radius=outline==1?4.1f:(i%4==1 || i%4==2?12f:8.7f);
                    Vector2 point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                    if(i==0)painter.MoveTo(point);else painter.LineTo(point);
                }
                painter.ClosePath();painter.Stroke();
            }
        }
        void CloseReceiptSettings(bool restoreFocus)
        {
            if(activeSettingsPanel==null)return;
            Show(activeSettingsPanel,false);activeSettingsPanel=null;
            SyncMatchPause();
            if(restoreFocus)activeSettingsOpener?.Focus();
            activeSettingsOpener=null;
        }
        void RefreshReceiptAudio()
        {
            string text=sound.AudioEnabled?"音（BGM・効果音）：オン":"音（BGM・効果音）：オフ";
            foreach(var button in settingsAudio)if(button!=null && button.text!=text)button.text=text;
        }
        void ToggleReceiptAudio(){sound.ToggleAudio();RefreshReceiptAudio();}
        void SyncMatchPause()
        {
            if(sim==null)return;
            MatchPhase phase=sim.State.Phase;
            sim.SetPaused((phase==MatchPhase.Countdown || phase==MatchPhase.Playing)
                && (codex || activeSettingsPanel!=null));
        }
        void SetReceiptBook(bool open,bool feedback=true)
        {
            if(sim.State.Phase!=MatchPhase.Playing)return;
            codex=open;SyncMatchPause();receiptDataDirty=true;if(feedback)sound.Page();
#if UNITY_WEBGL && !UNITY_EDITOR
            if(open)HideWebInput();
#endif
        }
        bool ReceiptCompositionPending()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return FeastWebInput_IsComposing()!=0;
#else
            return !string.IsNullOrEmpty(CurrentComposition) || Time.unscaledTime-compositionAt<=.08f;
#endif
        }
        void SubmitReceiptInput()
        {
            if(codex || activeSettingsPanel!=null || sim.State.Phase!=MatchPhase.Playing || ReceiptCompositionPending())return;
            HandleMatchKey(KeyCode.Return);FocusReceiptInput();
        }
        void FocusReceiptInput()
        {
            if(sim.State.Phase!=MatchPhase.Playing || codex)return;
#if UNITY_WEBGL && !UNITY_EDITOR
            FeastWebInput_Focus();
#else
            // Do not automatically pop the mobile keyboard on every state transition.
            if(!Application.isMobilePlatform)receiptInput.Focus();
#endif
        }
        void TurnReceiptBook(int delta)
        {bookPage=ReceiptUILayout.TurnPage(bookPage,delta,ReceiptUILayout.PageCount(Cookbook.All.Length,BookPageSize));sound.Page();}
        void SyncReceiptUI()
        {
            if(!receiptReady)return;
            UpdateReceiptLayout();var state=sim.State;bool playing=state.Phase==MatchPhase.Playing;
            bool phaseChanged=receiptPhase!=state.Phase;
            bool changed=phaseChanged || receiptWasBook!=codex;
            if(changed){CloseReceiptSettings(false);SyncMatchPause();}
            if(phaseChanged && state.Phase==MatchPhase.Result)receiptFeedStart=Time.unscaledTime;
            UpdateReceiptMotion(playing && !codex,state.Phase==MatchPhase.Result);
            UpdateDeadlineCall(state);
            Show(titleUI,state.Phase==MatchPhase.Title);Show(resultUI,state.Phase==MatchPhase.Result);
            Show(matchUI,playing || state.Phase==MatchPhase.Countdown);Show(bookUI,playing && codex);
            bool controls=playing && !codex && activeSettingsPanel==null;
            receiptInput.SetEnabled(controls);receiptSubmit.SetEnabled(controls);
            if(changed)
            {
                receiptDataDirty=true;receiptBookIndex=-1;
                if(playing && !codex)FocusReceiptInput();
                else if(codex)Required<Button>(bookUI,"book-close").Focus();
                else if(state.Phase==MatchPhase.Title)receiptCpu[cpuLevel].Focus();else receiptStage.Focus();
                receiptPhase=state.Phase;receiptWasBook=codex;
            }
            for(int p=0;p<3;p++)receiptCpu[p].EnableInClassList("selected",p==cpuLevel);
            for(int p=0;p<2;p++)
            {
                int score=Mathf.RoundToInt(shownScores[p]);
                if(!receiptTextInitialized || changed || receiptShownScore[p]!=score)
                {receiptShownScore[p]=score;SetLabel(receiptScores[p],score.ToString("N0"));}
            }
            int seconds=Mathf.Max(0,Mathf.CeilToInt(state.Remaining));
            if(!receiptTextInitialized || changed || receiptShownSeconds!=seconds)
            {receiptShownSeconds=seconds;SetLabel(receiptTimer,$"{seconds/60:00}:{seconds%60:00}");}
            receiptTextInitialized=true;
            receiptTimer.EnableInClassList("urgent",seconds<=15 && playing);
            if(receiptDataDirty && playing){receiptLiveDishes=sim.MatchDishes();RefreshReceiptHudDishes();}
            if(codex && (receiptDataDirty || receiptBookIndex!=bookPage))RefreshReceiptBook();
            if(state.Phase==MatchPhase.Result && (receiptDataDirty || checkoutEpoch!=state.Epoch))RefreshCheckout(state);
            receiptDataDirty=false;
            UpdateReceiptKeyboard();
#if UNITY_WEBGL && !UNITY_EDITOR
            if(controls)PlaceReceiptWebInput();else HideWebInput();
#endif
        }
        void PlayReceiptStamp(int player)
        {
            // A cooked event is confirmation; a failed or uncommitted IME submission isn't.
            if(!receiptReady || player!=activePlayer || codex || sim.State.Phase!=MatchPhase.Playing)return;
            orderStampStart=Time.unscaledTime;
            Show(orderedStamp,true);
            SetReceiptStampFrame(0);
        }
        void SetReceiptStampFrame(float elapsed)
        {
            float landing=Mathf.Clamp01(elapsed/.12f);
            float scale=landing<.72f?Mathf.Lerp(1.26f,.92f,landing/.72f):Mathf.Lerp(.92f,1,(landing-.72f)/.28f);
            orderedStamp.style.scale=new Scale(new Vector2(scale,scale));
            orderedStamp.style.opacity=1-Mathf.InverseLerp(.29f,OrderStampDuration,elapsed);
        }
        void UpdateReceiptMotion(bool playing,bool showingResult)
        {
            if(orderStampStart>=0)
            {
                float elapsed=Time.unscaledTime-orderStampStart;
                if(!playing || elapsed>=OrderStampDuration){orderStampStart=-1;Show(orderedStamp,false);}
                else SetReceiptStampFrame(elapsed);
            }
            if(receiptFeedStart<0)return;
            float progress=Mathf.Clamp01((Time.unscaledTime-receiptFeedStart)/ReceiptFeedDuration);
            if(!showingResult || progress>=1)
            {
                receiptFeedStart=-1;
                cashierFeed.style.translate=StyleKeyword.Null;
                cashierFeed.style.opacity=StyleKeyword.Null;
                return;
            }
            float ease=1-Mathf.Pow(1-progress,3);
            cashierFeed.style.translate=new Translate(0,Mathf.Lerp(-22,0,ease),0);
            cashierFeed.style.opacity=Mathf.Lerp(.65f,1,ease);
        }
        void UpdateDeadlineCall(MatchState state)
        {
            if(deadlineCallEpoch!=state.Epoch || state.Phase!=MatchPhase.Playing)
            {
                if(deadlineStart>=0)Show(deadlineCall,false);
                deadlineCallEpoch=state.Epoch;deadlineCalled=0;
                deadlinePrevious=state.Remaining;deadlineStart=-1;
                if(state.Phase!=MatchPhase.Playing)return;
            }
            int call=FeastDeadlineCalls.Crossed(deadlinePrevious,state.Remaining,deadlineCalled);
            deadlinePrevious=state.Remaining;
            if(call!=0)
            {
                deadlineCalled=call;deadlineStart=state.Elapsed;
                SetLabel(deadlineCall,FeastDeadlineCalls.Text(call));
                deadlineCall.EnableInClassList("last-call",call==3);
                Show(deadlineCall,true);
            }
            if(deadlineStart<0)return;
            float age=state.Elapsed-deadlineStart;
            if(age>=DeadlineDuration){Show(deadlineCall,false);deadlineStart=-1;return;}
            float settled=Mathf.Clamp01(age/.12f);
            deadlineCall.style.translate=new Translate(0,Mathf.Lerp(-6,0,settled),0);
            deadlineCall.style.opacity=Mathf.SmoothStep(0,1,settled)*(1-Mathf.InverseLerp(DeadlineDuration-.24f,DeadlineDuration,age));
        }
        void UpdateReceiptLayout()
        {
            var viewport=FeastKeyboardViewport.Read();
            var safe=viewport.SafeArea;
            bool portrait=viewport.Portrait;
            if(!receiptLayoutDirty && receiptWidth==Screen.width && receiptHeight==viewport.LayoutHeight && receiptSafe==safe && receiptLayout.Portrait==portrait)return;
            receiptWidth=Screen.width;receiptHeight=viewport.LayoutHeight;receiptSafe=safe;receiptLayoutDirty=false;receiptDataDirty=true;
            receiptLayout=ReceiptUILayout.Calculate(Screen.width,viewport.LayoutHeight,safe,portrait);receiptPanel.scale=receiptLayout.Scale;
            var pixels=receiptLayout.StagePixels;
            receiptStage.style.left=pixels.x/receiptLayout.Scale;receiptStage.style.top=pixels.y/receiptLayout.Scale;
            receiptStage.style.width=receiptLayout.Width;receiptStage.style.height=receiptLayout.Height;
            receiptStage.EnableInClassList("portrait",receiptLayout.Portrait);receiptStage.EnableInClassList("compact",receiptLayout.Compact);
        }
        void UpdateReceiptKeyboard()
        {
            var viewport=FeastKeyboardViewport.Read();
            float visibleBottom=viewport.VisibleBottom;
            bool keyboard=viewport.Keyboard && sim.State.Phase==MatchPhase.Playing && !codex;
            receiptKeyboardOpen=keyboard;
            if(!keyboard){inputDock.style.top=StyleKeyword.Null;inputDock.style.bottom=StyleKeyword.Null;return;}
            visibleBottom=Mathf.Min(visibleBottom,receiptLayout.StagePixels.yMax);
            float bottom=(visibleBottom-receiptLayout.StagePixels.y)/receiptLayout.Scale;
            float dockHeight=inputDock.resolvedStyle.height;if(!float.IsFinite(dockHeight) || dockHeight<=0)dockHeight=76;
            Vector2 local=inputDock.parent.WorldToLocal(receiptStage.LocalToWorld(new Vector2(0,Mathf.Max(0,bottom-dockHeight-8))));
            inputDock.style.top=Mathf.Max(0,local.y);inputDock.style.bottom=StyleKeyword.Auto;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        void PlaceReceiptWebInput()
        {
            Rect r=receiptInput.worldBound;float scale=receiptPanel.scale;
            if(!float.IsFinite(r.x) || r.width<=0 || r.height<=0 || Screen.width<=0 || Screen.height<=0)return;
            float fontSize=receiptInput.resolvedStyle.fontSize;if(!float.IsFinite(fontSize) || fontSize<=0)fontSize=23;
            FeastWebInput_PlaceResponsive(r.x*scale/Screen.width,r.y*scale/Screen.height,r.width*scale/Screen.width,r.height*scale/Screen.height,fontSize*scale/Screen.height);
            webInputVisible=true;
        }
#endif
        void RefreshReceiptHudDishes()
        {
            for(int p=0;p<2;p++)
            {
                int slot=0;foreach(var row in receiptLiveDishes)
                {if(row.Player!=p || slot>=3)continue;SetLabel(receiptHudDishes[p,slot++],Cookbook.All[row.Recipe].Name);}
                while(slot<3)SetLabel(receiptHudDishes[p,slot++],"");
            }
        }
        void RefreshReceiptBook()
        {
            int found=0;foreach(bool value in sim.Discovered)if(value)found++;
            int pages=ReceiptUILayout.PageCount(Cookbook.All.Length,BookPageSize);bookPage=Mathf.Clamp(bookPage,0,pages-1);
            SetLabel(receiptBookCount,$"{found} / {Cookbook.All.Length}");SetLabel(receiptBookPage,$"{bookPage+1} / {pages}");
            receiptBookPrevious.SetEnabled(pages>1);receiptBookNext.SetEnabled(pages>1);
            for(int slot=0;slot<BookPageSize;slot++)
            {
                int index=bookPage*BookPageSize+slot;bool known=index<Cookbook.All.Length && sim.Discovered[index];
                var b=receiptRecipes[slot];b.text=known?Cookbook.All[index].Name:"◇";b.tooltip=known?b.text:"未発見";b.SetEnabled(known);
                b.EnableInClassList("undiscovered",!known);
            }
            if(receiptBookIndex!=bookPage){var scroll=bookUI.Q<ScrollView>();if(scroll!=null)scroll.scrollOffset=Vector2.zero;}
            receiptBookIndex=bookPage;
        }
        void ReceiptKey(KeyDownEvent e)
        {
            if(!receiptReady)return;
            KeyCode key=e.keyCode;var phase=sim.State.Phase;bool used=true;
            if(activeSettingsPanel!=null)
            {
                if(key==KeyCode.Escape){CloseReceiptSettings(true);e.StopImmediatePropagation();e.PreventDefault();}
                else if(key==KeyCode.F3){ToggleReceiptAudio();e.StopImmediatePropagation();e.PreventDefault();}
                else if(key==KeyCode.Tab)
                {
                    var audio=Required<Button>(activeSettingsPanel,"settings-audio");
                    var close=Required<Button>(activeSettingsPanel,"settings-close");
                    if(activeSettingsPanel.focusController.focusedElement==audio)close.Focus();else audio.Focus();
                    e.StopImmediatePropagation();e.PreventDefault();
                }
                return; // Let the focused settings button receive navigation/submit, never gameplay shortcuts.
            }
            if(key==KeyCode.F3)ToggleReceiptAudio();
            else if(phase==MatchPhase.Title)
            {
                if(key==KeyCode.Return || key==KeyCode.KeypadEnter || key==KeyCode.F5)BeginMatch();
                else if(key==KeyCode.LeftArrow || key==KeyCode.RightArrow)SelectCpu((cpuLevel+(key==KeyCode.RightArrow?1:2))%3);else used=false;
            }
            else if(phase==MatchPhase.Result)
            {
                if(key==KeyCode.F5)BeginMatch();else if(key==KeyCode.Escape)ShowTitle();
                else if(key==KeyCode.LeftArrow || key==KeyCode.RightArrow)ShowCheckoutPlayer(key==KeyCode.RightArrow?1:0,true);
                else if(key!=KeyCode.Return && key!=KeyCode.KeypadEnter)used=false; // Never let Enter replay from focused button.
            }
            else if(phase==MatchPhase.Playing)
            {
                bool enter=key==KeyCode.Return || key==KeyCode.KeypadEnter;
                if(enter && !codex)
                {
                    if(ReceiptCompositionPending())used=false; // Let the TextField commit preedit.
                    else {SubmitReceiptInput();used=true;}
                }
                else used=HandleMatchKey(key);
            }
            else used=false;
            if(used){e.StopImmediatePropagation();e.PreventDefault();}
        }
        Vector2 ReceiptScoreTarget(int player)
        {
            Rect bounds=receiptScores[Mathf.Clamp(player,0,1)].worldBound;
            if(bounds.width<=0 || !float.IsFinite(bounds.x))return new Vector2(player==0?190:1090,92);
            Vector2 pixel=bounds.center*receiptPanel.scale;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            return (pixel-new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-800*scale)*.5f))/Mathf.Max(.001f,scale);
        }
        void DisposeReceiptUI()
        {
            receiptReady=false;if(hud!=null)hud.ScoreTarget=null;
            if(receiptDocument!=null)Destroy(receiptDocument);if(receiptPanel!=null)Destroy(receiptPanel);
        }
    }
}
