using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using Unity.Entities;
using UnityEngine;

namespace SpinningFeast
{
    public sealed partial class FeastApp : MonoBehaviour
    {
        FeastSystem sim;
        FeastView view;
        readonly string[] inputs={"",""};
        readonly int[] sequence={0,0};
        readonly float[] flashes={0,0};
        readonly bool[] failed={false,false};
        readonly float[] shownScores={0,0};
        const int activePlayer=0;
        int bookPage,cpuLevel=1,cpuTurn;
        const int BookPageSize=20;
        const int ResultPageSize=6;
        int resultPage;
        CookedDish[] resultDishes=Array.Empty<CookedDish>();
        bool codex, smoke;
        float botAt=5;
#if UNITY_WEBGL && !UNITY_EDITOR
        const string CurrentComposition=""; // The browser input value already includes the preedit text.
#else
        static string CurrentComposition=>Input.compositionString;
        float compositionAt=-100;
        string previousComposition="";
#endif
        int runtimeErrors;
        Font font;
        GUIStyle scoreStyle,timeStyle,inputStyle,bookStyle,chantStyle,tagStyle,menuTitleStyle,menuLabelStyle,menuButtonStyle;
        struct WordBurst { public string Text; public int Player; public float Born; }
        readonly List<WordBurst> wordBursts=new List<WordBurst>();
        FeastSound sound;
        FeastEffects effects;
        FeastHudEffects hud;
        bool endingPlayed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartGame()
        {
            if(FindAnyObjectByType<FeastApp>()!=null) return;
            new GameObject("熱烈電卓町中華 — runtime").AddComponent<FeastApp>();
        }
        void Start()
        {
            Application.logMessageReceived+=CountErrors;
            Application.targetFrameRate=60; Application.runInBackground=true;
#if !UNITY_WEBGL || UNITY_EDITOR
            Input.imeCompositionMode=IMECompositionMode.On;
#endif
            bool effectsProbe=Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-effects-test")>=0;
            bool gifCapture=Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-gif-capture")>=0;
            bool uiSmoke=ReceiptUISmokeRequested;
            smoke=uiSmoke || effectsProbe || gifCapture || Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-smoke")>=0;
            cpuLevel=smoke?1:FeastCpu.ClampLevel(PlayerPrefs.GetInt("feast.cpu-level",1));
            sim=World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<FeastSystem>();
            sim.SaveDiscoveries=!smoke;sim.ResetMatch(phase:smoke && !uiSmoke?MatchPhase.Playing:MatchPhase.Title);
            sim.Cooked+=OnCooked;sim.Failed+=OnFailed;sim.Served+=OnServed;
            view=gameObject.AddComponent<FeastView>();view.Initialize(sim);
            font=Resources.Load<Font>("NotoSansJP-Regular");
            sound=gameObject.AddComponent<FeastSound>();sound.Initialize();if(smoke)sound.Begin();
            effects=gameObject.AddComponent<FeastEffects>();effects.Initialize(sim,view);
            gameObject.AddComponent<FeastFrameProfile>().Initialize(sim,view,effects);
            hud=new FeastHudEffects();hud.Initialize(sim,view,effects,sound,font);
#if UNITY_WEBGL && !UNITY_EDITOR
            webInputOwner=this;
            FeastWebInput_Create(WebInputHandler);
#endif
            InitializeReceiptUI();
            if(uiSmoke) StartCoroutine(ReceiptUISmoke());
            else if(smoke) StartCoroutine(effectsProbe?EffectsProbe():gifCapture?CaptureCookGif():SmokeRun());
        }
        void CountErrors(string condition,string stack,LogType type) {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) runtimeErrors++;}
        void OnCooked(int player,int recipe,int servings) {receiptDataDirty=true;flashes[player]=1;failed[player]=false;sound.Cook(player,recipe,servings);}
        void OnFailed(int player) {flashes[player]=.7f;failed[player]=true;sound.Missing(player);}
        void OnServed(int player,int recipe,int servings) {sound.Serve(player,recipe,servings);}
        void Update()
        {
            if(sim==null) return;
            hud?.Update();
            string composition=CurrentComposition;
            if(!codex && hud!=null)hud.ObserveInput(activePlayer,inputs[activePlayer]+composition);
#if !UNITY_WEBGL || UNITY_EDITOR
            if(!string.IsNullOrEmpty(composition) || !string.IsNullOrEmpty(previousComposition)) compositionAt=Time.unscaledTime;
            previousComposition=composition;
#endif
            for(int p=0;p<2;p++) flashes[p]=Mathf.Max(0,flashes[p]-Time.deltaTime*1.9f);
            wordBursts.RemoveAll(b=>Time.unscaledTime-b.Born>=1.05f);
            var state=sim.State;sound.SetRemaining(state.Remaining);
            view.SetPreview(!codex && state.Phase==MatchPhase.Playing?Cookbook.Find(inputs[activePlayer]+composition):-1,activePlayer);
            if(state.Phase==MatchPhase.Result && !endingPlayed)
            {
                endingPlayed=true;receiptDataDirty=true;resultDishes=sim.MatchDishes();resultPage=0;codex=false;
#if UNITY_WEBGL && !UNITY_EDITOR
                HideWebInput();
#endif
                SetDishInput("");inputs[1]="";wordBursts.Clear();GUIUtility.keyboardControl=0;sound.End();
            }
            shownScores[0]=Mathf.MoveTowards(shownScores[0],state.CyanScore,Time.deltaTime*Mathf.Max(800,(state.CyanScore-shownScores[0])*8));
            shownScores[1]=Mathf.MoveTowards(shownScores[1],state.OrangeScore,Time.deltaTime*Mathf.Max(800,(state.OrangeScore-shownScores[1])*8));
            if(!smoke && state.Phase==MatchPhase.Playing && state.Elapsed>=botAt)
            {
                int chosen=FeastCpu.Choose(cpuLevel,sim.Inventory(),cpuTurn++);
                if(chosen>=0) Send(1,chosen);
                botAt=state.Elapsed+FeastCpu.Interval(cpuLevel);
            }
        }
        void LateUpdate() { if(sim!=null) SyncReceiptUI(); }
        void SelectCpu(int level)
        {
            cpuLevel=FeastCpu.ClampLevel(level);
            if(!smoke){PlayerPrefs.SetInt("feast.cpu-level",cpuLevel);PlayerPrefs.Save();}
            sound.Select();
        }

