using System.Collections.Generic;
using UnityEngine;

namespace SpinningFeast
{
    public static class FeastMeshes
    {
        static readonly Color Cream = new Color(1,.91f,.69f);
        static readonly Color Green = new Color(.23f,.62f,.21f);
        public static Mesh Food(FoodKind kind, bool detailed = true)
        {
            if(kind>=FoodKind.Beef && (int)kind<Cookbook.RawCount)return FeastExpansionMeshes.Ingredient(kind,detailed);
            if(FeastMeals.IsComposed(kind))return FeastMeals.Build(kind,detailed);
            var b = new Builder(detailed);
            switch (kind)
            {
                case FoodKind.Rice:
                    // A small wooden scoop filled with recognisable elongated rice grains.
                    b.Profile(new Vector3(0,-.1f,0),new[]{.35f,.55f,.60f},new[]{-.22f,-.04f,.12f},new Color(.54f,.29f,.12f));
                    b.Ball(new Vector3(0,.12f,0),new Vector3(.53f,.25f,.48f),Cream);
                    for(int i=0;i<18;i++) {float a=i*2.4f,r=Mathf.Sqrt((i%9)/9f)*.43f; b.Ball(new Vector3(Mathf.Sin(a)*r,.27f+(i%3)*.04f,Mathf.Cos(a)*r),new Vector3(.065f,.045f,.15f),i%2==0?Color.white:Cream,6,3);}
                    b.Rod(new Vector3(.44f,-.05f,0),new Vector3(.85f,.03f,0),.10f,.07f,new Color(.65f,.38f,.17f));break;
                case FoodKind.Egg:
                    b.Profile(new Vector3(-.16f,0,0),new[]{0f,.29f,.39f,.35f,.22f,0f},new[]{-.52f,-.4f,-.13f,.2f,.48f,.61f},new Color(1,.88f,.67f));
                    b.Ball(new Vector3(.36f,-.2f,-.16f),new Vector3(.35f,.17f,.43f),Color.white);
                    b.Ball(new Vector3(.36f,-.03f,-.16f),new Vector3(.19f,.08f,.23f),new Color(1,.65f,.04f));break;
                case FoodKind.Pork:
                    b.Ball(Vector3.zero,new Vector3(.68f,.20f,.46f),new Color(1,.82f,.72f));
                    b.Ball(new Vector3(0,.09f,0),new Vector3(.56f,.15f,.36f),new Color(.88f,.28f,.33f));
                    for(int i=0;i<4;i++)b.Rod(new Vector3(-.38f+i*.22f,.225f,-.24f),new Vector3(-.26f+i*.18f,.235f,.24f),.025f,.018f,new Color(1,.70f,.67f),4);
                    b.Rod(new Vector3(-.39f,.24f,.05f),new Vector3(.36f,.24f,-.04f),.022f,.02f,new Color(1,.78f,.71f),4);break;
                case FoodKind.Chicken:
                    b.Ball(new Vector3(-.18f,0,0),new Vector3(.55f,.35f,.39f),new Color(.89f,.52f,.20f));
                    b.Ball(new Vector3(.15f,0,0),new Vector3(.35f,.23f,.24f),new Color(.95f,.66f,.29f));
                    b.Rod(new Vector3(.30f,0,0),new Vector3(.76f,.04f,0),.105f,.09f,Cream);
                    b.Ball(new Vector3(.78f,.05f,.09f),new Vector3(.13f,.14f,.10f),Color.white);
                    b.Ball(new Vector3(.78f,.05f,-.09f),new Vector3(.13f,.14f,.10f),Color.white);
                    for(int i=0;i<5;i++)b.Ball(new Vector3(-.37f+i*.12f,.32f,-.07f+(i%2)*.12f),Vector3.one*.035f,new Color(.62f,.30f,.10f),4,3);break;
                case FoodKind.Carrot:
                    b.Profile(Vector3.zero,new[]{.015f,.12f,.23f,.31f,.26f},new[]{-.75f,-.48f,-.12f,.25f,.48f},new Color(1,.39f,.035f),10);
                    for(int i=0;i<3;i++)b.Rod(new Vector3(-.14f+i*.02f,-.22f+i*.22f,-.17f-i*.025f),new Vector3(.12f,-.20f+i*.22f,-.17f-i*.025f),.018f,.012f,new Color(.76f,.20f,.025f),4);
                    b.Leaf(new Vector3(0,.42f,0),new Vector3(-.42f,1.0f,.03f),.16f,Green);
                    b.Leaf(new Vector3(0,.43f,0),new Vector3(.33f,.99f,.08f),.15f,new Color(.36f,.76f,.15f));
                    b.Leaf(new Vector3(0,.44f,0),new Vector3(.03f,1.12f,-.17f),.13f,Green);break;
                case FoodKind.Onion:
                    b.Profile(Vector3.zero,new[]{.06f,.32f,.48f,.44f,.23f,.07f},new[]{-.43f,-.29f,-.02f,.24f,.45f,.64f},new Color(.78f,.40f,.57f),12);
                    for(int i=0;i<8;i++) {float a=i*Mathf.PI/4; b.Rod(new Vector3(Mathf.Sin(a)*.43f,-.12f,Mathf.Cos(a)*.43f),new Vector3(Mathf.Sin(a)*.24f,.4f,Mathf.Cos(a)*.24f),.018f,.012f,new Color(.96f,.69f,.77f),4);}
                    b.Rod(new Vector3(0,.48f,0),new Vector3(.12f,.81f,0),.07f,.025f,Green);
                    for(int i=0;i<3;i++)b.Rod(new Vector3(0,-.37f,0),new Vector3((i-1)*.11f,-.59f,.05f),.018f,.005f,new Color(.65f,.43f,.20f),4);break;
                case FoodKind.Tomato:
                    b.Ball(Vector3.zero,new Vector3(.55f,.43f,.53f),new Color(.97f,.14f,.045f),12,7);
                    for(int i=0;i<5;i++) {float a=i*Mathf.PI*2/5;b.Leaf(new Vector3(0,.43f,0),new Vector3(Mathf.Sin(a)*.42f,.38f,Mathf.Cos(a)*.42f),.10f,Green);}
                    b.Rod(new Vector3(0,.41f,0),new Vector3(.08f,.67f,0),.055f,.035f,new Color(.18f,.40f,.08f));
                    b.Ball(new Vector3(-.23f,.25f,-.33f),new Vector3(.10f,.04f,.08f),new Color(1,.57f,.29f),6,3);break;
                case FoodKind.Cheese:
                    b.Wedge(new Color(1,.75f,.12f));
                    foreach(var hole in new[]{new Vector3(-.30f,.251f,.02f),new Vector3(.04f,.251f,.18f),new Vector3(.27f,.251f,.05f)})
                        b.Ball(hole,new Vector3(.09f,.012f,.085f),new Color(.74f,.38f,.035f),8,3);
                    b.Ball(new Vector3(0,-.04f,.451f),new Vector3(.12f,.09f,.012f),new Color(.74f,.38f,.035f),8,4);
                    b.Ball(new Vector3(.35f,.11f,.451f),new Vector3(.065f,.065f,.012f),new Color(.74f,.38f,.035f),6,4);break;
                case FoodKind.Tofu:
                    b.Box(Vector3.zero,new Vector3(.95f,.65f,.85f),new Color(1,.98f,.87f));
                    for(int i=-1;i<=1;i++) {b.Rod(new Vector3(-.46f,.328f,i*.21f),new Vector3(.46f,.328f,i*.21f),.012f,.012f,new Color(.76f,.75f,.61f),4);b.Rod(new Vector3(i*.24f,.328f,-.41f),new Vector3(i*.24f,.328f,.41f),.012f,.012f,new Color(.76f,.75f,.61f),4);}
                    for(int i=0;i<3;i++)b.Box(new Vector3(-.25f+i*.13f,.35f,-.1f+(i%2)*.08f),new Vector3(.10f,.035f,.045f),Green);break;
                case FoodKind.Flour:
                    b.Ball(new Vector3(0,-.04f,0),new Vector3(.48f,.52f,.35f),new Color(.79f,.62f,.36f),10,6);
                    b.Profile(Vector3.zero,new[]{.30f,.36f,.35f},new[]{.35f,.47f,.53f},new Color(.64f,.41f,.20f),10);
                    b.Ball(new Vector3(0,.48f,0),new Vector3(.30f,.10f,.30f),new Color(1,.98f,.88f));
                    b.Rod(new Vector3(0,-.28f,-.346f),new Vector3(0,.22f,-.346f),.024f,.016f,new Color(.42f,.24f,.07f),4);
                    for(int i=0;i<3;i++)for(int side=-1;side<=1;side+=2)b.Rod(new Vector3(0,-.10f+i*.13f,-.35f),new Vector3(side*.14f,.02f+i*.13f,-.35f),.04f,.012f,new Color(.56f,.32f,.07f),4);break;
                case FoodKind.Spice:
                    b.Rod(new Vector3(-.42f,.14f,0),new Vector3(-.10f,.02f,0),.18f,.20f,new Color(.85f,.045f,.025f),8);
                    b.Rod(new Vector3(-.10f,.02f,0),new Vector3(.25f,-.12f,0),.20f,.11f,new Color(.92f,.07f,.025f),8);
                    b.Rod(new Vector3(.25f,-.12f,0),new Vector3(.61f,-.04f,0),.11f,.005f,new Color(.78f,.03f,.02f),8);
                    b.Rod(new Vector3(-.42f,.14f,0),new Vector3(-.57f,.43f,.03f),.08f,.035f,Green);
                    b.Rod(new Vector3(-.32f,.26f,-.07f),new Vector3(.03f,.13f,-.08f),.018f,.01f,new Color(1,.44f,.23f),4);break;
                case FoodKind.Gyoza: b.Ball(Vector3.zero,new Vector3(.5f,.26f,.25f),Cream); for(int i=0;i<4;i++) b.Ball(new Vector3(-.3f+i*.2f,.2f,0),new Vector3(.07f,.15f,.16f),new Color(.9f,.7f,.4f)); break;
                case FoodKind.SweetPork: b.Ball(Vector3.zero,new Vector3(.45f,.35f,.42f),new Color(.76f,.23f,.06f)); b.Ball(new Vector3(.25f,.2f,0),new Vector3(.17f,.12f,.17f),Green); break;
                case FoodKind.Omurice: b.Ball(Vector3.zero,new Vector3(.65f,.35f,.38f),new Color(1,.78f,.14f)); b.Ball(new Vector3(0,.33f,0),new Vector3(.4f,.045f,.08f),new Color(.9f,.17f,.08f)); break;
                default:
                    Color main = kind == FoodKind.FriedRice ? new Color(1,.75f,.3f) : kind == FoodKind.ChickenRice ? new Color(.94f,.38f,.15f) : kind == FoodKind.Curry ? new Color(.57f,.28f,.055f) : new Color(1,.7f,.13f);
                    b.Ball(Vector3.zero,new Vector3(.5f,.32f,.5f),main);
                    for(int i=0;i<5;i++) { float a=i*2.4f; b.Ball(new Vector3(Mathf.Sin(a)*.3f,.28f,Mathf.Cos(a)*.3f),Vector3.one*.09f,i%2==0?Green:Cream,5,3); }
                    break;
            }
            return b.Finish(kind.ToString());
        }
        public static Mesh Ring(float radius, float thickness, int segments = 96, bool detailed = true)
        {
            var b = new Builder();
            if(detailed) segments *= 2;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments, c=(i+1)*Mathf.PI*2/segments;
                b.Quad(new Vector3(Mathf.Sin(a)*(radius-thickness),0,Mathf.Cos(a)*(radius-thickness)),new Vector3(Mathf.Sin(a)*(radius+thickness),0,Mathf.Cos(a)*(radius+thickness)),new Vector3(Mathf.Sin(c)*(radius+thickness),0,Mathf.Cos(c)*(radius+thickness)),new Vector3(Mathf.Sin(c)*(radius-thickness),0,Mathf.Cos(c)*(radius-thickness)),Color.white);
            }
            return b.Finish("Ring");
        }
        public static Mesh Disc(bool detailed = true)
        {
            var b=new Builder();
            int segments=detailed?128:64;
            for(int i=0;i<segments;i++) {float a=i*Mathf.PI*2/segments,c=(i+1)*Mathf.PI*2/segments; b.Tri(Vector3.zero,new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)),new Vector3(Mathf.Sin(c),0,Mathf.Cos(c)),Color.white);}
            return b.Finish("Disc");
        }
        // Furniture meshes keep their original shape, normals and colors while each triangle splits in two.
        public static Mesh DoubleTriangles(Mesh source)
        {
            var v=source.vertices;var normals=source.normals;var colors=source.colors;
            var triangles=source.triangles;var positions=new List<Vector3>();var ns=new List<Vector3>();var cs=new List<Color>();var indices=new List<int>();
            for(int t=0;t<triangles.Length;t+=3)
            {
                int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                Vector3 mid=(v[a]+v[b])*.5f;
                Vector3 na=normals.Length==v.Length?normals[a]:Vector3.up,nb=normals.Length==v.Length?normals[b]:Vector3.up,nc=normals.Length==v.Length?normals[c]:Vector3.up;
                Color ca=colors.Length==v.Length?colors[a]:Color.white,cb=colors.Length==v.Length?colors[b]:Color.white,cc=colors.Length==v.Length?colors[c]:Color.white;
                int start=positions.Count;positions.AddRange(new[]{v[a],mid,v[c],mid,v[b],v[c]});
                ns.AddRange(new[]{na,(na+nb).normalized,nc,(na+nb).normalized,nb,nc});
                cs.AddRange(new[]{ca,(ca+cb)*.5f,cc,(ca+cb)*.5f,cb,cc});
                for(int i=0;i<6;i++)indices.Add(start+i);
            }
            var result=new Mesh {name=source.name+" 2x",indexFormat=positions.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
            result.SetVertices(positions);result.SetNormals(ns);result.SetColors(cs);result.SetTriangles(indices,0);result.RecalculateBounds();return result;
        }
        public sealed class Builder
        {
            readonly bool detailed;
            public Builder(bool detailed=false) {this.detailed=detailed;}
            readonly List<Vector3> verts=new List<Vector3>(); readonly List<int> indices=new List<int>(); readonly List<Color> colors=new List<Color>();
            public void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
            { int n=verts.Count; verts.Add(a);verts.Add(b);verts.Add(c); indices.Add(n);indices.Add(n+1);indices.Add(n+2); colors.Add(color);colors.Add(color);colors.Add(color); }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color) {Tri(a,b,c,color);Tri(a,c,d,color);}
            public void Ball(Vector3 p,Vector3 size,Color color,int sides=8,int rows=5)
            {
                if(detailed) sides *= 2;
                for(int y=0;y<rows;y++) for(int x=0;x<sides;x++)
                { float a=x*Mathf.PI*2/sides,c=(x+1)*Mathf.PI*2/sides,t=y*Mathf.PI/rows,u=(y+1)*Mathf.PI/rows;
                  Quad(p+Vector3.Scale(Sphere(a,t),size),p+Vector3.Scale(Sphere(a,u),size),p+Vector3.Scale(Sphere(c,u),size),p+Vector3.Scale(Sphere(c,t),size),color); }
            }
            static Vector3 Sphere(float a,float t) => new Vector3(Mathf.Sin(t)*Mathf.Sin(a),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Cos(a));
            public void Box(Vector3 p,Vector3 scale,Color color)
            {
                Vector3[] v=new Vector3[8]; for(int i=0;i<8;i++) v[i]=p+Vector3.Scale(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f),scale);
                int[] f={0,2,3,1,4,5,7,6,0,4,6,2,1,3,7,5,0,1,5,4,2,6,7,3};
                for(int i=0;i<f.Length;i+=4)
                {
                    Vector3 a=v[f[i]],b=v[f[i+1]],c=v[f[i+2]],d=v[f[i+3]];
                    if(detailed) {Vector3 ab=(a+b)*.5f,cd=(c+d)*.5f;Quad(a,ab,cd,d,color);Quad(ab,b,c,cd,color);}
                    else Quad(a,b,c,d,color);
                }
            }
            public void RoundedBox(Vector3 center,Vector3 size,float bevel,Color color,Quaternion rotation)
            {
                Vector3 half=size*.5f,inner=half-Vector3.one*bevel;
                Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
                int columns=detailed?8:4,rows=4;
                foreach(Vector3 n in normals)
                {
                    Vector3 u=Vector3.Cross(Mathf.Abs(n.y)>.9f?Vector3.forward:Vector3.up,n).normalized,v=Vector3.Cross(n,u);
                    for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                    {
                        Vector3[] corners=new Vector3[4];
                        int[] dx={0,1,1,0},dy={0,0,1,1};
                        for(int c=0;c<4;c++)
                        {
                            Vector3 face=n+u*((x+dx[c])*2f/columns-1)+v*((y+dy[c])*2f/rows-1);
                            Vector3 flat=Vector3.Scale(face,half);
                            Vector3 core=new Vector3(Mathf.Clamp(flat.x,-inner.x,inner.x),Mathf.Clamp(flat.y,-inner.y,inner.y),Mathf.Clamp(flat.z,-inner.z,inner.z));
                            corners[c]=center+rotation*(core+(flat-core).normalized*bevel);
                        }
                        Quad(corners[0],corners[1],corners[2],corners[3],color);
                    }
                }
            }
            void DetailTri(Vector3 a,Vector3 b,Vector3 c,Color color)
            {if(detailed){Vector3 mid=(a+b)*.5f;Tri(a,mid,c,color);Tri(mid,b,c,color);}else Tri(a,b,c,color);}
            void DetailQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
            {DetailTri(a,b,c,color);DetailTri(a,c,d,color);}
            public void Profile(Vector3 origin,float[] radii,float[] heights,Color color,int sides=12)
            {
                if(detailed)sides*=2;
                for(int j=0;j<radii.Length-1;j++)for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,c=(i+1)*Mathf.PI*2/sides;
                    Vector3 lowerA=new Vector3(Mathf.Cos(a)*radii[j],heights[j],Mathf.Sin(a)*radii[j]);
                    Vector3 upperA=new Vector3(Mathf.Cos(a)*radii[j+1],heights[j+1],Mathf.Sin(a)*radii[j+1]);
                    Vector3 upperB=new Vector3(Mathf.Cos(c)*radii[j+1],heights[j+1],Mathf.Sin(c)*radii[j+1]);
                    Vector3 lowerB=new Vector3(Mathf.Cos(c)*radii[j],heights[j],Mathf.Sin(c)*radii[j]);
                    Quad(origin+lowerA,origin+upperA,origin+upperB,origin+lowerB,color);
                }
            }
            public void Rod(Vector3 start,Vector3 end,float r1,float r2,Color color,int sides=6)
            {
                if(detailed)sides*=2;
                Vector3 axis=(end-start).normalized;
                Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized,v=Vector3.Cross(axis,u);
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,c=(i+1)*Mathf.PI*2/sides;
                    Vector3 ra=u*Mathf.Cos(a)+v*Mathf.Sin(a),rb=u*Mathf.Cos(c)+v*Mathf.Sin(c);
                    Quad(start+ra*r1,start+rb*r1,end+rb*r2,end+ra*r2,color);
                    Tri(start,start+rb*r1,start+ra*r1,color);Tri(end,end+ra*r2,end+rb*r2,color);
                }
            }
            public void Leaf(Vector3 start,Vector3 end,float width,Color color)
            {
                Vector3 side=Vector3.Cross(end-start,Vector3.forward).normalized*width;
                Vector3 mid=(start+end)*.5f;
                DetailQuad(start,mid+side,end,mid-side,color);
                DetailQuad(start,mid-side,end,mid+side,color*.85f);
            }
            public void TrianglePrism(Vector3 center,Vector3 size,Color color)
            {
                Vector3 a=center+new Vector3(-size.x*.5f,-size.y*.5f,-size.z*.5f);
                Vector3 b=center+new Vector3(size.x*.5f,-size.y*.5f,-size.z*.5f);
                Vector3 c=center+new Vector3(0,size.y*.5f,-size.z*.5f),back=Vector3.forward*size.z;
                DetailTri(a,c,b,color);DetailTri(a+back,b+back,c+back,color);
                DetailQuad(a,b,b+back,a+back,color);DetailQuad(b,c,c+back,b+back,color);DetailQuad(c,a,a+back,c+back,color);
            }
            public void Wedge(Color color)
            {
                Vector3 a=new Vector3(-.62f,-.25f,.45f),b=new Vector3(.62f,-.25f,.45f),c=new Vector3(0,-.25f,-.5f);
                Vector3 up=Vector3.up*.5f;
                DetailTri(a,c,b,color);DetailTri(a+up,b+up,c+up,color);
                DetailQuad(a,b,b+up,a+up,color);DetailQuad(b,c,c+up,b+up,color);DetailQuad(c,a,a+up,c+up,color);
            }
            public Mesh Finish(string name) {var m=new Mesh {name=name,indexFormat=verts.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};m.SetVertices(verts);m.SetTriangles(indices,0);m.SetColors(colors);m.RecalculateNormals();m.RecalculateBounds();return m;}
        }
    }
}
