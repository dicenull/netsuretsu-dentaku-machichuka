using UnityEngine;

namespace SpinningFeast
{
    // Composition reference: S&B curry rice / Kikkoman mapo tofu photographs.
    // All geometry is original; reference photographs are not included in the game.
    public static class FeastMeals
    {
        static Color Surface(float r,float g,float b,float shine) => new Color(r,g,b,shine);
        public static bool IsComposed(FoodKind kind) => kind==FoodKind.Curry || kind==FoodKind.MapoTofu || FeastExpansionMeshes.IsMeal(kind);
        public static Mesh Build(FoodKind kind,bool detailed)
        {
            if(FeastExpansionMeshes.IsMeal(kind))return FeastExpansionMeshes.Meal(kind,detailed);
            var b=new FeastMeshes.Builder(detailed);
            if(kind==FoodKind.Curry) Curry(b);else Mapo(b);
            return b.Finish(kind+" plated meal");
        }
        static void Curry(FeastMeshes.Builder b)
        {
            // Rice occupies the left half; curry is a shallow sauce pool with visible chunks.
            b.Ball(new Vector3(.17f,.09f,0),new Vector3(.77f,.09f,.92f),Surface(.48f,.22f,.055f,1),32,10);
            b.Ball(new Vector3(-.46f,.16f,0),new Vector3(.45f,.28f,.80f),Surface(.95f,.94f,.84f,.12f),20,10);
            for(int i=0;i<114;i++)
            {
                float a=i*2.399963f,r=Mathf.Sqrt((i+.5f)/114);
                float x=-.46f+Mathf.Cos(a)*.435f*r,z=Mathf.Sin(a)*.775f*r;
                float y=.16f+.28f*Mathf.Sqrt(1-r*r);
                float variation=.92f+.08f*Mathf.Sin(i*4.3f);
                b.Ball(new Vector3(x,y,z),new Vector3(.033f,.024f,.066f)*variation,Surface(1,.985f,.93f,.16f),6,4);
            }
            Vector3[] chunks={new Vector3(.15f,.16f,-.62f),new Vector3(.53f,.17f,-.43f),new Vector3(.22f,.18f,-.16f),new Vector3(.66f,.16f,.05f),new Vector3(.28f,.18f,.27f),new Vector3(.12f,.17f,.59f),new Vector3(.60f,.17f,.47f),new Vector3(-.04f,.16f,-.38f),new Vector3(.45f,.17f,.68f)};
            for(int i=0;i<chunks.Length;i++)
            {
                Color color=i%3==0?Surface(.91f,.36f,.075f,.8f):i%3==1?Surface(.70f,.48f,.18f,.7f):Surface(.33f,.15f,.065f,.75f);
                Vector3 size=i%3==0?new Vector3(.18f,.16f,.23f):new Vector3(.23f,.19f,.25f);
                b.RoundedBox(chunks[i],size,.035f,color,Quaternion.Euler(i*7%19,i*37,0));
                b.Ball(chunks[i]+Vector3.up*.075f,new Vector3(size.x*.49f,.025f,size.z*.46f),Surface(.49f,.23f,.075f,.95f),8,4);
            }
            // Small translucent-looking onion slivers follow the sauce surface.
            for(int i=0;i<12;i++)
            {
                float a=i*2.4f;Vector3 p=new Vector3(.24f+Mathf.Cos(a)*.43f,.175f,Mathf.Sin(a)*.65f);
                b.Rod(p,p+new Vector3(.06f,.005f,.025f),.012f,.007f,Surface(.75f,.52f,.26f,.65f),4);
            }
        }
        static void Mapo(FeastMeshes.Builder b)
        {
            b.Ball(new Vector3(0,.055f,0),new Vector3(.98f,.055f,.94f),Surface(.72f,.18f,.035f,1),32,10);
            b.Ball(new Vector3(0,.11f,0),new Vector3(.91f,.08f,.87f),Surface(.44f,.14f,.065f,.95f),32,10);
            for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)
            {
                if(x*x+z*z>5)continue;
                int i=(z+2)*5+x+2;
                Vector3 p=new Vector3(x*.33f+Mathf.Sin(i*8.1f)*.025f,.22f,z*.33f+Mathf.Cos(i*7.3f)*.025f);
                float size=.265f+.025f*Mathf.Sin(i*3.1f);
                b.RoundedBox(p,new Vector3(size,.27f+Mathf.Sin(i)*.035f,size),.025f,Surface(1,.91f,.72f,.5f),Quaternion.Euler(Mathf.Sin(i)*8,i*29,Mathf.Cos(i)*7));
                // A small coat of sauce crosses one edge, leaving most of the creamy tofu visible.
                b.Ball(p+new Vector3(.055f,.135f,.06f),new Vector3(.09f,.014f,.075f),Surface(.60f,.22f,.075f,.95f),8,4);
            }
            for(int i=0;i<125;i++)
            {
                float a=i*2.399963f,r=Mathf.Sqrt((i+.5f)/125)*.89f;
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r;
                int gx=Mathf.RoundToInt(x/.33f),gz=Mathf.RoundToInt(z/.33f);
                bool onTofu=gx*gx+gz*gz<=5 && Mathf.Abs(x-gx*.33f)<.13f && Mathf.Abs(z-gz*.33f)<.13f;
                float y=onTofu?.375f:.19f;
                float size=.026f+.013f*(.5f+.5f*Mathf.Sin(i*3.7f));
                b.Ball(new Vector3(x,y,z),new Vector3(size,size*.7f,size*1.15f),Surface(.30f+.08f*(i%3),.14f,.06f,.7f),5,3);
            }
            for(int i=0;i<26;i++)
            {
                float a=i*2.399963f,r=Mathf.Sqrt((i+.5f)/26)*.68f;
                Vector3 p=new Vector3(Mathf.Cos(a)*r,.395f,Mathf.Sin(a)*r);
                b.Rod(p,p+new Vector3(.055f,.015f,.025f),.018f,.017f,i%3==0?Surface(.94f,.95f,.76f,.3f):Surface(.18f,.48f,.12f,.35f),6);
            }
        }
        public static Mesh Plate(bool detailed=true)
        {
            var b=new FeastMeshes.Builder(detailed);
            b.Profile(Vector3.zero,new[]{0f,.78f,.98f,1f,.96f,.85f,.70f,0f},new[]{-.035f,-.035f,.02f,.10f,.12f,.065f,.025f,.02f},Surface(.98f,.97f,.92f,.7f),48);
            return b.Finish("Raised porcelain serving plate");
        }
    }
}