        public void Send(int player,int recipe,string typedText=null)
        {
            if(sim.State.Phase!=MatchPhase.Playing || sim.State.Remaining<=0) return;
            string words=typedText ?? (recipe>=0 && recipe<Cookbook.All.Length?Cookbook.All[recipe].Name:"");
            if(!string.IsNullOrWhiteSpace(words))
            {
                // Repeated submissions replace the previous burst on that side so the text stays legible.
                wordBursts.RemoveAll(b=>b.Player==player);
                wordBursts.Add(new WordBurst {Text=words,Player=player,Born=Time.unscaledTime});
            }
            sound.Submit(player);hud?.Submit(player);
            sim.Submit(player,recipe,++sequence[player],sim.State.Epoch);
        }
        void ResetPresentation()
        {
            sequence[0]=sequence[1]=0;shownScores[0]=shownScores[1]=0;
            SetDishInput("");inputs[1]="";flashes[0]=flashes[1]=0;failed[0]=failed[1]=false;
            resultDishes=Array.Empty<CookedDish>();resultPage=0;
            cpuTurn=0;botAt=FeastCpu.FirstMove(cpuLevel);codex=false;bookPage=0;wordBursts.Clear();endingPlayed=false;
#if !UNITY_WEBGL || UNITY_EDITOR
            previousComposition="";compositionAt=Time.unscaledTime;
#endif
            GUIUtility.keyboardControl=0;
            if(effects!=null)effects.ResetForMatch();hud?.Reset();if(view!=null)view.SetPreview(-1,0);
        }
        void BeginMatch()
        {
            sim.ResetMatch(phase:MatchPhase.Countdown);ResetPresentation();sound.Begin();
        }
        void ShowTitle()
        {
            sim.ResetMatch(phase:MatchPhase.Title);ResetPresentation();sound.Silence();sound.Menu();
        }
        void SetDishInput(string text)
        {
            inputs[activePlayer]=text;
            receiptInput?.SetValueWithoutNotify(text);
#if UNITY_WEBGL && !UNITY_EDITOR
            FeastWebInput_SetValue(text);
#endif
        }
        bool HandleMatchKey(KeyCode key)
        {
            if(codex && (key==KeyCode.LeftArrow || key==KeyCode.RightArrow))
            {
                bookPage=Mathf.Clamp(bookPage+(key==KeyCode.RightArrow?1:-1),0,(Cookbook.All.Length-1)/BookPageSize);
                sound.Page();
            }
            else if(key==KeyCode.F1)
            {
                codex=!codex;sound.Page();
#if UNITY_WEBGL && !UNITY_EDITOR
                if(codex)HideWebInput();
#endif
            }
            else if(key==KeyCode.F5)BeginMatch();
            else if(key==KeyCode.Escape)
            {
                if(codex)codex=false;
                else SetDishInput("");
            }
            else if((key==KeyCode.Return || key==KeyCode.KeypadEnter) && !codex)
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if(!string.IsNullOrEmpty(CurrentComposition) || Time.unscaledTime-compositionAt<=.08f)return false;
#endif
                string text=inputs[activePlayer];
                if(!string.IsNullOrWhiteSpace(text))Send(activePlayer,Cookbook.Find(text),text);
                SetDishInput("");
            }
            else return false; // Leave Tab to UI Toolkit focus navigation.
            return true;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        enum WebInputAction { Edit, Submit, Clear, Codex, Music, Restart }
        delegate void WebInputCallback(int action, IntPtr text);
        static readonly WebInputCallback WebInputHandler=OnWebInput;
        static FeastApp webInputOwner;
        bool webInputVisible;

