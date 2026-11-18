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
        VisualElement receiptStage,titleUI,matchUI,resultUI,bookUI,inputDock,bottomActions;
        TextField receiptInput;
        readonly Button[] receiptCpu=new Button[3],receiptRecipes=new Button[BookPageSize];
        readonly Label[] receiptScores=new Label[2],receiptResultScores=new Label[2],receiptHeaders=new Label[2];
        readonly Label[,] receiptDishNames=new Label[2,ResultPageSize],receiptDishCounts=new Label[2,ResultPageSize],receiptHudDishes=new Label[2,3];
        Label receiptTimer,receiptBookCount,receiptBookPage,receiptResultPage,receiptResultHeading,receiptResultTotal,receiptStamp;
        Button receiptMusic,receiptSubmit,receiptBookPrevious,receiptBookNext,receiptResultPrevious,receiptResultNext;
        ReceiptUILayout.Layout receiptLayout;
        bool receiptDataDirty=true,receiptReady,receiptLayoutDirty=true,receiptKeyboardOpen,receiptWasBook;
        MatchPhase receiptPhase=(MatchPhase)(-1);
        int receiptBookIndex=-1,receiptResultIndex=-1,receiptWidth=-1,receiptHeight=-1;
        Rect receiptSafe;
        int receiptUnobscuredWidth,receiptUnobscuredHeight;
        bool receiptUnobscuredPortrait;
        CookedDish[] receiptLiveDishes=Array.Empty<CookedDish>();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int FeastWebInput_IsComposing();
        [DllImport("__Internal")] static extern float FeastWebInput_VisibleBottom();
        [DllImport("__Internal")] static extern int FeastWebInput_Orientation();
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
            titleUI=LoadReceiptScreen("Title");matchUI=LoadReceiptScreen("MatchHUD");resultUI=LoadReceiptScreen("Result");bookUI=LoadReceiptScreen("Book");
            for(int p=0;p<3;p++){int level=p;receiptCpu[p]=Required<Button>(titleUI,"cpu-"+p);receiptCpu[p].text=FeastCpu.Name(p);receiptCpu[p].clicked+=()=>SelectCpu(level);}
            Bind(titleUI,"start",BeginMatch);receiptMusic=Required<Button>(titleUI,"music");receiptMusic.clicked+=()=>sound.ToggleMusic();
            for(int p=0;p<2;p++)
            {
                receiptScores[p]=Required<Label>(matchUI,"score-"+p);receiptResultScores[p]=Required<Label>(resultUI,"result-score-"+p);
                receiptHeaders[p]=Required<Label>(resultUI,"result-header-"+p);
                for(int s=0;s<ResultPageSize;s++)
                {receiptDishNames[p,s]=Required<Label>(resultUI,$"result-name-{p}-{s}");receiptDishCounts[p,s]=Required<Label>(resultUI,$"result-count-{p}-{s}");}
                for(int s=0;s<3;s++)receiptHudDishes[p,s]=matchUI.Q<Label>($"hud-dish-{p}-{s}");
            }
            receiptTimer=Required<Label>(matchUI,"timer");receiptInput=Required<TextField>(matchUI,"dish-input");receiptInput.maxLength=48;receiptInput.isDelayed=false;
            receiptInput.RegisterValueChangedCallback(e=>{inputs[activePlayer]=e.newValue;});
            inputDock=Required<VisualElement>(matchUI,"input-dock");bottomActions=matchUI.Q<VisualElement>("bottom-actions");
            receiptSubmit=Required<Button>(matchUI,"submit");receiptSubmit.clicked+=SubmitReceiptInput;
            Bind(matchUI,"book-open",()=>SetReceiptBook(true));
            var music=matchUI.Q<Button>("music");if(music!=null)music.clicked+=()=>sound.ToggleMusic();
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
            receiptResultHeading=Required<Label>(resultUI,"result-heading");receiptResultTotal=Required<Label>(resultUI,"result-total");
            receiptStamp=resultUI.Q<Label>("result-stamp");receiptResultPage=Required<Label>(resultUI,"result-page");
            receiptResultPrevious=Required<Button>(resultUI,"result-prev");receiptResultNext=Required<Button>(resultUI,"result-next");
            receiptResultPrevious.clicked+=()=>TurnReceiptResult(-1);receiptResultNext.clicked+=()=>TurnReceiptResult(1);
            Bind(resultUI,"replay",BeginMatch);Bind(resultUI,"title",ShowTitle);
            resultUI.RegisterCallback<WheelEvent>(e=>{if(!receiptLayout.Portrait && ResultPageCount()>1){TurnReceiptResult(e.delta.y>0?1:-1);e.StopPropagation();e.PreventDefault();}},TrickleDown.TrickleDown);
            bookUI.RegisterCallback<WheelEvent>(e=>{if(!receiptLayout.Portrait){TurnReceiptBook(e.delta.y>0?1:-1);e.StopPropagation();e.PreventDefault();}},TrickleDown.TrickleDown);
