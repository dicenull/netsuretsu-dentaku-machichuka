using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpinningFeast
{
    // Presentation only. Particle budgets never limit recipes, portions, plates or scoring.
    [DefaultExecutionOrder(100)]
    public sealed class FeastEffects : MonoBehaviour
    {
        public const int MaxParticles=1536,MaxRings=32,MaxBridges=12;
        struct Mote
        {
            public Vector3 Position,Velocity;
            public Color Color;
            public float Age,Life,Size,Angle,Spin,Gravity,Aspect;
            public int Kind;
        }
        struct Ripple {public int Slot;public Vector3 Position;public float Born,Life,Size;public Color Color;}
        struct Bridge {public int From,To,Player;public float Born;}
        readonly List<Mote> motes=new List<Mote>(MaxParticles);
        readonly List<Ripple> ripples=new List<Ripple>(MaxRings);
        readonly List<Bridge> bridges=new List<Bridge>(MaxBridges);
        readonly List<Vector3> positions=new List<Vector3>(MaxParticles*4);
        readonly List<Vector2> uv=new List<Vector2>(MaxParticles*4);
        readonly List<Color> colors=new List<Color>(MaxParticles*4);
        readonly List<int> indices=new List<int>(MaxParticles*6);
        readonly float[] servedAt=new float[Cookbook.All.Length];
        readonly float[] foundAt=new float[Cookbook.All.Length];
        readonly float[] platterAt=new float[Cookbook.All.Length],servedWeight=new float[Cookbook.All.Length];
        readonly List<CookCue> pendingArrivals=new List<CookCue>(64);
        readonly float[] actorAt={-100,-100};
        readonly float[] actorStrength={0,0};
        MatchPhase previousPhase;
        float resultAt=-100,nextConfetti;
        FeastSystem simulation;
        FeastView view;
        Mesh additiveMesh,alphaMesh,ring;
        Texture2D atlas;
        Material additive,alpha,ringMaterial;
        MaterialPropertyBlock props;
        System.Random random=new System.Random(7409);
        int epoch=-1,traceBudget,steamBudget,landingBudget,previewBudget,goldBudget;
        public Texture2D Atlas => atlas;
        public float LastCpuMilliseconds {get;private set;}
        public int ActiveParticles => motes.Count;
        public int PeakParticles {get;private set;}
        public int CookingEvents {get;private set;}
        public int ArrivalEvents {get;private set;}
        public int DiscoveryEvents {get;private set;}
        public int ChainEvents {get;private set;}
        public int EatingEvents {get;private set;}
        public void Initialize(FeastSystem sim,FeastView renderer)
        {
            simulation=sim;view=renderer;view.Effects=this;
            simulation.CookingCue+=OnCooking;simulation.ArrivalCue+=OnArrival;simulation.Eating+=OnEating;
            atlas=CreateAtlas();
            additive=new Material(Shader.Find("SpinningFeast/Particles"));additive.SetTexture("_MainTex",atlas);additive.SetFloat("_DstBlend",(float)BlendMode.One);
            alpha=new Material(additive);alpha.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            ringMaterial=new Material(Shader.Find("SpinningFeast/Ghost"));props=new MaterialPropertyBlock();
            additiveMesh=new Mesh {name="Feast sparks"};additiveMesh.MarkDynamic();
            alphaMesh=new Mesh {name="Feast steam and confetti"};alphaMesh.MarkDynamic();
            ring=FeastMeshes.Ring(1,.018f,64,false);
            ResetForMatch();
        }
        public void ResetForMatch()
        {
            epoch=simulation.State.Epoch;motes.Clear();ripples.Clear();bridges.Clear();pendingArrivals.Clear();
            for(int i=0;i<servedAt.Length;i++){servedAt[i]=-100;foundAt[i]=-100;platterAt[i]=-100;servedWeight[i]=0;}
            PeakParticles=CookingEvents=ArrivalEvents=DiscoveryEvents=ChainEvents=EatingEvents=0;
            actorAt[0]=actorAt[1]=-100;actorStrength[0]=actorStrength[1]=0;
            previousPhase=simulation.State.Phase;resultAt=-100;
            if(previousPhase==MatchPhase.Playing)
            {
                Ring(-1,new Vector3(0,.39f,0),FeastView.Cyan,4.1f,1.6f);
                Ring(-1,new Vector3(0,.40f,0),FeastView.Orange,3.3f,1.3f);
            }
        }
        float Random(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
        static Color Actor(int player)=>player==0?FeastView.Cyan:FeastView.Orange;
        public static int GradeForPortions(int portions)=>portions>=120?3:portions>=40?2:portions>=10?1:0;
        public static int BurstCount(int portions)=>Mathf.Clamp(16+Mathf.CeilToInt(Mathf.Log(1f+Mathf.Max(0,portions))*20),16,180);
        void Add(Mote mote)
        {
            if(motes.Count>=MaxParticles)return;
            motes.Add(mote);PeakParticles=Mathf.Max(PeakParticles,motes.Count);
        }
        public void Burst(Vector3 origin,Color color,int count,float force=1)
        {
            for(int i=0;i<count;i++)
            {
                float a=Random(0,Mathf.PI*2),speed=Random(.45f,2.4f)*force;
                Add(new Mote {Position=origin,Velocity=new Vector3(Mathf.Sin(a)*speed,Random(.5f,2.4f)*force,Mathf.Cos(a)*speed),Color=Color.Lerp(color,new Color(1,.94f,.68f),Random(0,.6f)),Life=Random(.38f,.85f),Size=Random(.035f,.095f),Gravity=-3,Kind=i%4==0?1:0,Angle=Random(0,6.28f),Spin=Random(-4,4)});
            }
        }
        void Ring(int slot,Vector3 position,Color color,float size,float life)
        {
            if(ripples.Count>=MaxRings)ripples.RemoveAt(0);
            ripples.Add(new Ripple {Slot=slot,Position=position,Color=color,Size=size,Life=life,Born=Time.time});
        }
        void Celebration(Vector3 position,Color actor,int grade)
        {
            if(grade==0)return;
            int count=grade==1?24:grade==2?64:120;
            float speed=1.5f+grade*.9f;
            for(int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count;
                Color color=i%3==0?FeastView.Golden:i%3==1?actor:new Color(1,.97f,.73f);
                Add(new Mote {Position=position+Vector3.up*(grade>=2?.6f:.3f),Velocity=new Vector3(Mathf.Sin(angle)*speed*Random(.7f,1.35f),Random(1.8f,4f+grade),Mathf.Cos(angle)*speed*Random(.7f,1.35f)),Color=color,Life=Random(.95f,1.4f+grade*.25f),Size=Random(.07f,.13f+grade*.02f),Kind=i%5==0?1:3,Spin=Random(-9,9),Angle=angle,Gravity=-2.8f});
            }
        }
        void OnCooking(CookCue cue)
        {
            if(epoch!=simulation.State.Epoch)ResetForMatch();
            CookingEvents++;if(cue.NewPlatter){platterAt[cue.Recipe]=Time.time;servedAt[cue.Recipe]=-100;}
            int grade=GradeForPortions(cue.Count);
            actorAt[cue.Player]=Time.time;actorStrength[cue.Player]=.45f+grade*.28f;
            Vector3 position=FeastSystem.PlatePosition(cue.Slot,simulation.State.Angle);
            Ring(cue.Slot,position,Actor(cue.Player),1.4f+grade*.7f,.75f+grade*.12f);
            for(int i=0;i<grade;i++)Ring(cue.Slot,position,i%2==0?FeastView.Golden:Actor(cue.Player),2.2f+grade*.8f+i*.55f,.75f+i*.16f);
            Burst(position+Vector3.up*.22f,Actor(cue.Player),BurstCount(cue.Count)/2,.8f+grade*.22f);
            if(grade>=2)Burst(position+Vector3.up*(1+grade*.4f),FeastView.Golden,24*grade,1.2f+grade*.2f);
            if(cue.BonusPoints>0)
            {
                Ring(cue.Slot,position,FeastView.Golden,2.7f,1.25f);
                Ring(cue.Slot,position,new Color(1,.95f,.55f),1.8f,.7f);
                Burst(position+Vector3.up*.4f,FeastView.Golden,64,1.5f);
            }
            if(cue.Chain && cue.SourceSlot>=0)
            {
                ChainEvents++;
                if(bridges.Count>=MaxBridges)bridges.RemoveAt(0);
                bridges.Add(new Bridge {From=cue.SourceSlot,To=cue.Slot,Player=cue.Player,Born=Time.time});
            }
        }
        void OnArrival(CookCue cue)
        {
            // ECS callbacks may occur before MonoBehaviour Update; defer visual emission until Update.
            if(pendingArrivals.Count>=64)pendingArrivals.RemoveAt(0);
            pendingArrivals.Add(cue);
        }
        void OnEating(EatingCue cue)
        {
            if(epoch!=simulation.State.Epoch)ResetForMatch();
            EatingEvents++;
            actorAt[cue.Player]=Time.time;actorStrength[cue.Player]=1.2f;
            Vector3 position=FeastSystem.PlatePosition(cue.Slot,simulation.State.Angle);
            Color actor=Actor(cue.Player);
            Vector3 origin=position+Vector3.up*.55f;
            // A quick plate-local cracker replaces the old fullscreen frame. The motes
            // use the shared capped batch, with no new renderers or textures.
            int strips=Mathf.Clamp(24+cue.Count/2,24,48);
            float offset=Random(0,Mathf.PI*2);
            for(int i=0;i<strips;i++)
            {
                float angle=offset+i*Mathf.PI*2/strips;
                float speed=Random(2.8f,7.4f);
                Add(new Mote {
                    Position=origin,
                    Velocity=new Vector3(Mathf.Sin(angle)*speed,Random(3.6f,8.2f),Mathf.Cos(angle)*speed),
                    Color=i%3==0?FeastView.Golden:actor,
                    Life=Random(.55f,1.1f),Size=Random(.12f,.26f),Kind=3,Aspect=Random(1.5f,2.7f),
                    Angle=angle,Spin=Random(-13f,13f),Gravity=-10f
                });
            }
        }
        public float LanternPulse(int player)
        {
            float t=Time.time-actorAt[player];
            return t<1.5f?actorStrength[player]*Mathf.Exp(-t*2.7f)*(.7f+.3f*Mathf.Cos(t*12)):0;
        }
        public void Anticipation(Visual food,int player)
        {
            if(previewBudget<=0 || ((int)(food.Seed*1000)+Time.frameCount)%47!=0)return;
            previewBudget--;Color color=Actor(player);color.a=.45f;
            Add(new Mote {Position=(Vector3)food.Position+Vector3.up*.25f,Velocity=Vector3.up*.22f,Color=color,Life=.35f,Size=.038f,Kind=player==0?0:1,Spin=1});
        }
        public void GoldenSparkle(Visual food)
        {
            if(goldBudget<=0 || ((int)(food.Seed*1000)+Time.frameCount)%19!=0)return;
            goldBudget--;
            float a=Time.time*2+food.Seed;
            Vector3 at=(Vector3)FoodMotion.GoldWorldPosition(food);
            Add(new Mote {Position=at+new Vector3(Mathf.Cos(a)*food.Scale*.55f,food.Scale*.75f,Mathf.Sin(a)*food.Scale*.55f),Velocity=Vector3.up*.3f,Color=FeastView.Golden,Life=.65f,Size=.14f,Kind=1,Spin=2});
            Add(new Mote {Position=at,Color=new Color(1,.65f,.08f,.18f),Life=.4f,Size=food.Scale*.95f,Kind=0});
        }
        public void Landing(Visual food)
        {
            if(landingBudget<=0 || food.Impact<.25f || food.Life-food.LandedAt>Time.deltaTime*.5f || (int)(food.Seed*1000)%9!=0)return;
            landingBudget--;
            for(int i=0;i<3;i++)
            {
                float angle=i*Mathf.PI*2/3+food.Seed;
                Add(new Mote {Position=(Vector3)food.Position+Vector3.up*.025f,Velocity=new Vector3(Mathf.Sin(angle)*.28f,.1f,Mathf.Cos(angle)*.28f),Color=new Color(1,.87f,.56f,.28f),Life=.28f,Size=.055f,Kind=2,Spin=1});
            }
        }
        void LanternGlow(int player)
        {
            if(Time.frameCount%6!=player*3)return;
            float pulse=LanternPulse(player);Vector3 at=new Vector3((player==0?-1:1)*(ArenaGeometry.TableRadius+.7f),1.8f,1.8f);
            Color color=Actor(player);color.a=.10f+pulse*.13f;
            Add(new Mote {Position=at,Color=color,Life=.18f,Size=.72f+pulse*.12f,Kind=0});
            if(pulse>.2f)Add(new Mote {Position=at+new Vector3(Random(-.14f,.14f),.5f,0),Velocity=Vector3.up*.55f,Color=Actor(player),Life=.45f,Size=.035f,Kind=1,Spin=2});
        }
        public void Hover(Vector3 position)
        {
            if(Time.frameCount%7!=0)return;
            float a=Time.time*3.5f;
            Add(new Mote {Position=position+new Vector3(Mathf.Cos(a)*.38f,.38f,Mathf.Sin(a)*.38f),Velocity=Vector3.up*.08f,Color=new Color(1,.88f,.43f,.7f),Life=.27f,Size=.038f,Kind=1,Spin=1});
        }
        void ConfettiJet(int player)
        {
            Vector3 origin=new Vector3((player==0?-1:1)*ArenaGeometry.TableRadius,1.2f,1.8f);
            for(int i=0;i<6;i++)
            {
                Color color=i%3==0?new Color(1,.85f,.39f):Actor(player);
                Add(new Mote {Position=origin,Velocity=new Vector3((player==0?1:-1)*Random(.7f,2.5f),Random(2.6f,4.8f),Random(-1.6f,.6f)),Color=color,Life=Random(1.6f,2.4f),Size=Random(.045f,.08f),Kind=i%4==0?1:3,Spin=Random(-8,8),Angle=Random(0,6.28f),Gravity=-3.5f});
            }
        }
        public float PlatterReveal(int recipe)
        {float t=Mathf.Clamp01((Time.time-platterAt[recipe])/.28f);return .25f+.75f*(1-Mathf.Pow(1-t,3));}
        public float DishBounce(int recipe)
        {
            float t=Time.time-servedAt[recipe];
            return t<.8f?Mathf.Sin(t*14)*Mathf.Exp(-t*6)*Mathf.Lerp(.1f,.23f,servedWeight[recipe]):0;
        }
        public float DiscoveryGlow(int recipe)=>Mathf.Clamp01(1-(Time.time-foundAt[recipe])/2.1f);
        public void Trace(Visual food)
        {
            if(traceBudget<=0 || food.Life<.04f || food.Life>food.Duration-.06f)return;
            if(((int)(food.Seed*1000)+Time.frameCount)%(food.Golden!=0?3:7)!=0)return;
            traceBudget--;
            Vector3 delta=(Vector3)(food.Position-food.Start);
            float angle=Mathf.Atan2(Vector3.Dot(delta,view.ViewCamera.transform.up),Vector3.Dot(delta,view.ViewCamera.transform.right));
            Add(new Mote {Position=food.Position,Velocity=Vector3.up*.12f,Color=food.Golden!=0?FeastView.Golden:Actor(food.Player),Life=food.Golden!=0?.48f:.24f,Size=food.Golden!=0?.11f:.027f+food.Scale*.06f,Kind=0,Angle=angle,Aspect=food.Golden!=0?3.5f:2.4f});
        }
        public void Steam(int recipe,int slot,Vector3 position,float radius)
        {
            if(steamBudget<=0 || (Time.frameCount+slot*7)%23!=0)return;
            MealShape shape=Cookbook.All[recipe].Shape;
            if(shape==MealShape.Salad || shape==MealShape.Dessert || shape==MealShape.Cream || shape==MealShape.Sandwich)return;
            steamBudget--;
            float a=Random(0,6.28f),r=Random(.1f,radius*.5f);
            bool soup=shape==MealShape.Soup;
            Color color=new Color(.94f,.95f,.87f,soup?.16f:.13f);
            if(soup && Time.frameCount%2==0)
            {
                Add(new Mote {Position=position+new Vector3(Mathf.Sin(a)*r,.03f,Mathf.Cos(a)*r),Velocity=Vector3.up*.14f,Color=new Color(1,.85f,.56f,.4f),Life=.35f,Size=.035f,Kind=0});
            }
            Add(new Mote {Position=position+new Vector3(Mathf.Sin(a)*r,.25f,Mathf.Cos(a)*r),Velocity=new Vector3(Random(-.10f,.10f),Random(.4f,.65f),Random(-.1f,.1f)),Color=color,Life=Random(1.1f,1.7f),Size=Random(.11f,.17f),Kind=2,Angle=Random(0,6.28f),Spin=Random(-.4f,.4f)});
        }
        void Update()
        {
            if(simulation==null)return;
            if(epoch!=simulation.State.Epoch)ResetForMatch();
            traceBudget=36;steamBudget=5;landingBudget=3;previewBudget=4;goldBudget=8;
            var state=simulation.State;
            if(state.Phase!=previousPhase)
            {
                previousPhase=state.Phase;
                if(state.Phase==MatchPhase.Result){resultAt=Time.time;nextConfetti=Time.time;}
            }
            if(state.Phase==MatchPhase.Result && Time.time-resultAt<2.2f && Time.time>=nextConfetti)
            {
                nextConfetti=Time.time+.12f;ConfettiJet(0);ConfettiJet(1);
            }
            foreach(var cue in pendingArrivals)
            {
                int grade=GradeForPortions(cue.Count);
                ArrivalEvents++;servedAt[cue.Recipe]=Time.time;servedWeight[cue.Recipe]=Mathf.Clamp01(Mathf.Log(1f+cue.Count)/5);actorAt[cue.Player]=Time.time;actorStrength[cue.Player]=.5f+grade*.25f;
                Vector3 position=FeastSystem.PlatePosition(cue.Slot,simulation.State.Angle);
                Burst(position+Vector3.up*.28f,Actor(cue.Player),BurstCount(cue.Count),1+grade*.24f);
                Celebration(position,Actor(cue.Player),grade);
                Ring(cue.Slot,position,Actor(cue.Player),1.8f+grade*.75f,.7f+grade*.15f);
                if(grade>=2)
                {
                    Ring(cue.Slot,position,FeastView.Golden,4f+grade,1.4f);
                    Ring(cue.Slot,position,new Color(1,.98f,.7f),5f+grade,1.8f);
                }
                if(grade==3)
                {
                    for(int i=0;i<4;i++)
                    {
                        float a=i*Mathf.PI*.5f;
                        Burst(new Vector3(Mathf.Sin(a)*5f,.7f,Mathf.Cos(a)*5f),i%2==0?Actor(cue.Player):FeastView.Golden,44,1.8f);
                    }
                }
                if(cue.BonusPoints>0)
                {
                    Ring(cue.Slot,position,FeastView.Golden,3.4f,1.45f);
                    Ring(cue.Slot,position,new Color(1,.98f,.68f),2.3f,.85f);
                    for(int i=0;i<48;i++)
                    {
                        float angle=i*Mathf.PI*2/24,speed=i<24?2.2f:1.3f;
                        Add(new Mote {Position=position+Vector3.up*.5f,Velocity=new Vector3(Mathf.Sin(angle)*speed,Random(2.4f,4.4f),Mathf.Cos(angle)*speed),Color=i%3==0?new Color(1,.96f,.65f):FeastView.Golden,Life=Random(1,1.7f),Size=i%3==0?.15f:.09f,Kind=i%3==0?1:3,Spin=Random(-6,6),Angle=angle,Gravity=-3});
                    }
                }
                if(cue.Count>=40)
                {
                    int petals=Mathf.Min(24,8+cue.Count/8);
                    for(int i=0;i<petals;i++)
                    {
                        float angle=i*Mathf.PI*2/petals;
                        Add(new Mote {Position=position+Vector3.up*.3f,Velocity=new Vector3(Mathf.Sin(angle)*1.2f,1.6f,Mathf.Cos(angle)*1.2f),Color=i%2==0?Actor(cue.Player):new Color(1,.87f,.48f),Life=1.05f,Size=.055f,Kind=3,Spin=Random(-6,6),Angle=angle,Gravity=-2});
                    }
                }
                if(cue.Discovery)
                {
                    DiscoveryEvents++;foundAt[cue.Recipe]=Time.time;
                    Ring(cue.Slot,position,new Color(1,.80f,.32f),2.25f,1.4f);
                    for(int i=0;i<12;i++)
                    {
                        float a=i*Mathf.PI/6;
                        Add(new Mote {Position=position+new Vector3(Mathf.Sin(a)*.3f,.4f,Mathf.Cos(a)*.3f),Velocity=new Vector3(Mathf.Sin(a)*.65f,Random(.7f,1.2f),Mathf.Cos(a)*.65f),Color=new Color(1,.85f,.39f),Size=.11f,Life=1.25f,Kind=1,Spin=Random(-2,2),Gravity=-.25f});
                    }
                }
            }
            pendingArrivals.Clear();
            float dt=Mathf.Min(Time.deltaTime,.05f);
            for(int i=motes.Count-1;i>=0;i--)
            {
                var p=motes[i];p.Age+=Time.deltaTime;
                if(p.Age>=p.Life){motes[i]=motes[motes.Count-1];motes.RemoveAt(motes.Count-1);continue;}
                p.Velocity.y+=p.Gravity*dt;p.Position+=p.Velocity*dt;p.Angle+=p.Spin*dt;motes[i]=p;
            }
            for(int i=ripples.Count-1;i>=0;i--)if(Time.time-ripples[i].Born>=ripples[i].Life)ripples.RemoveAt(i);
            for(int i=bridges.Count-1;i>=0;i--)if(Time.time-bridges[i].Born>=.9f)bridges.RemoveAt(i);
        }
        void LateUpdate()
        {
            if(view==null || view.ViewCamera==null)return;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            var state=simulation.State;LanternGlow(0);LanternGlow(1);
            foreach(var link in bridges)
            {
                float progress=Mathf.Clamp01((Time.time-link.Born)/.85f);
                Vector3 from=FeastSystem.PlatePosition(link.From,state.Angle),to=FeastSystem.PlatePosition(link.To,state.Angle);
                for(int i=0;i<3;i++)
                {
                    float t=Mathf.Clamp01(progress-i*.04f);Vector3 p=Vector3.Lerp(from,to,t)+Vector3.up*(.25f+Mathf.Sin(t*Mathf.PI)*1.1f);
                    Add(new Mote {Position=p,Velocity=Vector3.up*.08f,Color=Color.Lerp(Actor(link.Player),new Color(1,.84f,.38f),.4f),Life=.18f,Size=.055f,Kind=0});
                }
            }
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            foreach(var pulse in ripples)
            {
                float t=Mathf.Clamp01((Time.time-pulse.Born)/pulse.Life),radius=Mathf.Lerp(.25f,pulse.Size,1-Mathf.Pow(1-t,3));
                Vector3 p=pulse.Slot<0?pulse.Position:(Vector3)FeastSystem.PlatePosition(pulse.Slot,state.Angle);
                var color=pulse.Color;color.a=.65f*(1-t)*(1-t);props.SetColor("_Tint",color);
                Graphics.RenderMesh(new RenderParams(ringMaterial){camera=view.ViewCamera,matProps=props,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false},ring,0,Matrix4x4.TRS(p+Vector3.up*.04f,Quaternion.identity,Vector3.one*radius));
            }
            DrawParticles(false);DrawParticles(true);
            LastCpuMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000f/System.Diagnostics.Stopwatch.Frequency;
        }
        void DrawParticles(bool translucent)
        {
            positions.Clear();uv.Clear();colors.Clear();indices.Clear();
            Vector3 right=view.ViewCamera.transform.right,up=view.ViewCamera.transform.up;
            foreach(var p in motes)
            {
                if((p.Kind>=2)!=translucent)continue;
                float t=Mathf.Clamp01(p.Age/p.Life),fade=p.Kind==2?Mathf.Sin(t*Mathf.PI):Mathf.Pow(1-t,1.4f);
                float size=p.Size*(p.Kind==2?1+t*1.8f:1+t*.3f);
                float cs=Mathf.Cos(p.Angle),sn=Mathf.Sin(p.Angle);
                Vector3 x=(right*cs+up*sn)*size*Mathf.Max(1,p.Aspect),y=(-right*sn+up*cs)*size;
                if(p.Kind==3)x*=.4f+.6f*Mathf.Abs(Mathf.Cos(p.Age*7));
                int n=positions.Count;positions.Add(p.Position-x-y);positions.Add(p.Position+x-y);positions.Add(p.Position+x+y);positions.Add(p.Position-x+y);
                float u=p.Kind*.25f;uv.Add(new Vector2(u,0));uv.Add(new Vector2(u+.25f,0));uv.Add(new Vector2(u+.25f,1));uv.Add(new Vector2(u,1));
                Color color=p.Color;color.a*=fade;for(int j=0;j<4;j++)colors.Add(color);
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);
            }
            if(positions.Count==0)return;
            Mesh mesh=translucent?alphaMesh:additiveMesh;mesh.Clear(false);mesh.SetVertices(positions);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(indices,0);
            Graphics.RenderMesh(new RenderParams(translucent?alpha:additive){camera=view.ViewCamera,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false},mesh,0,Matrix4x4.identity);
        }
        static Texture2D CreateAtlas()
        {
            const int side=64;var texture=new Texture2D(side*4,side,TextureFormat.RGBA32,false){name="Feast procedural particle atlas",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[side*side*4];
            for(int kind=0;kind<4;kind++)for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                float u=(x+.5f)/side*2-1,v=(y+.5f)/side*2-1,r=Mathf.Sqrt(u*u+v*v),a;
                if(kind==1)a=Mathf.Pow(Mathf.Clamp01(1-r),2)+Mathf.Pow(Mathf.Clamp01(1-Mathf.Min(Mathf.Abs(u),Mathf.Abs(v))*10),3)*Mathf.Clamp01(1-r);
                else if(kind==2)a=Mathf.Pow(Mathf.Clamp01(1-r),1.8f)*(.55f+.45f*Mathf.PerlinNoise(u*3+6,v*3+4));
                else if(kind==3)a=Mathf.Clamp01((1-Mathf.Abs(u)-Mathf.Abs(v))*8);
                else a=Mathf.Pow(Mathf.Clamp01(1-r),2.5f);
                pixels[y*side*4+kind*side+x]=new Color(1,1,1,Mathf.Clamp01(a));
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        void OnDestroy()
        {
            if(simulation!=null){simulation.CookingCue-=OnCooking;simulation.ArrivalCue-=OnArrival;simulation.Eating-=OnEating;}
            foreach(var item in new Object[]{atlas,additive,alpha,ringMaterial,additiveMesh,alphaMesh,ring})if(item)Destroy(item);
        }
    }
}