        [DllImport("__Internal")] static extern void FeastWebInput_Create(WebInputCallback callback);
        [DllImport("__Internal")] static extern void FeastWebInput_SetValue(string text);
        [DllImport("__Internal")] static extern void FeastWebInput_Hide();
        [DllImport("__Internal")] static extern void FeastWebInput_Destroy();

        [AOT.MonoPInvokeCallback(typeof(WebInputCallback))]
        static void OnWebInput(int action, IntPtr text)
        {
            var app=webInputOwner;
            if(app==null || app.codex || app.sim.State.Phase!=MatchPhase.Playing)return;
            if(text!=IntPtr.Zero){app.inputs[activePlayer]=Marshal.PtrToStringUTF8(text);app.receiptInput?.SetValueWithoutNotify(app.inputs[activePlayer]);}
            switch((WebInputAction)action)
            {
                case WebInputAction.Submit: app.HandleMatchKey(KeyCode.Return); break;
                case WebInputAction.Clear: app.HandleMatchKey(KeyCode.Escape); break;
                case WebInputAction.Codex: app.HandleMatchKey(KeyCode.F1); break;
                case WebInputAction.Music: app.sound.ToggleMusic(); break;
                case WebInputAction.Restart: app.HandleMatchKey(KeyCode.F5); break;
            }
        }
        void HideWebInput()
        {
            if(!webInputVisible)return;
            FeastWebInput_Hide();
            webInputVisible=false;
        }
#endif
        void Styles()
        {
            if(scoreStyle!=null) return;
            scoreStyle=new GUIStyle(GUI.skin.label) {font=font,fontSize=38,alignment=TextAnchor.MiddleLeft,fontStyle=FontStyle.Bold};
            timeStyle=new GUIStyle(scoreStyle) {fontSize=27,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Normal};
            inputStyle=new GUIStyle(GUI.skin.textField) {font=font,fontSize=23,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(18,18,10,10)};
            inputStyle.normal.background=inputStyle.focused.background=inputStyle.hover.background=inputStyle.active.background=null;
            inputStyle.normal.textColor=inputStyle.focused.textColor=Ink;
            bookStyle=new GUIStyle(timeStyle) {fontSize=22};
            chantStyle=new GUIStyle(timeStyle) {fontSize=56,fontStyle=FontStyle.Bold,wordWrap=true,richText=false};
            tagStyle=new GUIStyle(timeStyle) {fontSize=19,fontStyle=FontStyle.Normal,richText=false};
            menuTitleStyle=new GUIStyle(timeStyle) {fontSize=66,fontStyle=FontStyle.Bold};
            menuLabelStyle=new GUIStyle(timeStyle) {fontSize=18};
            menuButtonStyle=new GUIStyle(timeStyle) {fontSize=23,fontStyle=FontStyle.Bold};
        }
        static void Fill(Rect rect,Color color) {Color before=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=before;}
        // Only world-anchored labels and transient presentation remain IMGUI.
        // All menus, scores, input, book and result controls use the shared UIDocument.
        void OnGUI()
        {
            if(sim==null) return;
            Styles();float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-800*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            var state=sim.State;
            if(!codex && state.Phase==MatchPhase.Playing)
            {DrawDishTags(scale,state.Epoch);DrawIngredientHint(scale);DrawWordEffects(state.Remaining>0);}
            if(state.Phase==MatchPhase.Playing || state.Phase==MatchPhase.Countdown)hud?.DrawPlaying(scale,codex);
            hud?.DrawTransition();GUI.matrix=old;
        }
        void DrawWordEffects(bool playing)
        {
            for(int p=0;p<2;p++)
            {
                string preview=inputs[p];
                if(p==activePlayer)preview+=CurrentComposition;
                if(playing && !string.IsNullOrWhiteSpace(preview))
                    DrawWords(preview,new Vector2(p==0?640:885,652),p,42,.6f,0,1+(hud!=null?hud.TypePulse(p):0));
            }
            foreach(var burst in wordBursts)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-burst.Born)/1.05f);
                float pop=Mathf.Lerp(.7f,1.12f,Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.18f)));
                float alpha=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.5f)/.5f));
                DrawWords(burst.Text,new Vector2(burst.Player==0?395:885,560-85*t),burst.Player,56,alpha,(burst.Player==0?-1:1)*5*Mathf.Sin(t*Mathf.PI),pop);
            }
        }
        void DrawWords(string text,Vector2 center,int player,int size,float alpha,float rotation,float pop)
        {
            chantStyle.fontSize=size;chantStyle.wordWrap=false;
            float width=chantStyle.CalcSize(new GUIContent(text)).x;
            if(width>450)chantStyle.fontSize=Mathf.Max(18,Mathf.FloorToInt(size*450/width));
            chantStyle.wordWrap=true;
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=old*Matrix4x4.TRS(new Vector3(center.x,center.y,0),Quaternion.Euler(0,0,rotation),new Vector3(pop,pop,1));
            var box=new Rect(-230,-65,460,130);
            chantStyle.normal.textColor=new Color(.015f,.045f,.055f,alpha*.95f);
            GUI.Label(new Rect(box.x+3,box.y+4,box.width,box.height),text,chantStyle);
            Color color=player==0?FeastView.Cyan:FeastView.Orange;color.a=alpha;
            chantStyle.normal.textColor=color;GUI.Label(box,text,chantStyle);GUI.matrix=old;
        }
        void DrawDishTags(float scale,int epoch)
        {
            if(view==null || view.TagEpoch!=epoch || view.ViewCamera==null) return;
            Vector2 offset=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-800*scale)*.5f);
            foreach(var tag in view.DishTags)
            {
                Vector3 screen=view.ViewCamera.WorldToScreenPoint(tag.Position);
                if(screen.z<=0 || screen.x<0 || screen.x>Screen.width || screen.y<0 || screen.y>Screen.height)continue;
                Vector2 anchor=(new Vector2(screen.x,Screen.height-screen.y)-offset)/scale;
                string name=Cookbook.All[tag.Recipe].Name;
                float width=tagStyle.CalcSize(new GUIContent(name)).x+22;
                // Labels stay anchored to their own plate; overlap is intentional.
                Rect label=new Rect(Mathf.Clamp(anchor.x-width*.5f,64,1216-width),Mathf.Clamp(anchor.y-43,160,600),width,29);
                Color actor=tag.Player==0?FeastView.Cyan:FeastView.Orange;actor.a=.55f;
                DrawLeader(anchor,new Vector2(label.center.x,label.yMax),new Color(actor.r,actor.g,actor.b,.35f));
                Fill(new Rect(anchor.x-2,anchor.y-2,4,4),actor);
                Fill(label,new Color(.025f,.075f,.09f,.45f));
                Fill(new Rect(label.x,label.yMax-2,label.width,2),actor);
                tagStyle.normal.textColor=new Color(1,.97f,.86f,.8f);GUI.Label(label,name,tagStyle);hud?.DishGlints(label,tag.Recipe);
            }
        }
        void DrawIngredientHint(float scale)
        {
            if(view==null || !view.HasHoveredIngredient || view.TagEpoch!=sim.State.Epoch)return;
            Vector3 screen=view.ViewCamera.WorldToScreenPoint(view.HoveredIngredientPosition);
            Vector2 offset=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-800*scale)*.5f);
            Vector2 anchor=(new Vector2(screen.x,Screen.height-screen.y)-offset)/scale;
            string name=Cookbook.RawNames[(int)view.HoveredIngredient];
            float width=tagStyle.CalcSize(new GUIContent(name)).x+26;
            var box=new Rect(Mathf.Clamp(anchor.x-width*.5f,20,1260-width),Mathf.Clamp(anchor.y-50,145,660),width,30);
            Fill(box,new Color(.025f,.075f,.09f,.45f));Fill(new Rect(box.x,box.yMax-2,box.width,2),new Color(1,.86f,.44f,.55f));
            tagStyle.normal.textColor=new Color(1,.98f,.86f,.8f);GUI.Label(box,name,tagStyle);
        }
        static void DrawLeader(Vector2 start,Vector2 end,Color color)
        {
            Vector2 d=end-start;float length=d.magnitude;if(length<1)return;
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=old*Matrix4x4.TRS(start,Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,-.6f,length,1.2f),color);GUI.matrix=old;
        }
        IEnumerator CaptureCookGif()
        {
            string path=CapturePath();
            sim.RainEnabled=false;
            yield return new WaitForSecondsRealtime(.7f);
            for(int frame=0;frame<42;frame++)
            {
                if(frame==5)Send(0,Cookbook.Find("mapo"));
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(path,$"frame-{frame:D3}.png"));
                yield return new WaitForSecondsRealtime(.09f);
            }
            yield return new WaitForSecondsRealtime(2f);
            File.WriteAllText(Path.Combine(path,"capture.txt"),$"Cooked commands: {sim.SuccessfulCommands}\nRuntime errors: {runtimeErrors}\nResolution: {Screen.width}x{Screen.height}\n");
            Application.Quit(sim.SuccessfulCommands>0 && runtimeErrors==0?0:1);
        }
        IEnumerator SmokeRun()
        {
            yield return new WaitForSeconds(1.2f);
            Send(0,Cookbook.Find("chahan"));yield return new WaitForSeconds(1.1f);
            Send(1,Cookbook.Find("omurice"));yield return new WaitForSeconds(1.1f);
            Send(0,Cookbook.Find("curry"));yield return new WaitForSeconds(1.1f);
            Send(1,Cookbook.Find("currydoria"));yield return new WaitForSeconds(1.1f);
            Send(0,Cookbook.Find("gyoza"));yield return new WaitForSeconds(1.1f);
            Send(1,Cookbook.Find("mapo"));yield return new WaitForSeconds(1.1f);
            Send(0,Cookbook.Find("subuta"));yield return new WaitForSeconds(1.1f);
            Send(1,Cookbook.Find("cheesecurrydoria"));yield return new WaitForSeconds(1.1f);
            Send(0,Cookbook.Find("chickenrice"));yield return new WaitForSeconds(.28f);
            string path=CapturePath();
            ScreenCapture.CaptureScreenshot(Path.Combine(path,"prototype.png"));
            yield return new WaitForSeconds(1.5f);
            bool passed=runtimeErrors==0 && sim.SuccessfulCommands>=8 && sim.State.CyanScore>0 && sim.State.OrangeScore>0;
            File.WriteAllText(Path.Combine(path,"player-smoke.txt"),$"Result: {(passed?"PASS":"FAIL")}\nRuntime errors: {runtimeErrors}\nSuccessful commands: {sim.SuccessfulCommands}\nCyan: {sim.State.CyanScore}\nOrange: {sim.State.OrangeScore}\nRendered food models: {view.VisibleFoodCount}\nTotal ECS food visuals: {view.TotalFoodCount}\nGraphics: {SystemInfo.graphicsDeviceName}\nResolution: {Screen.width}x{Screen.height}\n");
            Application.Quit(passed?0:1);
        }
        static string CapturePath()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-feast-output");
            string path=index>=0 && index+1<args.Length?args[index+1]:Application.persistentDataPath;
            Directory.CreateDirectory(path);return path;
        }
        void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(webInputOwner==this)
            {
                HideWebInput();
                FeastWebInput_Destroy();
                webInputOwner=null;
            }
#endif
            Application.logMessageReceived-=CountErrors;hud?.Dispose();
            if(sim!=null){sim.Cooked-=OnCooked;sim.Failed-=OnFailed;sim.Served-=OnServed;}
            DisposeReceiptUI();
            if(!smoke) PlayerPrefs.Save();
        }
    }
}
