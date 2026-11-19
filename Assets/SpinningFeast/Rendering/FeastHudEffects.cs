using System.Collections.Generic;
using UnityEngine;

namespace SpinningFeast
{
    // Temporary ornaments use the existing 1280 x 800 HUD coordinate space.
    public sealed class FeastHudEffects
    {
        // Optional UI Toolkit anchor expressed in the existing presentation coordinate space.
        public System.Func<int,Vector2> ScoreTarget;
        struct Fleck {public Vector2 Start,Velocity;public float Born,Life,Size,Angle,Spin;public Color Color;public int Kind;}
        struct ScoreFlight {public CookCue Cue;public float Born;}
        struct EatFlight {public EatingCue Cue;public float Born;public int WordIndex;}
        const int MaxFlecks=192,MaxFlights=32;
        static readonly string[] GradeWords={"好！","大盛！","豪華！","宴だ！"};
        static readonly string[] EatingWords={
            "うまい！","美味しい！","ごちそうさま！","絶品！","最高！","おかわり！",
            "もう一皿！","たまらない！","大満足！","しあわせ！","箸が止まらない！",
            "これは旨い！","やみつき！","いい香り！","熱々で最高！","また食べたい！",
            "ごちそう！","うますぎる！"
        };
        readonly List<Fleck> flecks=new List<Fleck>(MaxFlecks);
        readonly List<ScoreFlight> flights=new List<ScoreFlight>(MaxFlights);
        readonly List<EatFlight> eatFlights=new List<EatFlight>(MaxFlights);
        readonly string[] previousInput={"",""};
        readonly float[] scoreAt={-100,-100},leadAt={-100,-100};
        readonly float[] goldAt={-100,-100};
        readonly int[] goldPoints={0,0};
        int previousLeader=-1,lastEatingWord=-1;float lastLeadSound=-100;
        readonly float[] typedAt={-100,-100},submittedAt={-100,-100},failedAt={-100,-100};
        FeastSystem simulation;FeastView view;FeastEffects worldEffects;FeastSound sound;Font font;
        GUIStyle effectText;
        System.Random random=new System.Random(61217);
        MatchPhase previousPhase;
        int epoch,lastSecond=121;
        float phaseAt;
        public float PhaseAge => Mathf.Max(0,Time.unscaledTime-phaseAt);
        public void Initialize(FeastSystem sim,FeastView renderer,FeastEffects effects,FeastSound audio,Font face)
        {
            simulation=sim;view=renderer;worldEffects=effects;sound=audio;font=face;
            simulation.CookingCue+=OnCooking;simulation.Failed+=OnFailed;simulation.ArrivalCue+=OnArrival;simulation.Eating+=OnEating;
            Reset();
        }
        public void Dispose()
        {if(simulation!=null){simulation.CookingCue-=OnCooking;simulation.Failed-=OnFailed;simulation.ArrivalCue-=OnArrival;simulation.Eating-=OnEating;}}
        public void Reset()
        {
            epoch=simulation.State.Epoch;previousPhase=simulation.State.Phase;phaseAt=Time.unscaledTime;lastSecond=121;
            goldAt[0]=goldAt[1]=-100;goldPoints[0]=goldPoints[1]=0;
            flecks.Clear();flights.Clear();eatFlights.Clear();previousInput[0]=previousInput[1]="";
            previousLeader=-1;lastEatingWord=-1;lastLeadSound=-100;
            for(int i=0;i<2;i++)typedAt[i]=submittedAt[i]=failedAt[i]=scoreAt[i]=leadAt[i]=-100;
        }
        float Random(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
        static Color Actor(int p)=>p==0?FeastView.Cyan:FeastView.Orange;
        void Emit(Vector2 p,Vector2 velocity,Color color,float size,float life,int kind)
        {
            if(flecks.Count>=MaxFlecks)return;
            flecks.Add(new Fleck {Start=p,Velocity=velocity,Color=color,Size=size,Life=life,Born=Time.unscaledTime,Kind=kind,Angle=Random(-1,1),Spin=Random(-3,3)});
        }
        public void ObserveInput(int player,string text)
        {
            if(simulation.State.Phase!=MatchPhase.Playing)return;
            if(text.Length>previousInput[player].Length)
            {
                typedAt[player]=Time.unscaledTime;sound.Type(player);
                float x=340+20+Mathf.Min(550,text.Length*13);
                for(int i=0;i<Mathf.Min(8,(text.Length-previousInput[player].Length)*2);i++)
                    Emit(new Vector2(x,701),new Vector2(Random(-26,26),Random(-55,-18)),Actor(player),Random(3,6),Random(.3f,.6f),player==0?0:3);
            }
            previousInput[player]=text;
        }
        public void Submit(int player)
        {
            if(player!=0)return;
            submittedAt[player]=Time.unscaledTime;
            for(int i=0;i<14;i++)Emit(new Vector2(640,702),new Vector2(Random(-180,180),Random(-100,-25)),Actor(player),Random(3,6),.5f,i%3==0?1:0);
        }
        void OnFailed(int player){failedAt[player]=Time.unscaledTime;}
        void OnCooking(CookCue cue)
        {
            if(epoch!=simulation.State.Epoch)Reset();
            if(flights.Count>=MaxFlights)flights.RemoveAt(0);
            flights.Add(new ScoreFlight {Cue=cue,Born=Time.unscaledTime});
            if(cue.BonusPoints>0)
            {
                goldAt[cue.Player]=Time.unscaledTime;goldPoints[cue.Player]=cue.BonusPoints;
                Vector2 center=new Vector2(cue.Player==0?350:930,245);
                for(int i=0;i<28;i++)
                {
                    float a=i*Mathf.PI*2/28;
                    Emit(center,new Vector2(Mathf.Cos(a)*Random(100,240),Mathf.Sin(a)*Random(75,165)),i%3==0?Color.white:FeastView.Golden,Random(6,13),Random(.7f,1.4f),i%2==0?1:3);
                }
            }
        }
        void OnArrival(CookCue cue)
        {
            scoreAt[cue.Player]=Time.unscaledTime;
            if(cue.Discovery || cue.BonusPoints>0)sound.Discover(cue.Player);
            else if(cue.Chain)sound.Chain(cue.Player);
        }
        void OnEating(EatingCue cue)
        {
            if(epoch!=simulation.State.Epoch)Reset();
            if(eatFlights.Count>=MaxFlights)eatFlights.RemoveAt(0);
            int wordIndex=lastEatingWord<0?random.Next(EatingWords.Length):random.Next(EatingWords.Length-1);
            if(lastEatingWord>=0 && wordIndex>=lastEatingWord)wordIndex++;
            lastEatingWord=wordIndex;
            eatFlights.Add(new EatFlight {Cue=cue,Born=Time.unscaledTime,WordIndex=wordIndex});
            scoreAt[cue.Player]=Time.unscaledTime;
            sound.Eating(cue.Player,cue.Count);
        }
        public void Update()
        {
            if(simulation==null)return;
            var state=simulation.State;
            if(epoch!=state.Epoch)Reset();
            if(state.Phase!=previousPhase){previousPhase=state.Phase;phaseAt=Time.unscaledTime;}
            if(state.Phase==MatchPhase.Countdown)
            {
                int second=Mathf.CeilToInt(state.CountdownRemaining);
                if(second!=lastSecond && second>0){sound.Tick(second);lastSecond=second;}
            }
            if(state.Phase==MatchPhase.Playing)
            {
                int leader=state.CyanScore==state.OrangeScore?-1:state.CyanScore>state.OrangeScore?0:1;
                if(leader>=0 && previousLeader>=0 && leader!=previousLeader)
                {
                    leadAt[leader]=Time.unscaledTime;
                    if(Time.unscaledTime-lastLeadSound>1){lastLeadSound=Time.unscaledTime;sound.Lead(leader);}
                }
                if(leader>=0)previousLeader=leader;
                int second=Mathf.CeilToInt(state.Remaining);
                if(second!=lastSecond && second>0 && second<=10)sound.Tick(second);
                lastSecond=second;
            }
            for(int i=flecks.Count-1;i>=0;i--)if(Time.unscaledTime-flecks[i].Born>=flecks[i].Life)flecks.RemoveAt(i);
            for(int i=flights.Count-1;i>=0;i--)if(Time.unscaledTime-flights[i].Born>=2.1f)flights.RemoveAt(i);
            for(int i=eatFlights.Count-1;i>=0;i--)if(Time.unscaledTime-eatFlights[i].Born>=2.4f)eatFlights.RemoveAt(i);
        }
        public float TypePulse(int player)
        {float t=Time.unscaledTime-typedAt[player];return t<.2f?Mathf.Sin(t/.2f*Mathf.PI)*.055f:0;}
        public float ScorePulse(int player)
        {float t=Time.unscaledTime-scoreAt[player];return t<.45f?Mathf.Sin(t/.45f*Mathf.PI)*(1-t/.45f):0;}
        public void DrawTransition()
        {
            if(Event.current.type!=EventType.Repaint)return;
            float age=PhaseAge;
            if(age<.28f)
            {
                Matrix4x4 matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;
                Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.015f,.045f,.055f,(1-age/.28f)*.55f));GUI.matrix=matrix;
            }
        }
        public float InputShake(int player)
        {
            float t=Time.unscaledTime-failedAt[player];return t<.35f?Mathf.Sin(t*55)*(1-t/.35f)*3:0;
        }
        void Style()
        {
            if(effectText==null)effectText=new GUIStyle(GUI.skin.label){font=font,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,richText=false};
        }
        static void Fill(Rect rect,Color color)
        {Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        void Sprite(Vector2 point,float size,Color color,int kind=0,float angle=0)
        {
            Color old=GUI.color;Matrix4x4 matrix=GUI.matrix;GUI.color=color;
            GUI.matrix=matrix*Matrix4x4.TRS(point,Quaternion.Euler(0,0,angle),Vector3.one);
            GUI.DrawTextureWithTexCoords(new Rect(-size,-size,size*2,size*2),worldEffects.Atlas,new Rect(kind*.25f,0,.25f,1));
            GUI.matrix=matrix;GUI.color=old;
        }
        void Words(Vector2 point,string words,int size,Color color,float scale=1)
        {
            Style();Matrix4x4 matrix=GUI.matrix;
            GUI.matrix=matrix*Matrix4x4.TRS(point,Quaternion.identity,Vector3.one*scale);
            float height=Mathf.Max(80,size*1.6f);
            effectText.fontSize=size;float width=Mathf.Max(480,effectText.CalcSize(new GUIContent(words)).x+48);
            effectText.normal.textColor=new Color(.015f,.04f,.05f,color.a*.8f);
            GUI.Label(new Rect(-width*.5f+3,-height*.5f+3,width,height),words,effectText);
            effectText.normal.textColor=color;GUI.Label(new Rect(-width*.5f,-height*.5f,width,height),words,effectText);GUI.matrix=matrix;
        }
        static float FadeOut(float age,float start,float end)=>1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,age));
        public static float EatingSplashOpacity(float age)=>age<0 || age>=2.4f?0:FadeOut(age,1.35f,2.4f);
        void DrawEatingMessage()
        {
            foreach(var f in eatFlights)
            {
                float age=Time.unscaledTime-f.Born;
                if(age<0 || age>=2.4f)continue;
                Color actor=Actor(f.Cue.Player);
                float wordOpacity=Mathf.Clamp01(age/.18f)*EatingSplashOpacity(age);
                Color wordColor=f.WordIndex%3==0
                    ?new Color(actor.r,actor.g,actor.b,wordOpacity)
                    :new Color(1,.96f,.72f,wordOpacity);
                float pop=1.08f+.2f*Mathf.Sin(Mathf.Clamp01(age/.45f)*Mathf.PI);
                Words(new Vector2(640,385-age*16),EatingWords[f.WordIndex],82,wordColor,pop);
            }
        }
        Vector2 Project(Vector3 world,float scale)
        {
            Vector3 p=view.ViewCamera.WorldToScreenPoint(world);
            Vector2 offset=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-800*scale)*.5f);
            return (new Vector2(p.x,Screen.height-p.y)-offset)/scale;
        }
        public void DrawPlaying(float scale,bool bookOpen)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var state=simulation.State;
            if(state.Phase==MatchPhase.Countdown)
            {
                int number=Mathf.Clamp(Mathf.CeilToInt(state.CountdownRemaining),1,3);
                float beat=1-(state.CountdownRemaining-Mathf.Floor(state.CountdownRemaining));
                float pop=1.35f-.28f*Mathf.Clamp01(beat*2);
                Color red=new Color(.92f,.16f,.08f,.91f);
                Fill(new Rect(458,230,364,202),new Color(.13f,.025f,.015f,.62f));
                Fill(new Rect(458,230,364,7),FeastView.Golden);
                Words(new Vector2(640,325),number.ToString(),112,new Color(1,.95f,.69f,.98f),pop);
                for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI/8+Time.unscaledTime*.7f;
                    Sprite(new Vector2(640+Mathf.Cos(a)*(120+beat*80),325+Mathf.Sin(a)*(100+beat*65)),5+3*(1-beat),i%3==0?FeastView.Golden:red,i%4==0?1:3,Time.unscaledTime*90+i*24);
                }
                return;
            }
            DrawEatingMessage();
            if(!bookOpen)
            {
                foreach(var f in flights)
                {
                    float age=Time.unscaledTime-f.Born,t=Mathf.Clamp01((age-.3f)/.95f);
                    int grade=FeastEffects.GradeForPortions(f.Cue.Count);
                    Vector3 world=FeastSystem.PlatePosition(f.Cue.Slot,state.Angle);world.y+=.8f;
                    Vector2 origin=Project(world,scale);origin.x=Mathf.Clamp(origin.x,160,1120);origin.y=Mathf.Clamp(origin.y,180,590);
                    Vector2 target=ScoreTarget!=null?ScoreTarget(f.Cue.Player):new Vector2(f.Cue.Player==0?190:1090,92);
                    float smooth=t*t*(3-2*t);Vector2 point=Vector2.Lerp(origin,target,smooth);point.y-=Mathf.Sin(t*Mathf.PI)*65;
                    Color color=Actor(f.Cue.Player);color.a=Mathf.Clamp01(age/.15f)*(1-Mathf.SmoothStep(.78f,1,t));
                    int points=f.Cue.Count*Cookbook.All[f.Cue.Recipe].Points+f.Cue.BonusPoints;
                    Words(point,"+"+points.ToString("N0"),27,color,1+.12f*Mathf.Sin(Mathf.Clamp01(age/.3f)*Mathf.PI));
                    if(age<.65f)
                    {
                        Color portions=Actor(f.Cue.Player);portions.a=(1-age/.65f)*.8f;
                        Words(new Vector2(f.Cue.Player==0?395:885,615-age*32),"×"+f.Cue.Count,22,portions);
                    }
                    if(age<1.9f)
                    {
                        float fade=1-Mathf.SmoothStep(1.1f,1.9f,age);
                        Color stamp=grade>=2?FeastView.Golden:Actor(f.Cue.Player);stamp.a=fade;
                        Vector2 center=origin+new Vector2(0,-75-grade*8-age*14);
                        float pop=1+.4f*Mathf.Sin(Mathf.Clamp01(age/.3f)*Mathf.PI)*Mathf.Exp(-age);
                        Words(center,GradeWords[grade],28+grade*9,stamp,pop);
                        int stars=4+grade*5;
                        for(int i=0;i<stars;i++)
                        {
                            float a=i*Mathf.PI*2/stars+age*2;
                            Sprite(center+new Vector2(Mathf.Cos(a)*(68+grade*19+age*65),Mathf.Sin(a)*(34+grade*11+age*35)),6+grade*4,stamp,i%3==0?1:3,age*110+i*35);
                        }
                        if(grade>=2 && age<.38f)
                        {
                            Color edge=stamp;edge.a=(1-age/.38f)*(grade==3?.22f:.11f);
                            Fill(new Rect(0,0,1280,8+grade*3),edge);
                            Fill(new Rect(0,800-(8+grade*3),1280,8+grade*3),edge);
                            Fill(new Rect(0,0,8+grade*3,800),edge);
                            Fill(new Rect(1280-(8+grade*3),0,8+grade*3,800),edge);
                        }
                    }
                    if(t>0 && t<.9f)Sprite(point+new Vector2(f.Cue.Player==0?-70:70,0),7,color,1,age*80);
                }
                foreach(var f in eatFlights)
                {
                    float age=Time.unscaledTime-f.Born;
                    float t=Mathf.Clamp01((age-.45f)/1.05f),smooth=t*t*(3-2*t);
                    Vector3 world=FeastSystem.PlatePosition(f.Cue.Slot,state.Angle);world.y+=1.5f;
                    Vector2 origin=Project(world,scale);origin.x=Mathf.Clamp(origin.x,170,1110);origin.y=Mathf.Clamp(origin.y,190,570);
                    Vector2 target=ScoreTarget!=null?ScoreTarget(f.Cue.Player):new Vector2(f.Cue.Player==0?190:1090,92);
                    Vector2 point=Vector2.Lerp(origin,target,smooth);point.y-=Mathf.Sin(t*Mathf.PI)*80;
                    Color accent=Actor(f.Cue.Player);accent.a=FadeOut(age,1.65f,2.4f)*.95f;
                    Color gold=FeastView.Golden;gold.a=accent.a;
                    if(age<1.4f)
                    {
                        Words(origin+new Vector2(0,-35-age*10),$"{f.Cue.Count}人前",24,accent);
                    }
                    Words(point,"+"+f.Cue.Points.ToString("N0"),31,accent);
                    for(int i=0;i<12;i++)
                    {
                        float a=i*Mathf.PI/6+age*2;
                        Sprite(origin+new Vector2(Mathf.Cos(a)*(70+age*85),Mathf.Sin(a)*(42+age*52)),6+4*(1-t),i%3==0?gold:accent,i%4==0?1:3,age*95+i*30);
                    }
                }
                for(int player=0;player<2;player++)
                {
                    float age=Time.unscaledTime-goldAt[player];if(age<0 || age>2.1f)continue;
                    float fade=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-1.5f)/.6f));
                    float pop=1+.24f*Mathf.Sin(Mathf.Clamp01(age/.4f)*Mathf.PI);
                    Vector2 center=new Vector2(player==0?350:930,235-Mathf.Min(age,.5f)*12);
                    Color gold=FeastView.Golden;gold.a=fade;
                    Color accent=Actor(player);accent.a=fade*.85f;
                    Fill(new Rect(center.x-187,center.y-65,374,150),new Color(.16f,.045f,.015f,fade*.8f));
                    Fill(new Rect(center.x-187,center.y-65,374,4),gold);
                    Fill(new Rect(center.x-187,center.y+81,374,4),accent);
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4+age*.6f;
                        Sprite(center+new Vector2(Mathf.Cos(a)*205,Mathf.Sin(a)*77),9+4*Mathf.Sin(age*5+i),gold,1,age*60+i*45);
                    }
                    Words(center+new Vector2(0,-26),"金素材ボーナス！",30,gold,pop);
                    Words(center+new Vector2(0,34),"+"+goldPoints[player].ToString("N0"),48,new Color(1,.97f,.7f,fade),pop);
                }
                foreach(var p in flecks)
                {
                    float age=Time.unscaledTime-p.Born,t=age/p.Life;Color c=p.Color;c.a*=1-t;
                    Sprite(p.Start+p.Velocity*age+Vector2.down*(25*age*age),p.Size,c,p.Kind,(p.Angle+p.Spin*age)*Mathf.Rad2Deg);
                }
                for(int p=0;p<2;p++)
                {
                    float leadAge=Time.unscaledTime-leadAt[p];
                    if(leadAge<1.25f)
                    {
                        float fade=1-Mathf.SmoothStep(.5f,1.25f,leadAge);Color accent=Actor(p);accent.a=fade*.75f;
                        float leadX=p==0?102:1178;
                        Sprite(new Vector2(leadX,130),15,accent,1,leadAge*90);
                        for(int i=0;i<3;i++)Sprite(new Vector2(leadX+(p==0?1:-1)*(35+leadAge*65+i*18),130-Mathf.Sin(leadAge*3+i)*9),4,accent,i%2,leadAge*120);
                    }
                    float age=Time.unscaledTime-submittedAt[p];
                    if(p==0 && age<.6f)
                    {
                        float width=600+age*90;Color c=Actor(p);c.a=(1-age/.6f)*.38f;
                        Fill(new Rect(640-width*.5f,696-age*18,width,2),c);
                    }
                    float typeAge=Time.unscaledTime-typedAt[p];
                    if(p==0 && typeAge<.25f){Color c=Actor(p);c.a=(1-typeAge/.25f)*.6f;Fill(new Rect(340,758,600,2),c);}
                }
                float intro=PhaseAge;
                if(intro<1.3f)
                {
                    float a=Mathf.Clamp01(intro/.16f)*(1-Mathf.SmoothStep(.6f,1.3f,intro));
                    Words(new Vector2(640,300-intro*24),"料理開始！",64,new Color(1,.94f,.76f,a),.85f+.15f*(1-Mathf.Exp(-intro*10)));
                    for(int p=0;p<2;p++){Color c=Actor(p);c.a=a*.8f;Fill(new Rect(p==0?380:640,344-intro*24,260,3),c);}
                }
            }
            if(state.Remaining>0 && state.Remaining<=10)
            {
                float beat=1-(Mathf.Ceil(state.Remaining)-state.Remaining);Color warm=FeastView.Orange;warm.a=.35f+beat*.4f;
                Fill(new Rect(585,122,110*state.Remaining/10f,3),warm);
                Color edge=new Color(1,.45f,.1f,beat*.055f);Fill(new Rect(0,0,1280,4),edge);Fill(new Rect(0,796,1280,4),edge);
                Sprite(new Vector2(640,94),58,new Color(1,.55f,.17f,beat*.08f),0);
            }
        }
        public void DishGlints(Rect label,int recipe)
        {
            if(Event.current.type!=EventType.Repaint)return;
            float glow=worldEffects.DiscoveryGlow(recipe);if(glow<=0)return;
            Color gold=new Color(1,.85f,.35f,glow*.8f);
            float wobble=Mathf.Sin(Time.unscaledTime*7)*4;
            Sprite(new Vector2(label.x-7,label.center.y+wobble),8*glow,gold,1,Time.unscaledTime*65);
            Sprite(new Vector2(label.xMax+7,label.center.y-wobble),8*glow,gold,1,-Time.unscaledTime*65);
        }
    }
}