#if UNITY_WEBGL && !UNITY_EDITOR
            // Keep layout/bounds but HTML input owns browser IME; never render a duplicate string.
            receiptInput.style.opacity=0;receiptInput.focusable=false;receiptInput.pickingMode=PickingMode.Ignore;
            receiptInput.Query<VisualElement>().ForEach(v=>{v.pickingMode=PickingMode.Ignore;v.focusable=false;});
#endif
            if(hud!=null)hud.ScoreTarget=ReceiptScoreTarget;
            receiptReady=true;SyncReceiptUI();
        }
        void SetReceiptBook(bool open)
        {
            if(sim.State.Phase!=MatchPhase.Playing)return;
            codex=open;receiptDataDirty=true;sound.Page();
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
            if(codex || sim.State.Phase!=MatchPhase.Playing || ReceiptCompositionPending())return;
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
        int ResultPageCount()
        {
            int left=0,right=0;foreach(var row in resultDishes)if(row.Player==0)left++;else right++;
            return ReceiptUILayout.PageCount(Mathf.Max(left,right),ResultPageSize);
        }
        void TurnReceiptResult(int delta)
        {resultPage=ReceiptUILayout.TurnPage(resultPage,delta,ResultPageCount());if(ResultPageCount()>1)sound.Page();}
        void SyncReceiptUI()
        {
            if(!receiptReady)return;
            UpdateReceiptLayout();var state=sim.State;bool playing=state.Phase==MatchPhase.Playing;
            bool changed=receiptPhase!=state.Phase || receiptWasBook!=codex;
            Show(titleUI,state.Phase==MatchPhase.Title);Show(resultUI,state.Phase==MatchPhase.Result);
            Show(matchUI,playing || state.Phase==MatchPhase.Countdown);Show(bookUI,playing && codex);
            receiptInput.SetEnabled(playing && !codex);receiptSubmit.SetEnabled(playing && !codex);
            if(changed)
            {
                receiptDataDirty=true;receiptBookIndex=receiptResultIndex=-1;
                if(playing && !codex)FocusReceiptInput();
                else if(codex)Required<Button>(bookUI,"book-close").Focus();
                else if(state.Phase==MatchPhase.Title)receiptCpu[cpuLevel].Focus();else receiptStage.Focus();
                receiptPhase=state.Phase;receiptWasBook=codex;
            }
            for(int p=0;p<3;p++)receiptCpu[p].EnableInClassList("selected",p==cpuLevel);
            string musicText=sound.MusicEnabled?"♪ ON":"♪ OFF";
            if(receiptMusic.text!=musicText)receiptMusic.text=musicText;
            var music=matchUI.Q<Button>("music");if(music!=null && music.text!=musicText)music.text=musicText;
            SetLabel(titleUI.Q<Label>("cpu-description"),FeastCpu.Description(cpuLevel));
            for(int p=0;p<2;p++)SetLabel(receiptScores[p],Mathf.RoundToInt(shownScores[p]).ToString("N0"));
            int seconds=Mathf.Max(0,Mathf.CeilToInt(state.Remaining));SetLabel(receiptTimer,$"{seconds/60:00}:{seconds%60:00}");
            receiptTimer.EnableInClassList("urgent",seconds<=15 && playing);
            if(receiptDataDirty && playing){receiptLiveDishes=sim.MatchDishes();RefreshReceiptHudDishes();}
            if(codex && (receiptDataDirty || receiptBookIndex!=bookPage))RefreshReceiptBook();
            if(state.Phase==MatchPhase.Result && (receiptDataDirty || receiptResultIndex!=resultPage))RefreshReceiptResult();
            receiptDataDirty=false;
            UpdateReceiptKeyboard();
#if UNITY_WEBGL && !UNITY_EDITOR
            if(playing && !codex)PlaceReceiptWebInput();else HideWebInput();
#endif
        }
        void UpdateReceiptLayout()
        {
            var safe=Screen.safeArea;
            bool? portraitOverride=null;
#if UNITY_WEBGL && !UNITY_EDITOR
            int orientation=FeastWebInput_Orientation();if(orientation>=0)portraitOverride=orientation==1;
#else
            bool nativeKeyboard=TouchScreenKeyboard.visible;
            if(!nativeKeyboard || receiptUnobscuredWidth==0 || Mathf.Abs(Screen.width-receiptUnobscuredWidth)>receiptUnobscuredWidth*.2f)
            {
                receiptUnobscuredWidth=Screen.width;receiptUnobscuredHeight=Screen.height;
                receiptUnobscuredPortrait=Screen.width<Screen.height;
            }
            if(nativeKeyboard)portraitOverride=receiptUnobscuredPortrait;
#endif
            if(!receiptLayoutDirty && receiptWidth==Screen.width && receiptHeight==Screen.height && receiptSafe==safe)return;
            receiptWidth=Screen.width;receiptHeight=Screen.height;receiptSafe=safe;receiptLayoutDirty=false;receiptDataDirty=true;
            receiptLayout=ReceiptUILayout.Calculate(Screen.width,Screen.height,safe,portraitOverride);receiptPanel.scale=receiptLayout.Scale;
            var pixels=receiptLayout.StagePixels;
            receiptStage.style.left=pixels.x/receiptLayout.Scale;receiptStage.style.top=pixels.y/receiptLayout.Scale;
            receiptStage.style.width=receiptLayout.Width;receiptStage.style.height=receiptLayout.Height;
            receiptStage.EnableInClassList("portrait",receiptLayout.Portrait);receiptStage.EnableInClassList("compact",receiptLayout.Compact);
        }
        void UpdateReceiptKeyboard()
        {
            float visibleBottom=Screen.height;bool keyboard=false;
#if UNITY_WEBGL && !UNITY_EDITOR
            visibleBottom=Mathf.Clamp01(FeastWebInput_VisibleBottom())*Screen.height;
            keyboard=visibleBottom<receiptLayout.StagePixels.yMax-40;
#else
            if(TouchScreenKeyboard.visible)
            {
                float height=TouchScreenKeyboard.area.height;
                // adjustResize may already have shortened Screen.height. Subtract only the
                // remaining overlay portion, never both the resize and the full keyboard.
                float resized=Mathf.Abs(Screen.width-receiptUnobscuredWidth)<=receiptUnobscuredWidth*.2f
                    ?Mathf.Max(0,receiptUnobscuredHeight-Screen.height):0;
                float overlay=height>0?Mathf.Max(0,height-resized):(resized>40?0:Screen.height*.4f);
                visibleBottom=Screen.height-overlay;keyboard=true;
            }
#endif
            keyboard=keyboard && sim.State.Phase==MatchPhase.Playing && !codex;
            receiptStage.EnableInClassList("keyboard-open",keyboard);receiptKeyboardOpen=keyboard;
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
        void RefreshReceiptResult()
        {
            var state=sim.State;int winner=state.CyanScore==state.OrangeScore?-1:state.CyanScore>state.OrangeScore?0:1;
            SetLabel(receiptResultHeading,winner<0?"いい勝負！":winner==0?"あなたの勝ち！":"CPUの勝ち！");
            SetLabel(receiptStamp,winner<0?"引\n分":winner==0?"勝\n利":"再\n挑\n戦");
            SetLabel(receiptResultScores[0],state.CyanScore.ToString("N0"));SetLabel(receiptResultScores[1],state.OrangeScore.ToString("N0"));
            int pages=ResultPageCount();resultPage=Mathf.Clamp(resultPage,0,pages-1);int all=0;
            for(int p=0;p<2;p++)
            {
                for(int slot=0;slot<ResultPageSize;slot++){SetLabel(receiptDishNames[p,slot],"");SetLabel(receiptDishCounts[p,slot],"");}
                int ordinal=0,total=0;
                foreach(var row in resultDishes)
                {
                    if(row.Player!=p)continue;int slot=ordinal++-resultPage*ResultPageSize;total+=row.Count;
                    if(slot<0 || slot>=ResultPageSize)continue;
                    string name=Cookbook.All[row.Recipe].Name;SetLabel(receiptDishNames[p,slot],name);receiptDishNames[p,slot].tooltip=name;
                    receiptDishNames[p,slot].style.fontSize=Mathf.Clamp((receiptLayout.Portrait?188f:168f)/Mathf.Max(1,name.Length),12,receiptLayout.Portrait?16:19);
                    SetLabel(receiptDishCounts[p,slot],$"{row.Count:N0} 人前");
                }
                if(ordinal==0)SetLabel(receiptDishNames[p,0],"まだ料理がありません");
                else if(resultPage*ResultPageSize>=ordinal)SetLabel(receiptDishNames[p,0],"ここまでで全品！");
                SetLabel(receiptHeaders[p],$"{(p==0?"あなた":"CPU")}　{ordinal}品 / {total:N0}人前");all+=total;
            }
            SetLabel(receiptResultTotal,$"計 {all:N0} 人前");SetLabel(receiptResultPage,$"{resultPage+1} / {pages}");
            receiptResultPrevious.SetEnabled(pages>1);receiptResultNext.SetEnabled(pages>1);
            if(receiptResultIndex!=resultPage){var scroll=resultUI.Q<ScrollView>();if(scroll!=null)scroll.scrollOffset=Vector2.zero;}
            receiptResultIndex=resultPage;
        }
        void ReceiptKey(KeyDownEvent e)
        {
            if(!receiptReady)return;
            KeyCode key=e.keyCode;var phase=sim.State.Phase;bool used=true;
            if(key==KeyCode.F3)sound.ToggleMusic();
            else if(phase==MatchPhase.Title)
            {
                if(key==KeyCode.Return || key==KeyCode.KeypadEnter || key==KeyCode.F5)BeginMatch();
                else if(key==KeyCode.LeftArrow || key==KeyCode.RightArrow)SelectCpu((cpuLevel+(key==KeyCode.RightArrow?1:2))%3);else used=false;
            }
            else if(phase==MatchPhase.Result)
            {
                if(key==KeyCode.F5)BeginMatch();else if(key==KeyCode.Escape)ShowTitle();
                else if(key==KeyCode.LeftArrow || key==KeyCode.RightArrow)TurnReceiptResult(key==KeyCode.RightArrow?1:-1);
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
