using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpinningFeast
{
    public struct DishTag { public int Recipe, Player, Slot; public Vector3 Position; }

    public sealed class FeastView : MonoBehaviour
    {
        public static readonly Color Cyan = new Color(.2f,.94f,1);
        public static readonly Color Orange = new Color(1,.48f,.16f);
        const int Variants=6;
        bool mobileRendering;
        bool mobileBaseline;
        bool smallGeometry;
        readonly Mesh[] smallFoods=new Mesh[Cookbook.RawCount];
        readonly List<Matrix4x4>[] smallBatches=new List<Matrix4x4>[Cookbook.RawCount*Variants];
        readonly uint[] smallTriangles=new uint[Cookbook.RawCount];
        readonly int[] smallVertices=new int[Cookbook.RawCount];
        public int SmallGeometryCount {get;private set;}
        public bool UsesSmallGeometry=>smallGeometry;
        FeastFoodImpostors impostors;
        readonly Dictionary<float,bool> impostorLod=new Dictionary<float,bool>();
        int impostorEpoch=-1;
        public int ImpostorCount=>impostors!=null?impostors.AddedCount:0;
        public bool ImpostorsReady=>impostors!=null && impostors.Ready;
        readonly float[] foodRadii=new float[Cookbook.KindCount];
        readonly long[] foodVertexBytes=new long[Cookbook.KindCount],smallVertexBytes=new long[Cookbook.RawCount];
        public long SubmittedVertexInputBytes {get;private set;}
        readonly int[] foodVertices=new int[Cookbook.KindCount];
        public long SubmittedFoodVertices {get;private set;}
        public float ShadowQuadScreenCoverage {get;private set;}
        readonly uint[] foodTriangles=new uint[Cookbook.KindCount];
        public long SubmittedFoodTriangles {get;private set;}
        public int SubmittedDrawCalls {get;private set;}
        public int SubmittedShadowInstances {get;private set;}
        public long SubmittedMatrixBytes {get;private set;}
        public bool UsesMobileShader=>mobileRendering && !mobileBaseline;
        public bool UsesMobileGeometry=>mobileRendering;
        public static readonly Color Golden = new Color(1,.73f,.12f);
        readonly Mesh[] foods = new Mesh[Cookbook.KindCount];
        readonly List<Matrix4x4>[] batches = new List<Matrix4x4>[Cookbook.KindCount*Variants];
        readonly Matrix4x4[] chunk = new Matrix4x4[500];
        int BatchSize=>smallGeometry?500:256;
        readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        readonly List<GameObject> scenery = new List<GameObject>();
        readonly Material[] foodMaterials = new Material[Variants];
        readonly Material[] cookedMaterials = new Material[Variants];
        Material white, gold, jade, ghost, missingGhost, cyan, orange;
        readonly Dictionary<PrimitiveType,Mesh> furnitureMeshes=new Dictionary<PrimitiveType,Mesh>();
        Mesh ring, disc, servingPlate,shadowQuad;
        Material contactShadow;
        readonly List<Matrix4x4> shadows=new List<Matrix4x4>(4096);
        readonly Transform[] lanternBodies=new Transform[2];
        EntityQuery visualQuery, plateQuery;
        EntityManager em;
        FeastSystem simulation;
        World queryWorld;
        Transform turntable;
        Camera viewCamera;
        MaterialPropertyBlock ghostProps;
        readonly List<DishTag> dishTags=new List<DishTag>();
        public System.Collections.Generic.IReadOnlyList<DishTag> DishTags => dishTags;
        public int TagEpoch {get; private set;}
        public Camera ViewCamera => viewCamera;
        public FeastEffects Effects {get;set;}
        int previewRecipe=-1,previewPlayer,previewEpoch=-1;
        float nextPreviewAt;
        readonly int[] previewAmounts=new int[Cookbook.KindCount],previewRemaining=new int[Cookbook.KindCount];
        public int PreviewServings {get;private set;}
        public int HighlightedFoodCount {get;private set;}
        public void SetPreview(int recipe,int player)
        {
            if(recipe==previewRecipe && player==previewPlayer)return;
            previewRecipe=recipe;previewPlayer=player;nextPreviewAt=0;
            PreviewServings=0;Array.Clear(previewAmounts,0,previewAmounts.Length);
        }
        void RefreshPreview(MatchState state)
        {
            if(previewEpoch!=state.Epoch){previewEpoch=state.Epoch;nextPreviewAt=0;Array.Clear(previewAmounts,0,previewAmounts.Length);}
            if(previewRecipe>=0 && previewRecipe<Cookbook.All.Length && state.Phase==MatchPhase.Playing)
            {
                if(Time.unscaledTime>=nextPreviewAt)
                {
                    nextPreviewAt=Time.unscaledTime+.18f;
                    var plan=RecipeSolver.Solve(Cookbook.All[previewRecipe],simulation.Inventory());
                    Array.Copy(plan.Consume,previewAmounts,previewAmounts.Length);PreviewServings=plan.Servings;
                }
            }
            else {PreviewServings=0;Array.Clear(previewAmounts,0,previewAmounts.Length);}
            Array.Copy(previewAmounts,previewRemaining,previewAmounts.Length);HighlightedFoodCount=0;
            foodMaterials[5].SetFloat("_Glow",.24f+.1f*Mathf.Sin(Time.unscaledTime*4));
            float glow=.13f+.045f*Mathf.Sin(Time.unscaledTime*6);
            for(int i=3;i<5;i++){foodMaterials[i].SetFloat("_Glow",glow);cookedMaterials[i].SetFloat("_Glow",glow);}
        }
        public float LastCpuMilliseconds {get;private set;}
        public int VisibleFoodCount { get; private set; }
        public int TotalFoodCount { get; private set; }
        public bool HasHoveredIngredient {get; private set;}
        public FoodKind HoveredIngredient {get; private set;}
        public Vector3 HoveredIngredientPosition {get; private set;}
        public void Initialize(FeastSystem system)
        {
            simulation = system; em = system.EntityManager; queryWorld=system.World; ghostProps=new MaterialPropertyBlock();
            visualQuery=em.CreateEntityQuery(typeof(Visual)); plateQuery=em.CreateEntityQuery(typeof(Plate),typeof(Stock));
            mobileRendering=Application.isMobilePlatform || Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-mobile-rendering")>=0;
            mobileBaseline=Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-mobile-baseline")>=0;
            for(int i=0;i<foods.Length;i++) { foods[i]=FeastMeshes.Food((FoodKind)i,!mobileRendering); foodTriangles[i]=foods[i].GetIndexCount(0)/3; foodVertices[i]=foods[i].vertexCount;foodRadii[i]=foods[i].bounds.extents.magnitude;for(int stream=0;stream<foods[i].vertexBufferCount;stream++)foodVertexBytes[i]+=(long)foodVertices[i]*foods[i].GetVertexBufferStride(stream); resources.Add(foods[i]); }
            smallGeometry=!mobileBaseline && Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-full-geometry")<0
                && (mobileRendering || Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-small-geometry")>=0);
            for(int i=0;i<Cookbook.RawCount;i++)if(smallGeometry){smallFoods[i]=FeastMeshes.Food((FoodKind)i,false,true);smallTriangles[i]=smallFoods[i].GetIndexCount(0)/3;smallVertices[i]=smallFoods[i].vertexCount;for(int stream=0;stream<smallFoods[i].vertexBufferCount;stream++)smallVertexBytes[i]+=(long)smallVertices[i]*smallFoods[i].GetVertexBufferStride(stream);resources.Add(smallFoods[i]);}
            for(int i=0;i<smallBatches.Length;i++)smallBatches[i]=new List<Matrix4x4>();
            for(int i=0;i<batches.Length;i++) batches[i]=new List<Matrix4x4>(i/Variants<Cookbook.RawCount?128:8);
            foodMaterials[0]=Mat(Color.white); foodMaterials[1]=Mat(Color.Lerp(Color.white,Cyan,.4f),.45f); foodMaterials[2]=Mat(Color.Lerp(Color.white,Orange,.4f),.45f);
            foodMaterials[3]=Mat(Color.Lerp(Color.white,Cyan,.22f),.12f);foodMaterials[4]=Mat(Color.Lerp(Color.white,Orange,.22f),.12f);
            foodMaterials[5]=Mat(Golden,.18f);foodMaterials[5].SetFloat("_Gold",1);foodMaterials[5].SetFloat("_Specular",.85f);foodMaterials[5].SetFloat("_Gloss",.8f);
            for(int i=0;i<Variants;i++){cookedMaterials[i]=new Material(foodMaterials[i]);cookedMaterials[i].SetFloat("_Specular",.48f);cookedMaterials[i].SetFloat("_Gloss",.7f);resources.Add(cookedMaterials[i]);}
            if(UsesMobileShader)for(int i=0;i<5;i++)foodMaterials[i].EnableKeyword("FEAST_NO_SPECULAR");
            white=Mat(new Color(.96f,.94f,.83f));white.SetFloat("_Specular",.22f); gold=Mat(new Color(.88f,.64f,.28f)); jade=Mat(new Color(.08f,.32f,.29f)); cyan=Mat(Cyan,.25f); orange=Mat(Orange,.25f);
            ghost=new Material(Shader.Find("SpinningFeast/Ghost")) {enableInstancing=true}; resources.Add(ghost);
            missingGhost=new Material(ghost) {renderQueue=3100};missingGhost.SetFloat("_DepthTest",(float)CompareFunction.Always);missingGhost.SetFloat("_ColorMix",.7f);missingGhost.SetFloat("_Hologram",1);resources.Add(missingGhost);
            ring=FeastMeshes.Ring(1,.025f); disc=FeastMeshes.Disc(); resources.Add(ring);resources.Add(disc);
            servingPlate=FeastMeals.Plate();resources.Add(servingPlate);
            shadowQuad=new Mesh {name="Soft contact quad"};
            shadowQuad.vertices=new[]{new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(1,0,-1)};
            shadowQuad.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
            shadowQuad.triangles=new[]{0,1,2,0,2,3};shadowQuad.RecalculateBounds();resources.Add(shadowQuad);
            contactShadow=new Material(Shader.Find("SpinningFeast/ContactShadow")){enableInstancing=true};resources.Add(contactShadow);
            BuildRoom();
            if(!mobileBaseline && Array.IndexOf(Environment.GetCommandLineArgs(),"-feast-food-impostors")>=0)
            {impostors=new FeastFoodImpostors();impostors.Initialize(foods,foodMaterials,viewCamera);}
        }
        Material Mat(Color color,float glow=0)
        { var m=new Material(Shader.Find("SpinningFeast/Lit")) { enableInstancing=true }; m.SetColor("_Tint",color);m.SetFloat("_Glow",glow);resources.Add(m);return m; }
        GameObject Shape(string name,PrimitiveType primitive,Vector3 pos,Vector3 size,Material mat,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(parent==null?transform:parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
            var filter=go.GetComponent<MeshFilter>();
            if(!furnitureMeshes.TryGetValue(primitive,out var mesh)) {mesh=mobileRendering?Instantiate(filter.sharedMesh):FeastMeshes.DoubleTriangles(filter.sharedMesh);furnitureMeshes.Add(primitive,mesh);resources.Add(mesh);}
            filter.sharedMesh=mesh;
            Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;scenery.Add(go);return go;
        }
        GameObject Ring(string name,float radius,float y,Material mat,Transform parent=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent==null?transform:parent,false);go.transform.localPosition=new Vector3(0,y,0);go.transform.localScale=Vector3.one*radius;
            go.AddComponent<MeshFilter>().sharedMesh=ring;go.AddComponent<MeshRenderer>().sharedMaterial=mat;scenery.Add(go);return go;
        }
        void BuildRoom()
        {
            var cameraObject=new GameObject("Fixed banquet camera");cameraObject.transform.SetParent(transform);viewCamera=cameraObject.AddComponent<Camera>();
            viewCamera.transform.position=new Vector3(0,13.6f,-17.8f);viewCamera.transform.LookAt(new Vector3(0,.3f,0));viewCamera.orthographic=true;viewCamera.orthographicSize=ArenaGeometry.CameraHalfHeight;
            viewCamera.backgroundColor=new Color(.045f,.11f,.13f);viewCamera.clearFlags=CameraClearFlags.SolidColor;viewCamera.nearClipPlane=.1f;viewCamera.farClipPlane=90;
            cameraObject.AddComponent<AudioListener>();
            var floor=Mat(new Color(.055f,.15f,.17f)); var red=Mat(new Color(.51f,.115f,.075f)); var dark=Mat(new Color(.17f,.055f,.035f));
            Shape("Restaurant floor",PrimitiveType.Cube,new Vector3(0,-1.4f,0),new Vector3(90,.2f,90),floor);
            Shape("Table pedestal",PrimitiveType.Cylinder,new Vector3(0,-.8f,0),new Vector3(3*ArenaGeometry.TableScale,.65f,3*ArenaGeometry.TableScale),dark);
            Shape("Lacquer edge",PrimitiveType.Cylinder,new Vector3(0,-.23f,0),new Vector3(ArenaGeometry.TableRadius*2+.25f,.22f,ArenaGeometry.TableRadius*2+.25f),gold);
            Shape("Vermilion dining table",PrimitiveType.Cylinder,new Vector3(0,-.11f,0),new Vector3(ArenaGeometry.TableRadius*2,.14f,ArenaGeometry.TableRadius*2),red);
            Ring("Outer gold inlay",ArenaGeometry.TableRadius-.14f,.035f,gold);Ring("Inner gold inlay",ArenaGeometry.TurntableRadius+.25f,.04f,gold);
            var swivel=new GameObject("Rotating lazy Susan");swivel.transform.SetParent(transform);turntable=swivel.transform;
            Shape("Jade lazy Susan",PrimitiveType.Cylinder,new Vector3(0,.2f,0),new Vector3(ArenaGeometry.TurntableRadius*2,.15f,ArenaGeometry.TurntableRadius*2),jade,turntable);
            Ring("Turntable rim",ArenaGeometry.TurntableRadius-.06f,.357f,gold,turntable);Ring("Turntable inner inlay",ArenaGeometry.TurntableRadius*.3125f,.36f,gold,turntable);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                var tile=Shape("Radial inlay",PrimitiveType.Cube,new Vector3(Mathf.Sin(a)*.72f*ArenaGeometry.TableScale,.363f,Mathf.Cos(a)*.72f*ArenaGeometry.TableScale),new Vector3(.02f*ArenaGeometry.TableScale,.008f,.25f*ArenaGeometry.TableScale),gold,turntable);
                tile.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);
            }
            for(int p=0;p<2;p++)
            {
                var m=p==0?cyan:orange; float x=(p==0?-1:1)*(ArenaGeometry.TableRadius+.7f);
                lanternBodies[p]=Shape("Player lantern",PrimitiveType.Sphere,new Vector3(x,1.8f,1.8f),new Vector3(.65f,1,.65f),m).transform;
                Shape("Lantern cap",PrimitiveType.Cylinder,new Vector3(x,2.3f,1.8f),new Vector3(.4f,.06f,.4f),gold);
                Shape("Lantern tassel",PrimitiveType.Cylinder,new Vector3(x,1.1f,1.8f),new Vector3(.05f,.23f,.05f),gold);
            }
        }
        void Draw(Mesh mesh,Material material,Matrix4x4 matrix,MaterialPropertyBlock props=null)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var rp=new RenderParams(material) { camera=viewCamera, shadowCastingMode=ShadowCastingMode.Off, receiveShadows=false, matProps=props };
            Graphics.RenderMesh(rp,mesh,0,matrix);
            SubmittedDrawCalls++;SubmittedMatrixBytes+=64;
        }
        Matrix4x4 FoodTransform(Visual v)
        {
            var q=Quaternion.Euler(Mathf.Sin(v.Seed)*9,(v.Seed+v.OrbitAngle)*Mathf.Rad2Deg,Mathf.Cos(v.Seed)*12);
            if(v.Scatter>0)q*=Quaternion.Euler(Mathf.Sin(v.Life*19+v.Seed)*v.Scatter*38,0,Mathf.Cos(v.Life*17+v.Seed)*v.Scatter*32);
            Vector3 stretch=new Vector3(1+v.Impact*.07f,1-v.Impact*.12f,1+v.Impact*.07f);
            float hop=GoldenHop(v);
            if(hop>0 || (v.Golden!=0 && Mathf.Abs(v.Position.y-v.Floor)<.08f))
            {
                float landing=Mathf.Pow(1-hop,8);
                stretch=Vector3.Scale(stretch,new Vector3(1+.12f*landing-.07f*hop,1-.16f*landing+.15f*hop,1+.12f*landing-.07f*hop));
            }
            return Matrix4x4.TRS((Vector3)FoodMotion.GoldWorldPosition(v),q,stretch*v.Scale);
        }
        float GoldenHop(Visual v)
        {
            return v.Golden!=0 && v.Mode==0?Mathf.Clamp01((v.Position.y-v.Floor)/6f):0;
        }
        float FoodHitRadius(Visual v)
        {
            float extent=foodRadii[(int)v.Kind];
            return Mathf.Max(18,extent*1.25f*v.Scale*(1+v.Impact*.07f)*Screen.height/(viewCamera.orthographicSize*2));
        }
        void LateUpdate()
        {
            if(simulation==null || !em.Exists(simulation.MatchEntity)) return;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            SubmittedFoodTriangles=0;SubmittedFoodVertices=0;SubmittedVertexInputBytes=0;SmallGeometryCount=0;ShadowQuadScreenCoverage=0;SubmittedDrawCalls=0;SubmittedMatrixBytes=0;SubmittedShadowInstances=0;
            em.CompleteAllTrackedJobs(); MatchState state=simulation.State;
            if(impostorEpoch!=state.Epoch){impostorEpoch=state.Epoch;impostorLod.Clear();}
            if(impostors!=null)impostors.BeginFrame();
            int previousHover=HasHoveredIngredient?(int)HoveredIngredient:-1;
            RefreshPreview(state);
            turntable.localRotation=Quaternion.Euler(0,state.Angle*Mathf.Rad2Deg,0);
            // Preserve the fixed angle while widening the view as platters rise
            // into additional tiers. This also keeps the full table in frame.
            float halfHeight=Mathf.Max(ArenaGeometry.CameraHalfHeight,(ArenaGeometry.TableRadius+.6f)/viewCamera.aspect);
            Vector3 viewTarget=new Vector3(0,.3f,0),cameraUp=viewCamera.transform.up,cameraRight=viewCamera.transform.right;
            using(var plateEntities=plateQuery.ToEntityArray(Allocator.Temp))foreach(Entity plateEntity in plateEntities)
            {
                var plate=em.GetComponentData<Plate>(plateEntity);
                var stock=em.GetComponentData<Stock>(plateEntity);
                if(stock.Quantity+plate.Incoming<=0)continue;
                Vector3 plateCenter=FeastSystem.PlatePosition(plate.Slot,state.Angle);
                Vector3 offset=plateCenter+Vector3.up*.85f-viewTarget;
                halfHeight=Mathf.Max(halfHeight,Mathf.Abs(Vector3.Dot(offset,cameraUp))+1.5f);
                halfHeight=Mathf.Max(halfHeight,(Mathf.Abs(Vector3.Dot(offset,cameraRight))+1.5f)/viewCamera.aspect);
            }
            viewCamera.orthographicSize=halfHeight;
            for(int player=0;player<2;player++)
            {
                float pulse=Effects!=null?Effects.LanternPulse(player):0;
                lanternBodies[player].localScale=new Vector3(.65f*(1+pulse*.14f),1-pulse*.06f,.65f*(1+pulse*.14f));
                (player==0?cyan:orange).SetFloat("_Glow",.25f+pulse*.65f);
            }
            foreach(var b in batches) b.Clear();foreach(var b in smallBatches)b.Clear();shadows.Clear();
            dishTags.Clear();
            TagEpoch=state.Epoch;
            HasHoveredIngredient=false;float nearestHit=float.PositiveInfinity;
            Matrix4x4 hoveredMatrix=Matrix4x4.identity;
            // Mobile has no passive pointer: preserve all food, skip only idle hit testing.
            bool testHover=!mobileRendering || Input.touchCount>0;
            Vector3 pointer=Input.mousePosition;
            Ray hoverRay=testHover?viewCamera.ScreenPointToRay(pointer):default;
            using(var vs=visualQuery.ToComponentDataArray<Visual>(Allocator.Temp))
            {
                TotalFoodCount=vs.Length;VisibleFoodCount=0;
                for(int i=0;i<vs.Length;i++)
                {
                    var v=vs[i];
                    if(v.Mode==2) continue;
                    VisibleFoodCount++;
                    if(v.Mode==0 && v.Position.y<3.2f)
                    {
                        float size=v.Scale*(.45f+Mathf.Max(0,v.Position.y-v.Floor)*.08f)*(1-.38f*GoldenHop(v));
                        float shadowY=v.Floor<0?-1.295f:.035f;
                        float3 at=FoodMotion.GoldWorldPosition(v);
                        shadows.Add(Matrix4x4.TRS(new Vector3(at.x,shadowY+(v.Seed%1)*.0002f,at.z),Quaternion.identity,new Vector3(size,1,size)));
                    }
                    if((v.Mode==1 || v.Mode==4) && Effects!=null)Effects.Trace(v);
                    if(v.Mode==0 && Effects!=null){Effects.Landing(v);if(v.Golden!=0)Effects.GoldenSparkle(v);}
                    Matrix4x4 matrix;
                    if(v.Mode==0)matrix=FoodTransform(v);
                    else
                    {
                        float shrink=v.Mode==1?Mathf.Lerp(1,.3f,Mathf.Pow(v.Life/v.Duration,4)):v.Mode==4?Mathf.Lerp(1,.25f,Mathf.Pow(Mathf.Clamp01(v.Life/v.Duration),3)):1;
                        var q=Quaternion.Euler(v.Seed*30,v.Seed*60+(v.Mode==1?v.Life*360:0),v.Seed*17);
                        matrix=Matrix4x4.TRS(v.Position,q,Vector3.one*v.Scale*shrink);
                    }
                    if(testHover && v.Mode==0 && (int)v.Kind<Cookbook.RawCount)
                    {
                        Bounds hitBounds=foods[(int)v.Kind].bounds;
                        hitBounds.extents=hitBounds.extents*1.25f;
                        Vector3 screen=viewCamera.WorldToScreenPoint(matrix.MultiplyPoint3x4(Vector3.zero));
                        float radius=FoodHitRadius(v);
                        Vector2 delta=new Vector2(screen.x-pointer.x,screen.y-pointer.y);
                        if(screen.z>0 && delta.sqrMagnitude<radius*radius)
                        {
                            Matrix4x4 inverse=matrix.inverse;
                            var localRay=new Ray(inverse.MultiplyPoint3x4(hoverRay.origin),inverse.MultiplyVector(hoverRay.direction));
                            if(hitBounds.IntersectRay(localRay,out float distance))
                            {
                                float worldDistance=Vector3.Distance(hoverRay.origin,matrix.MultiplyPoint3x4(localRay.GetPoint(distance)));
                                if(worldDistance<nearestHit){nearestHit=worldDistance;HasHoveredIngredient=true;HoveredIngredient=v.Kind;HoveredIngredientPosition=matrix.MultiplyPoint3x4(Vector3.zero);hoveredMatrix=matrix;}
                            }
                        }
                    }
                    if(v.Mode==3)
                    {
                        // Camera-facing 3D space above each input: never buried under the falling food.
                        float t=Mathf.Clamp01(v.Life/v.Duration);
                        float pop=Mathf.Lerp(.45f,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.16f)));
                        float fade=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.6f)/.4f));
                        float x=(v.Player==0?.5f:.69f)+(v.EffectSlot-(v.EffectCount-1)*.5f)*.065f;
                        Vector3 effectPosition=viewCamera.ViewportToWorldPoint(new Vector3(x,.19f+.025f*Mathf.Sin(t*Mathf.PI*.5f),8));
                        float size=viewCamera.orthographicSize*.16f*pop*ArenaGeometry.FoodModelScale;
                        matrix=Matrix4x4.TRS(effectPosition,Quaternion.Euler(-10,Mathf.Sin(t*Mathf.PI)*22,0),Vector3.one*size);
                        Color c=Color.Lerp(Color.white,v.Player==0?Cyan:Orange,.65f);c.a=.85f*fade;
                        ghostProps.SetColor("_Tint",c);Draw(foods[(int)v.Kind],missingGhost,matrix,ghostProps);
                    }
                    else
                    {
                        int variant=v.Mode==1 || v.Mode==4?v.Player+1:0;
                        if(v.Mode==0 && previewRemaining[(int)v.Kind]>0)
                        {previewRemaining[(int)v.Kind]--;HighlightedFoodCount++;variant=previewPlayer+3;if(Effects!=null)Effects.Anticipation(v,previewPlayer);}
                        if(v.Golden!=0)variant=5;
                        bool billboard=false;
                        if(ImpostorsReady && v.Mode==0 && v.Golden==0 && (int)v.Kind<Cookbook.RawCount && (int)v.Kind!=previousHover
                            && Mathf.Abs(v.Position.y-v.Floor)<.08f && Mathf.Abs(v.Velocity)<.1f && v.Impact<.1f && v.Scatter<.05f)
                        {
                            bool was=impostorLod.TryGetValue(v.Seed,out var oldLod) && oldLod;
                            float diameter=2*foodRadii[(int)v.Kind]*v.Scale*Screen.height/(2*viewCamera.orthographicSize);
                            bool use=diameter<(was?64:48);impostorLod[v.Seed]=use;
                            if(use)billboard=impostors.TryAdd((int)v.Kind,matrix,variant);
                        }
                        if(!billboard)
                        {
                            float pixelDiameter=smallGeometry?2*foodRadii[(int)v.Kind]*v.Scale*Screen.height/(2*viewCamera.orthographicSize):0;
                            bool useSmall=smallGeometry && (int)v.Kind<Cookbook.RawCount && v.Golden==0 && (int)v.Kind!=previousHover && pixelDiameter<64;
                            if(useSmall){smallBatches[(int)v.Kind*Variants+variant].Add(matrix);SmallGeometryCount++;}
                            else batches[(int)v.Kind*Variants+variant].Add(matrix);
                        }
                    }
                }
            }
            if(HasHoveredIngredient)
            {
                ghostProps.SetColor("_Tint",new Color(1,1,.75f,.4f));
                Draw(foods[(int)HoveredIngredient],ghost,hoveredMatrix*Matrix4x4.Scale(Vector3.one*1.10f),ghostProps);
                if(Effects!=null)Effects.Hover(HoveredIngredientPosition);
            }
            using(var es=plateQuery.ToEntityArray(Allocator.Temp)) foreach(Entity e in es)
            {
                var p=em.GetComponentData<Plate>(e);var stock=em.GetComponentData<Stock>(e);int total=stock.Quantity;
                if(total+p.Incoming==0) continue;
                Vector3 position=FeastSystem.PlatePosition(p.Slot,state.Angle);
                float bounce=Effects!=null?Effects.DishBounce(p.Recipe):0;
                float reveal=Effects!=null?Effects.PlatterReveal(p.Recipe):1;
                position.y+=Mathf.Max(0,bounce)*.28f+(1-reveal)*.12f;
                if(total>0) dishTags.Add(new DishTag {Recipe=p.Recipe,Player=p.LastPlayer,Slot=p.Slot,Position=position+Vector3.up*.3f*ArenaGeometry.FoodModelScale*ArenaGeometry.TableScale});
                float radius=(.40f+.32f*(1-Mathf.Exp(-(total+p.Incoming)/25f)))*ArenaGeometry.FoodModelScale*ArenaGeometry.TableScale;
                radius*=(1-bounce*.35f)*reveal;
                float pulse=.018f*Mathf.Sin(Time.time*5+p.Recipe);
                if(total>0 && Effects!=null)
                {
                    float top=foods[(int)stock.Kind].bounds.max.y*radius*(.72f+Mathf.Log(1+total)*.17f);
                    Effects.Steam(p.Recipe,p.Slot,position+Vector3.up*Mathf.Max(.1f,top),radius);
                }
                shadows.Add(Matrix4x4.TRS(position+Vector3.down*.015f,Quaternion.identity,new Vector3(radius*1.12f,1,radius*1.12f)));
                bool previewPlate=previewAmounts[(int)stock.Kind]>0 && total>0;
                int dishVariant=previewPlate?previewPlayer+3:0;
                if(previewPlate)
                {
                    Color glow=previewPlayer==0?Cyan:Orange;glow.a=.4f+.1f*Mathf.Sin(Time.unscaledTime*6);ghostProps.SetColor("_Tint",glow);
                    Draw(ring,ghost,Matrix4x4.TRS(position+Vector3.up*.025f,Quaternion.identity,Vector3.one*(radius+.11f)),ghostProps);
                }
                bool composed=FeastMeals.IsComposed(stock.Kind);
                Draw(composed?servingPlate:disc,white,Matrix4x4.TRS(position,Quaternion.identity,new Vector3(radius,1,radius)));
                Draw(ring,gold,Matrix4x4.TRS(position+Vector3.up*.008f,Quaternion.identity,Vector3.one*radius));
                Draw(ring,p.LastPlayer==0?cyan:orange,Matrix4x4.TRS(position+Vector3.up*.012f,Quaternion.identity,Vector3.one*(radius+.045f+pulse)));
                // Counts stay exact in Stock; logarithmic height and representative pieces keep plating legible.
                int pieces=Mathf.Min(total,64);float height=(.06f+Mathf.Log(1+total)*.045f)*ArenaGeometry.FoodModelScale*ArenaGeometry.TableScale;
                if(composed && total>0)
                {
                    float foodRadius=radius*.88f;
                    float fullness=(.72f+Mathf.Log(1+total)*.17f)*(1+bounce);
                    batches[(int)stock.Kind*Variants+dishVariant].Add(Matrix4x4.TRS(position+Vector3.up*.03f,Quaternion.Euler(0,state.Angle*Mathf.Rad2Deg,0),new Vector3(foodRadius,foodRadius*fullness,foodRadius)));
                }
                for(int j=0;j<(composed?0:pieces);j++)
                {
                    float a=j*2.39996f+state.Angle;float r=Mathf.Sqrt((j%16)/16f)*radius*.67f;
                    Vector3 pos=position+new Vector3(Mathf.Sin(a)*r,.08f+(j/16)*height*.46f,Mathf.Cos(a)*r);
                    float size=((total<4?.40f:.30f)+Mathf.Min(total,180)*.00035f)*ArenaGeometry.FoodModelScale*ArenaGeometry.TableScale;
                    batches[(int)stock.Kind*Variants+dishVariant].Add(Matrix4x4.TRS(pos,Quaternion.Euler(0,a*Mathf.Rad2Deg,0),Vector3.one*size));
                }
                if(p.Incoming>0)
                {
                    Color c=p.LastPlayer==0?Cyan:Orange;c.a=.22f;ghostProps.SetColor("_Tint",c);
                    Draw(foods[(int)stock.Kind],ghost,Matrix4x4.TRS(position+Vector3.up*.35f*ArenaGeometry.TableScale,Quaternion.Euler(0,Time.time*80,0),Vector3.one*(.6f*reveal*ArenaGeometry.FoodModelScale*ArenaGeometry.TableScale)),ghostProps);
                }
            }
            dishTags.Sort((a,b)=>a.Slot.CompareTo(b.Slot));
            SubmittedShadowInstances=shadows.Count;
            // Geometric coverage proxy only: includes off-screen/occluded quads, not measured overdraw.
            float shadowPixelScale=Screen.height/(2*viewCamera.orthographicSize);
            float projectedPlane=Mathf.Abs(Vector3.Dot(viewCamera.transform.forward,Vector3.up));
            foreach(var shadow in shadows)ShadowQuadScreenCoverage+=4*Mathf.Abs(shadow.m00*shadow.m22)*projectedPlane*shadowPixelScale*shadowPixelScale/(Screen.width*(float)Screen.height);
            var shadowParams=new RenderParams(contactShadow){camera=viewCamera,worldBounds=new Bounds(Vector3.zero,Vector3.one*90),shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false};
            for(int offset=0;SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null && offset<shadows.Count;offset+=BatchSize)
            {
                int count=Mathf.Min(BatchSize,shadows.Count-offset);shadows.CopyTo(offset,chunk,0,count);
                Graphics.RenderMeshInstanced(shadowParams,shadowQuad,0,chunk,count);
                SubmittedDrawCalls++;SubmittedMatrixBytes+=64L*count;
            }
            for(int i=0;SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null && i<batches.Length;i++)
            {
                var list=batches[i];var rp=new RenderParams(i/Variants>=Cookbook.RawCount?cookedMaterials[i%Variants]:foodMaterials[i%Variants]) {camera=viewCamera,worldBounds=new Bounds(Vector3.zero,Vector3.one*90),shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false};
                for(int offset=0;offset<list.Count;offset+=BatchSize)
                {int count=Mathf.Min(BatchSize,list.Count-offset);list.CopyTo(offset,chunk,0,count);Graphics.RenderMeshInstanced(rp,foods[i/Variants],0,chunk,count);SubmittedFoodTriangles+=(long)foodTriangles[i/Variants]*count;SubmittedFoodVertices+=(long)foodVertices[i/Variants]*count;SubmittedVertexInputBytes+=foodVertexBytes[i/Variants]*count;SubmittedDrawCalls++;SubmittedMatrixBytes+=64L*count;}
            }
            if(smallGeometry)for(int i=0;SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null && i<smallBatches.Length;i++)
            {
                var list=smallBatches[i];var rp=new RenderParams(foodMaterials[i%Variants]){camera=viewCamera,worldBounds=new Bounds(Vector3.zero,Vector3.one*90),shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false};
                for(int offset=0;offset<list.Count;offset+=BatchSize)
                {int count=Mathf.Min(BatchSize,list.Count-offset);list.CopyTo(offset,chunk,0,count);Graphics.RenderMeshInstanced(rp,smallFoods[i/Variants],0,chunk,count);SubmittedFoodTriangles+=(long)smallTriangles[i/Variants]*count;SubmittedFoodVertices+=(long)smallVertices[i/Variants]*count;SubmittedVertexInputBytes+=smallVertexBytes[i/Variants]*count;SubmittedDrawCalls++;SubmittedMatrixBytes+=64L*count;}
            }
            if(impostors!=null)
            {impostors.Draw();SubmittedDrawCalls+=impostors.DrawCalls;SubmittedFoodTriangles+=2L*impostors.AddedCount;SubmittedFoodVertices+=4L*impostors.AddedCount;SubmittedVertexInputBytes+=80L*impostors.AddedCount;SubmittedMatrixBytes+=64L*impostors.AddedCount+256L*16*impostors.DrawCalls;}
            LastCpuMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000f/System.Diagnostics.Stopwatch.Frequency;
        }
        void OnDestroy()
        {
            if(queryWorld!=null && queryWorld.IsCreated) {visualQuery.Dispose();plateQuery.Dispose();}
            if(impostors!=null)impostors.Dispose();
            foreach(var r in resources) if(r) Destroy(r);
        }
    }
}
