using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpinningFeast
{
    // Explicit opt-in experiment. Does not create entities, remove food, or select LOD.
    // Caller retains authoritative 3D picking and filters flying/gold/large food.
    public sealed class FeastFoodImpostors : IDisposable
    {
        const int Directions = 8, Cell = 64, Padding = 2, Columns = 16, Batch = 256;
        struct Item { public Matrix4x4 Matrix; public Vector4 UV; }
        readonly List<Item>[] items = {new List<Item>(),new List<Item>(),new List<Item>(),new List<Item>(),new List<Item>(),new List<Item>()};
        readonly Matrix4x4[] matrices = new Matrix4x4[Batch];
        readonly Vector4[] rects = new Vector4[Batch];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        static readonly int AtlasRect = Shader.PropertyToID("_AtlasRect");
        Mesh quad; Texture2D atlas; Material material; Camera camera;
        Material[] sourceMaterials; float[] radii; int atlasHeight; Quaternion bakedRotation;
        public bool Ready { get; private set; }
        public int AddedCount { get; private set; }
        public int DrawCalls { get; private set; }
        public string FailureReason { get; private set; }

        public bool Initialize(Mesh[] meshes, Material[] materials, Camera targetCamera)
        {
            Dispose(); FailureReason = null;
            if(targetCamera==null || meshes==null || meshes.Length<Cookbook.RawCount || materials==null || materials.Length<6 || !SystemInfo.supportsInstancing || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
            { FailureReason="Missing resources or instancing graphics device."; return false; }
            // Never share a layer with an existing renderer. No layers/settings are modified.
            var occupied = new bool[32];
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)) occupied[renderer.gameObject.layer]=true;
            int layer=-1; for(int i=31;i>=8;i--) if(!occupied[i]) {layer=i;break;}
            if(layer<0) {FailureReason="No unused render layer available for isolated atlas bake.";return false;}
            GameObject cameraObject=null,foodObject=null; RenderTexture target=null;
            Material bakeMaterial=null; Texture2D tile=null;
            var previous=RenderTexture.active;
            try
            {
                var shader=Shader.Find("SpinningFeast/FoodImpostor");
                if(shader==null || !shader.isSupported) throw new InvalidOperationException("Impostor shader unavailable.");
                camera=targetCamera; sourceMaterials=materials; bakedRotation=camera.transform.rotation;
                radii=new float[Cookbook.RawCount];
                atlasHeight=((Cookbook.RawCount*Directions+Columns-1)/Columns)*Cell;
                atlas=new Texture2D(Columns*Cell,atlasHeight,TextureFormat.RGBA32,false,false) {name="Food impostor atlas (opt-in)",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                atlas.SetPixels32(new Color32[atlas.width*atlas.height]);
                tile=new Texture2D(Cell,Cell,TextureFormat.RGBA32,false,false);
                target=new RenderTexture(Cell,Cell,24,RenderTextureFormat.ARGB32) {antiAliasing=1}; target.Create();
                cameraObject=new GameObject("Isolated food atlas camera") {hideFlags=HideFlags.HideAndDontSave};
                var bakeCamera=cameraObject.AddComponent<Camera>(); bakeCamera.enabled=false;
                bakeCamera.orthographic=true; bakeCamera.clearFlags=CameraClearFlags.SolidColor; bakeCamera.backgroundColor=Color.clear;
                bakeCamera.cullingMask=1<<layer; bakeCamera.nearClipPlane=.01f; bakeCamera.farClipPlane=100;
                bakeCamera.allowHDR=false; bakeCamera.allowMSAA=false; bakeCamera.targetTexture=target;
                Vector3 origin=new Vector3(1000,1000,1000);
                bakeCamera.transform.SetPositionAndRotation(origin-bakedRotation*Vector3.forward*40,bakedRotation);
                foodObject=new GameObject("Isolated food atlas source") {hideFlags=HideFlags.HideAndDontSave,layer=layer};
                foodObject.transform.position=origin;
                var filter=foodObject.AddComponent<MeshFilter>(); var renderer=foodObject.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
                bakeMaterial=new Material(materials[0]); bakeMaterial.SetColor("_Tint",Color.white); bakeMaterial.SetFloat("_Glow",0); bakeMaterial.SetFloat("_Gold",0);
                renderer.sharedMaterial=bakeMaterial;
                for(int kind=0;kind<Cookbook.RawCount;kind++)
                {
                    if(meshes[kind]==null) throw new InvalidOperationException("Missing raw food mesh.");
                    filter.sharedMesh=meshes[kind];
                    // Origin-centered radius deliberately preserves the authored pivot.
                    float radius=Mathf.Max(.01f,meshes[kind].bounds.center.magnitude+meshes[kind].bounds.extents.magnitude);
                    radii[kind]=radius*Cell/(Cell-Padding*2); bakeCamera.orthographicSize=radii[kind]; bakeCamera.aspect=1;
                    for(int yaw=0;yaw<Directions;yaw++)
                    {
                        foodObject.transform.rotation=Quaternion.Euler(0,yaw*360f/Directions,0);
                        bakeCamera.Render(); RenderTexture.active=target;
                        tile.ReadPixels(new Rect(0,0,Cell,Cell),0,0,false); tile.Apply(false,false);
                        int index=kind*Directions+yaw;
                        atlas.SetPixels((index%Columns)*Cell,(index/Columns)*Cell,Cell,Cell,tile.GetPixels());
                    }
                }
                atlas.Apply(false,true);
                quad=new Mesh {name="Food impostor quad"};
                quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
                quad.triangles=new[]{0,1,2,0,2,3}; quad.RecalculateBounds();
                material=new Material(shader) {enableInstancing=true}; material.SetTexture("_MainTex",atlas);
                Ready=true; return true;
            }
            catch(Exception exception) {FailureReason=exception.Message; Dispose(); Debug.LogWarning("Food impostor disabled: "+FailureReason); return false;}
            finally
            {
                RenderTexture.active=previous;
                if(cameraObject!=null) {cameraObject.GetComponent<Camera>().targetTexture=null; Destroy(cameraObject);}
                if(foodObject!=null) {foodObject.SetActive(false); Destroy(foodObject);}
                if(target!=null) {target.Release();Destroy(target);}
                Destroy(tile);Destroy(bakeMaterial);
            }
        }
        public void BeginFrame() {foreach(var list in items) list.Clear();AddedCount=DrawCalls=0;}
        public bool TryAdd(int kind,Matrix4x4 foodMatrix,int variant)
        {
            if(!Ready || kind<0 || kind>=radii.Length || variant<0 || variant>=5 || Quaternion.Angle(camera.transform.rotation,bakedRotation)>.1f) return false;
            Vector3 forward=foodMatrix.GetColumn(2);
            float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            int direction=((Mathf.RoundToInt(yaw*Directions/360f)%Directions)+Directions)%Directions;
            int index=kind*Directions+direction;
            float scale=Mathf.Max(((Vector3)foodMatrix.GetColumn(0)).magnitude,Mathf.Max(((Vector3)foodMatrix.GetColumn(1)).magnitude,forward.magnitude));
            var matrix=Matrix4x4.TRS(foodMatrix.MultiplyPoint3x4(Vector3.zero),bakedRotation,Vector3.one*(2*radii[kind]*scale));
            items[variant].Add(new Item {Matrix=matrix,UV=new Vector4(1f/Columns,(float)Cell/atlasHeight,(float)(index%Columns)/Columns,(float)((index/Columns)*Cell)/atlasHeight)});
            AddedCount++;return true;
        }
        public void Draw()
        {
            if(!Ready)return;
            for(int variant=0;variant<5;variant++)
            {
                var list=items[variant];
                properties.SetColor("_Tint",sourceMaterials[variant].GetColor("_Tint"));
                properties.SetFloat("_Glow",sourceMaterials[variant].GetFloat("_Glow"));
                for(int start=0;start<list.Count;start+=Batch)
                {
                    int count=Mathf.Min(Batch,list.Count-start);
                    for(int i=0;i<count;i++){matrices[i]=list[start+i].Matrix;rects[i]=list[start+i].UV;}
                    properties.SetVectorArray(AtlasRect,rects);
                    var rp=new RenderParams(material) {camera=camera,matProps=properties,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false};
                    Graphics.RenderMeshInstanced(rp,quad,0,matrices,count);DrawCalls++;
                }
            }
        }
        static void Destroy(UnityEngine.Object value) {if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public void Dispose() {Ready=false;BeginFrame();Destroy(quad);Destroy(atlas);Destroy(material);quad=null;atlas=null;material=null;camera=null;sourceMaterials=null;radii=null;}
    }
}
