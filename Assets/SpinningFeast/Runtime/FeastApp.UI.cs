using UnityEngine;

namespace SpinningFeast
{
    public sealed partial class FeastApp
    {
        static readonly Color Paper=new Color(1,.945f,.79f);
        static readonly Color Vermilion=new Color(.82f,.16f,.105f);
        static readonly Color Ink=new Color(.25f,.105f,.075f);
        static readonly Color Yolk=new Color(1,.76f,.23f);
        static readonly Color Jade=new Color(.055f,.37f,.31f);
        Texture2D uiDisc;
        void Disc(Rect r,Color color)
        {
            if(uiDisc==null)
            {
                const int n=256;uiDisc=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Feast UI circle",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01(n*.5f-Vector2.Distance(new Vector2(x,y),new Vector2((n-1)*.5f,(n-1)*.5f))));
                uiDisc.SetPixels(pixels);uiDisc.Apply(false,true);
            }
            Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,uiDisc);GUI.color=old;
        }
        void Round(Rect r,Color c,float radius=18)
        {
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
            Fill(new Rect(r.x+radius-.5f,r.y,r.width-2*radius+1,r.height),c);
            Fill(new Rect(r.x,r.y+radius-.5f,r.width,r.height-2*radius+1),c);
            Disc(new Rect(r.x,r.y,radius*2,radius*2),c);Disc(new Rect(r.xMax-radius*2,r.y,radius*2,radius*2),c);
            Disc(new Rect(r.x,r.yMax-radius*2,radius*2,radius*2),c);Disc(new Rect(r.xMax-radius*2,r.yMax-radius*2,radius*2,radius*2),c);
        }
        void Card(Rect r,Color body,Color edge,float radius=22,float border=4)
        {
            Round(new Rect(r.x+3,r.y+6,r.width,r.height),Ink,radius);
            Round(r,edge,radius);
            Round(new Rect(r.x+border,r.y+border,r.width-2*border,r.height-2*border),body,Mathf.Max(2,radius-border));
        }
        void Stroke(Vector2 from,Vector2 to,float width,Color c)
        {
            Matrix4x4 old=GUI.matrix;Vector2 delta=to-from;
            GUI.matrix=old*Matrix4x4.TRS(from,Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,-width*.5f,delta.magnitude,width),c);GUI.matrix=old;
        }
        void MenuLabel(Rect r,string text,int size,Color c)
        {
            menuLabelStyle.fontSize=size;menuLabelStyle.fontStyle=FontStyle.Bold;menuLabelStyle.normal.textColor=c;GUI.Label(r,text,menuLabelStyle);
        }
        void Headline(Rect r,string text,int size,Color face,Color edge)
        {
            menuTitleStyle.fontSize=size;menuTitleStyle.normal.textColor=Ink;
            GUI.Label(new Rect(r.x+4,r.y+7,r.width,r.height),text,menuTitleStyle);
            menuTitleStyle.normal.textColor=edge;
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;
                GUI.Label(new Rect(r.x+Mathf.Cos(a)*3,r.y+Mathf.Sin(a)*3,r.width,r.height),text,menuTitleStyle);
            }
            menuTitleStyle.normal.textColor=face;GUI.Label(r,text,menuTitleStyle);
        }
        void KeyPattern(float x,float y,int count,Color c)
        {
            for(int i=0;i<count;i++)
            {
                float a=x+i*28;
                Fill(new Rect(a,y,23,3),c);Fill(new Rect(a,y,3,18),c);
                Fill(new Rect(a,y+15,16,3),c);Fill(new Rect(a+13,y+7,3,11),c);
                Fill(new Rect(a+7,y+7,9,3),c);Fill(new Rect(a+21,y,3,24),c);
            }
        }
        void Cloud(float x,float y,float scale)
        {
            Color c=new Color(.96f,.83f,.59f);Matrix4x4 old=GUI.matrix;
            GUI.matrix=old*Matrix4x4.TRS(new Vector3(x,y,0),Quaternion.identity,Vector3.one*scale);
            Round(new Rect(-55,0,110,23),c,11);Disc(new Rect(-30,-21,45,45),c);Disc(new Rect(2,-11,33,33),c);
            GUI.matrix=old;
        }
        void Lantern(float x,float y,float phase)
        {
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=old*Matrix4x4.TRS(new Vector3(x,y,0),Quaternion.Euler(0,0,Mathf.Sin(Time.unscaledTime*1.3f+phase)*3),Vector3.one);
            Fill(new Rect(-2,-100,4,100),Ink);
            Disc(new Rect(-57,0,114,139),Ink);Disc(new Rect(-53,3,106,129),Vermilion);
            Disc(new Rect(-35,4,70,127),new Color(.96f,.25f,.13f));Disc(new Rect(-18,4,36,127),new Color(1,.34f,.17f));
            Round(new Rect(-34,-3,68,12),Yolk,4);Round(new Rect(-34,125,68,12),Yolk,4);
            Fill(new Rect(-3,137,6,20),Ink);Disc(new Rect(-8,151,16,16),Yolk);
            for(int i=-2;i<=2;i++)Fill(new Rect(i*5-1,164,3,28-Mathf.Abs(i)*3),Vermilion);
            MenuLabel(new Rect(-43,37,86,65),"福",42,Paper);GUI.matrix=old;
        }
        void Bowl(float x,float y,float scale,Color accent)
        {
            Matrix4x4 old=GUI.matrix;GUI.matrix=old*Matrix4x4.TRS(new Vector3(x,y,0),Quaternion.Euler(0,0,-8),Vector3.one*scale);
            Stroke(new Vector2(10,-38),new Vector2(74,-100),7,Ink);Stroke(new Vector2(27,-34),new Vector2(90,-94),7,Ink);
            Disc(new Rect(-68,-33,136,95),Ink);Disc(new Rect(-61,-28,122,81),accent);
            Round(new Rect(-27,45,54,14),Ink,4);Disc(new Rect(-68,-42,136,45),Ink);Disc(new Rect(-60,-38,120,33),Paper);
            for(int i=0;i<4;i++)
            {Disc(new Rect(-40+i*21,-31,28,19),Yolk);Stroke(new Vector2(-37+i*21,-27),new Vector2(-20+i*21,-18),3,Vermilion);}
            Round(new Rect(-32,12,64,6),Paper,3);GUI.matrix=old;
        }
        void Dumpling(float x,float y,float scale)
        {
            Matrix4x4 old=GUI.matrix;GUI.matrix=old*Matrix4x4.TRS(new Vector3(x,y,0),Quaternion.Euler(0,0,12),Vector3.one*scale);
            Disc(new Rect(-82,-13,164,60),Ink);Disc(new Rect(-78,-10,156,52),Jade);Disc(new Rect(-68,-8,136,40),Paper);
            Disc(new Rect(-57,-56,114,77),Ink);Disc(new Rect(-52,-51,104,66),Yolk);Disc(new Rect(-48,-49,96,59),Paper);
            for(int i=-2;i<=2;i++)Stroke(new Vector2(i*15,-43+Mathf.Abs(i)*4),new Vector2(i*19,-20),4,new Color(.79f,.52f,.27f));
            GUI.matrix=old;
        }
        bool MenuButton(Rect r,string text,Color accent,bool selected=false)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            Color body=selected?accent:hover?Yolk:Paper;
            Card(r,body,Ink,18,3);
            menuButtonStyle.normal.textColor=menuButtonStyle.hover.textColor=menuButtonStyle.active.textColor=selected?Paper:Ink;
            bool hit=GUI.Button(r,text,menuButtonStyle);if(hit)sound.Select();return hit;
        }
        void DrawMenu(MatchState state,Event e)
        {
            bool title=state.Phase==MatchPhase.Title;menuButtonStyle.fontSize=23;
            if(!title && (e.type==EventType.KeyDown || e.type==EventType.KeyUp) && (e.keyCode==KeyCode.Return || e.keyCode==KeyCode.KeypadEnter))e.Use();
            Matrix4x4 matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;
            Fill(new Rect(0,0,Screen.width,Screen.height),Paper);GUI.matrix=matrix;
            // Offset cloud groups and sign-painter borders frame the menu without covering controls.
            for(int i=0;i<6;i++){Cloud(70+i*238,65+(i%2)*12,.85f);Cloud(25+i*245,753-(i%2)*9,.65f);}
            Fill(new Rect(0,0,1280,12),Vermilion);Fill(new Rect(0,788,1280,12),Vermilion);
            KeyPattern(32,26,44,Vermilion);KeyPattern(32,748,44,Vermilion);
            Lantern(145,140,.2f);Lantern(1135,140,1.2f);
            if(!title && Event.current.type==EventType.Repaint && hud!=null && hud.PhaseAge<4)
            {
                float age=hud.PhaseAge;
                for(int i=0;i<32;i++)
                {
                    float x=i%2==0?38+(i*37)%164:1078+(i*37)%164;
                    float y=Mathf.Repeat(i*73+age*(55+i%5*14),730)+50;
                    Color c=i%3==0?Yolk:i%3==1?Vermilion:Jade;
                    Stroke(new Vector2(x,y),new Vector2(x+Mathf.Sin(i+age*4)*9,y+9),5,c);
                }
            }
            Card(new Rect(250,98,780,614),Paper,Vermilion,32,6);
            if(!title)
            {
                Round(new Rect(478,78,324,48),Vermilion,16);
                MenuLabel(new Rect(478,79,324,43),"本 日 の 勝 負",23,Paper);
            }
            Dumpling(137,568,1.07f);Bowl(1141,552,1.03f,Vermilion);
            MenuLabel(new Rect(67,631,140,36),"できたて！",20,Vermilion);
            MenuLabel(new Rect(1069,624,145,36),"大盛上等",20,Vermilion);
            if(title)
            {
                Headline(new Rect(276,161,728,132),"熱烈電卓町中華",86,Vermilion,Paper);
                Round(new Rect(548,363,184,30),Yolk,15);MenuLabel(new Rect(548,362,184,30),"CPU の強さ",17,Ink);
                for(int level=0;level<3;level++)
                {
                    Rect r=new Rect(286+240*level,407,228,65);
                    if(MenuButton(r,FeastCpu.Name(level),Vermilion,cpuLevel==level))SelectCpu(level);
                    if(cpuLevel==level)Disc(new Rect(r.center.x-5,483,10,10),Vermilion);
                }
                MenuLabel(new Rect(280,499,720,29),FeastCpu.Description(cpuLevel),17,Jade);
                menuButtonStyle.fontSize=30;
                if(MenuButton(new Rect(405,549,470,70),"いただきます！",Vermilion,true)){BeginMatch();return;}
                menuButtonStyle.fontSize=23;
                MenuLabel(new Rect(315,638,650,27),"← → で強さを選ぶ    ENTER で開店",16,Ink);
                menuLabelStyle.normal.textColor=Jade;
                if(GUI.Button(new Rect(891,674,113,29),sound.MusicEnabled?"♪ ON":"♪ OFF",menuLabelStyle))sound.ToggleMusic();
            }
            else
            {
                int winner=state.CyanScore==state.OrangeScore?-1:state.CyanScore>state.OrangeScore?0:1;
                Headline(new Rect(280,132,720,74),winner<0?"いい勝負！":winner==0?"あなたの勝ち！":"CPUの勝ち！",48,Vermilion,Paper);
                float reveal=hud!=null?hud.PhaseAge:10;
                for(int p=0;p<2;p++)
                {
                    float x=p==0?286:662;Color accent=p==0?FeastView.Cyan:FeastView.Orange;
                    Card(new Rect(x,217,332,94),Paper,Ink,20,3);
                    Round(new Rect(x+5,222,322,31),accent,13);
                    MenuLabel(new Rect(x+5,222,322,30),p==0?"あなた":"CPU ・ "+FeastCpu.Name(cpuLevel),18,Ink);
                    MenuLabel(new Rect(x+8,254,316,51),Mathf.RoundToInt((p==0?state.CyanScore:state.OrangeScore)*(1-Mathf.Pow(1-Mathf.Clamp01(reveal/1.2f),3))).ToString("N0"),35,Ink);
                    if(p==winner){Disc(new Rect(x+288,285,36,36),Vermilion);MenuLabel(new Rect(x+288,285,36,34),"勝",19,Paper);}
                }
                DrawResultDishes(e);
                menuButtonStyle.fontSize=21;
                if(MenuButton(new Rect(310,615,315,48),"もう一戦！  F5",Vermilion,true)){BeginMatch();return;}
                if(MenuButton(new Rect(655,615,315,48),"タイトルへ",Jade)){ShowTitle();return;}
                MenuLabel(new Rect(300,674,680,26),"F5 で再戦    ESC でタイトル",15,Jade);
            }
            if(e.type!=EventType.KeyDown)return;
            if(e.keyCode==KeyCode.F5 || (title && (e.keyCode==KeyCode.Return || e.keyCode==KeyCode.KeypadEnter))){e.Use();BeginMatch();}
            else if(!title && e.keyCode==KeyCode.Escape){e.Use();ShowTitle();}
            else if(title && (e.keyCode==KeyCode.LeftArrow || e.keyCode==KeyCode.RightArrow)){SelectCpu((cpuLevel+(e.keyCode==KeyCode.RightArrow?1:2))%3);e.Use();}
        }
        void DrawResultDishes(Event e)
        {
            int cyanKinds=0,orangeKinds=0;
            foreach(var row in resultDishes){if(row.Player==0)cyanKinds++;else orangeKinds++;}
            int pages=Mathf.Max(1,(Mathf.Max(cyanKinds,orangeKinds)+ResultPageSize-1)/ResultPageSize);
            if(e.type==EventType.KeyDown && (e.keyCode==KeyCode.LeftArrow || e.keyCode==KeyCode.RightArrow))
            {resultPage=(resultPage+(e.keyCode==KeyCode.RightArrow?1:pages-1))%pages;if(pages>1)sound.Page();e.Use();}
            if(pages>1 && e.type==EventType.ScrollWheel && new Rect(280,320,720,285).Contains(e.mousePosition))
            {resultPage=Mathf.Clamp(resultPage+(e.delta.y>0?1:-1),0,pages-1);sound.Page();e.Use();}
            for(int p=0;p<2;p++)
            {
                float x=p==0?286:662;int total=0,kinds=p==0?cyanKinds:orangeKinds,ordinal=0;
                foreach(var row in resultDishes)if(row.Player==p)total+=row.Count;
                Round(new Rect(x,329,332,229),new Color(.97f,.86f,.65f),16);
                MenuLabel(new Rect(x+9,335,314,28),$"お品書き　{kinds}品・{total:N0}人前",16,Jade);
                Fill(new Rect(x+15,368,302,2),Vermilion);
                foreach(var row in resultDishes)
                {
                    if(row.Player!=p)continue;
                    int slot=ordinal++-resultPage*ResultPageSize;
                    if(slot<0 || slot>=ResultPageSize)continue;
                    float y=374+slot*29;
                    menuLabelStyle.alignment=TextAnchor.MiddleLeft;menuLabelStyle.fontSize=18;
                    string name=Cookbook.All[row.Recipe].Name;
                    float width=menuLabelStyle.CalcSize(new GUIContent(name)).x;
                    MenuLabel(new Rect(x+15,y,223,28),name,width>223?Mathf.Max(10,Mathf.FloorToInt(18*223/width)):18,Ink);
                    menuLabelStyle.alignment=TextAnchor.MiddleRight;
                    MenuLabel(new Rect(x+238,y,79,28),$"{row.Count:N0}人前",16,Ink);
                    menuLabelStyle.alignment=TextAnchor.MiddleCenter;
                }
                if(kinds==0)MenuLabel(new Rect(x+12,414,308,50),"まだ料理がありません",17,Jade);
                else if(resultPage*ResultPageSize>=kinds)MenuLabel(new Rect(x+12,414,308,50),"ここまでで全品！",17,Jade);
            }
            menuButtonStyle.fontSize=18;
            if(pages>1)
            {
                if(MenuButton(new Rect(467,570,51,31),"◀",Jade)){resultPage=(resultPage+pages-1)%pages;sound.Page();}
                if(MenuButton(new Rect(762,570,51,31),"▶",Jade)){resultPage=(resultPage+1)%pages;sound.Page();}
            }
            MenuLabel(new Rect(522,569,236,30),pages>1?$"← お品書き {resultPage+1} / {pages} →":"ごちそうさまでした！",15,Jade);
        }
        void DrawMatchHud(MatchState state)
        {
            for(int p=0;p<2;p++)
            {
                float x=p==0?65:905;Color accent=p==0?FeastView.Cyan:FeastView.Orange;
                Card(new Rect(x,52,310,78),Paper,Ink,24,4);
                Round(new Rect(x+9,61,60,60),accent,19);
                // A bowl icon marks each side; permanent HUD text remains score, time and input only.
                Disc(new Rect(x+18,78,43,27),Ink);Disc(new Rect(x+22,77,35,21),Paper);
                Fill(new Rect(x+29,104,20,4),Ink);Fill(new Rect(x+17,76,45,4),Ink);
                scoreStyle.fontSize=36+Mathf.RoundToInt((hud!=null?hud.ScorePulse(p):0)*5);
                scoreStyle.alignment=TextAnchor.MiddleCenter;scoreStyle.normal.textColor=Color.Lerp(Ink,Vermilion,flashes[p]);
                GUI.Label(new Rect(x+75,55,222,68),Mathf.RoundToInt(shownScores[p]).ToString("N0"),scoreStyle);
            }
            Card(new Rect(553,43,174,87),state.Remaining<=15?Vermilion:Paper,Ink,29,4);
            int remaining=Mathf.CeilToInt(state.Remaining);
            timeStyle.fontSize=32+(remaining<=10?Mathf.RoundToInt(Mathf.Pow(Mathf.Clamp01(1-(remaining-state.Remaining)*3),2)*4):0);
            timeStyle.fontStyle=FontStyle.Bold;timeStyle.normal.textColor=state.Remaining<=15?Paper:Vermilion;
            GUI.Label(new Rect(558,58,164,58),$"{remaining/60:00}:{remaining%60:00}",timeStyle);
            float inputX=340+(hud!=null?hud.InputShake(0):0);
            Card(new Rect(inputX-9,695,618,70),Paper,Ink,24,4);
            Round(new Rect(inputX-3,701,606,58),Color.Lerp(FeastView.Cyan,Paper,failed[0]?flashes[0]:0),19);
            Round(new Rect(inputX+1,705,598,50),Paper,16);
            Rect inputRect=new Rect(inputX,703,600,54);
#if UNITY_WEBGL && !UNITY_EDITOR
            if(!codex && state.Phase==MatchPhase.Playing && Event.current.type==EventType.Repaint)PlaceWebInput(inputRect);
            else if(state.Phase==MatchPhase.Countdown && Event.current.type==EventType.Repaint)HideWebInput();
#else
            Color oldCursor=GUI.skin.settings.cursorColor;GUI.skin.settings.cursorColor=Ink;
            GUI.SetNextControlName("dish0");
            if(!codex && state.Phase==MatchPhase.Playing)inputs[0]=GUI.TextField(inputRect,inputs[0],48,inputStyle);
            else GUI.Box(inputRect,"",inputStyle);
            GUI.skin.settings.cursorColor=oldCursor;
            if(!codex && state.Phase==MatchPhase.Playing)GUI.FocusControl("dish0");
#endif
        }
    }
}
