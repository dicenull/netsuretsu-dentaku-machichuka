using System.Collections.Generic;
using UnityEngine;

namespace SpinningFeast
{
    // Original procedural ingredient silhouettes and category-specific plated meals.
    public static class FeastExpansionMeshes
    {
        static readonly Color Cream=new Color(1,.94f,.75f), Green=new Color(.25f,.62f,.13f), Brown=new Color(.50f,.22f,.08f);
        public static bool IsMeal(FoodKind kind) => kind>=FoodKind.EggFriedRice && kind<FoodKind.Count;
        public static Mesh Ingredient(FoodKind kind,bool detailed)
        {
            var b=new FeastMeshes.Builder(detailed);
            switch(kind)
            {
                case FoodKind.Beef:
                    b.Ball(Vector3.zero,new Vector3(.65f,.18f,.43f),new Color(.57f,.08f,.12f),12,6);
                    for(int i=0;i<5;i++)b.Rod(new Vector3(-.4f+i*.18f,.17f,-.28f),new Vector3(-.3f+i*.18f,.18f,.24f),.025f,.012f,new Color(1,.72f,.67f));break;
                case FoodKind.Shrimp: Shrimp(b,Vector3.zero,.9f);break;
                case FoodKind.Fish:
                    b.Ball(Vector3.zero,new Vector3(.72f,.22f,.30f),new Color(.22f,.51f,.67f),12,6);
                    b.Ball(new Vector3(0,-.08f,-.03f),new Vector3(.61f,.1f,.28f),new Color(.75f,.85f,.83f));
                    b.Leaf(new Vector3(-.55f,0,0),new Vector3(-.98f,0,.02f),.26f,new Color(.12f,.34f,.50f));
                    b.Ball(new Vector3(.48f,.10f,-.21f),Vector3.one*.065f,Color.black);
                    for(int i=0;i<4;i++)b.Rod(new Vector3(-.28f+i*.16f,.18f,-.12f),new Vector3(-.25f+i*.16f,.1f,-.27f),.012f,.008f,Cream);break;
                case FoodKind.Squid:
                    b.Profile(Vector3.zero,new[]{.06f,.31f,.35f,.2f,0f},new[]{-.25f,-.10f,.3f,.62f,.8f},new Color(.95f,.75f,.72f));
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;b.Rod(new Vector3(Mathf.Sin(a)*.14f,-.2f,Mathf.Cos(a)*.14f),new Vector3(Mathf.Sin(a)*.38f,-.7f,Mathf.Cos(a)*.38f),.045f,.018f,Cream);}
                    b.Ball(new Vector3(.2f,-.03f,-.22f),Vector3.one*.055f,Color.black);break;
                case FoodKind.Noodles:
                    for(int i=0;i<12;i++)b.Rod(new Vector3(-.45f,-.13f+(i/4)*.09f,(i%4-1.5f)*.11f),new Vector3(.5f,.08f+(i/4)*.09f,(i%4-1.5f)*.11f),.045f,.045f,Cream);
                    b.Box(Vector3.zero,new Vector3(.22f,.49f,.51f),new Color(.7f,.19f,.12f));break;
                case FoodKind.Cabbage:
                    b.Ball(Vector3.zero,new Vector3(.52f,.48f,.5f),new Color(.62f,.82f,.28f),12,7);
                    for(int i=0;i<7;i++){float a=i*2.4f;var end=new Vector3(Mathf.Sin(a)*.54f,.26f,Mathf.Cos(a)*.54f);b.Leaf(new Vector3(0,-.34f,0),end,.28f,i%2==0?Green:new Color(.43f,.71f,.21f));b.Rod(new Vector3(0,-.34f,0),end,.028f,.012f,Cream);}break;
                case FoodKind.Mushroom:
                    for(int i=0;i<3;i++){var p=new Vector3((i-1)*.25f,0,(i%2)*.2f);b.Rod(p+Vector3.down*.4f,p+Vector3.up*.15f,.1f,.09f,Cream);b.Ball(p+Vector3.up*.18f,new Vector3(.30f,.22f,.29f),new Color(.49f,.29f,.13f));b.Ball(p+new Vector3(-.1f,.36f,0),new Vector3(.04f,.015f,.05f),Cream);}break;
                case FoodKind.Potato:
                    b.Ball(Vector3.zero,new Vector3(.57f,.38f,.43f),new Color(.74f,.52f,.24f),12,6);
                    for(int i=0;i<8;i++){float a=i*2.4f;b.Ball(new Vector3(Mathf.Sin(a)*.38f,.29f,Mathf.Cos(a)*.26f),new Vector3(.03f,.018f,.03f),Brown,6,3);}break;
                case FoodKind.Scallion:
                    for(int i=-1;i<=1;i++){var p=new Vector3(i*.17f,0,0);b.Rod(p+Vector3.down*.6f,p+Vector3.up*.1f,.075f,.065f,Cream);b.Rod(p+Vector3.up*.1f,p+new Vector3(i*.08f,.85f,.1f),.065f,.025f,Green);}break;
                case FoodKind.Garlic:
                    for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;b.Ball(new Vector3(Mathf.Sin(a)*.22f,0,Mathf.Cos(a)*.22f),new Vector3(.18f,.31f,.18f),i%2==0?Cream:Color.white);}
                    b.Rod(new Vector3(0,.15f,0),new Vector3(.08f,.62f,0),.12f,.02f,new Color(.77f,.66f,.45f));break;
                case FoodKind.Ginger:
                    for(int i=0;i<5;i++)b.Ball(new Vector3((i-2)*.21f,(i%2)*.15f,(i%3)*.08f),new Vector3(.24f,.17f,.18f),new Color(.85f,.65f,.33f));break;
                case FoodKind.SoySauce:
                    b.Profile(Vector3.zero,new[]{0f,.3f,.3f,.14f,.14f,0f},new[]{-.55f,-.5f,.28f,.42f,.68f,.68f},new Color(.18f,.07f,.035f));
                    b.Profile(Vector3.zero,new[]{.16f,.16f},new[]{.59f,.77f},new Color(.84f,.14f,.045f));
                    b.Box(new Vector3(0,-.06f,-.29f),new Vector3(.33f,.37f,.025f),Cream);break;
                case FoodKind.Miso:
                    b.Profile(Vector3.zero,new[]{0f,.35f,.47f,.43f,0f},new[]{-.35f,-.35f,.2f,.27f,.27f},new Color(.49f,.28f,.10f));
                    b.Ball(new Vector3(0,.25f,0),new Vector3(.39f,.11f,.39f),new Color(.72f,.38f,.11f));break;
                case FoodKind.Milk:
                    b.Box(Vector3.zero,new Vector3(.53f,.9f,.47f),Color.white);
                    b.Box(new Vector3(0,-.12f,-.245f),new Vector3(.54f,.34f,.025f),new Color(.25f,.64f,.88f));
                    b.Rod(new Vector3(0,.45f,0),new Vector3(0,.62f,0),.25f,.06f,Cream,4);break;
                case FoodKind.Butter:
                    b.Box(new Vector3(0,-.19f,0),new Vector3(1,.035f,.7f),Color.white);
                    b.RoundedBox(Vector3.zero,new Vector3(.8f,.34f,.51f),.04f,new Color(1,.81f,.19f),Quaternion.identity);
                    for(int i=0;i<3;i++)b.Rod(new Vector3(-.22f+i*.2f,.175f,-.25f),new Vector3(-.22f+i*.2f,.175f,.25f),.009f,.009f,Brown);break;
                case FoodKind.Sugar:
                    for(int i=0;i<5;i++)b.RoundedBox(new Vector3((i%2)*.35f-.17f,(i/2)*.24f-.25f,(i%3)*.07f),Vector3.one*.30f,.03f,Color.white,Quaternion.Euler(0,i*17,0));break;
                case FoodKind.Bread:
                    b.RoundedBox(Vector3.zero,new Vector3(.9f,.55f,.63f),.13f,new Color(.75f,.39f,.10f),Quaternion.identity);
                    b.RoundedBox(new Vector3(.455f,0,0),new Vector3(.025f,.44f,.52f),.01f,Cream,Quaternion.identity);
                    for(int i=0;i<3;i++)b.Rod(new Vector3(-.3f+i*.22f,.27f,-.16f),new Vector3(-.2f+i*.22f,.27f,.17f),.025f,.02f,Cream);break;
                case FoodKind.Seaweed:
                    for(int i=0;i<3;i++)b.Box(new Vector3(i*.05f,i*.045f,0),new Vector3(.78f,.025f,.65f),new Color(.035f,.16f+i*.025f,.08f));break;
                case FoodKind.Sesame:
                    b.Ball(new Vector3(0,-.1f,0),new Vector3(.5f,.17f,.42f),Brown);
                    for(int i=0;i<24;i++){float a=i*2.4f,r=Mathf.Sqrt(i/24f)*.38f;b.Ball(new Vector3(Mathf.Sin(a)*r,.07f,Mathf.Cos(a)*r),new Vector3(.045f,.025f,.075f),Cream,6,3);}break;
                case FoodKind.BellPepper:
                    for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;b.Ball(new Vector3(Mathf.Sin(a)*.19f,0,Mathf.Cos(a)*.19f),new Vector3(.28f,.46f,.28f),Green,10,6);}
                    b.Rod(new Vector3(0,.34f,0),new Vector3(.12f,.65f,0),.08f,.05f,new Color(.13f,.31f,.07f));break;
                case FoodKind.Eggplant:
                    b.Ball(new Vector3(0,-.12f,0),new Vector3(.33f,.6f,.31f),new Color(.24f,.08f,.34f),12,7);
                    for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;b.Leaf(new Vector3(0,.55f,0),new Vector3(Mathf.Sin(a)*.3f,.28f,Mathf.Cos(a)*.3f),.12f,Green);}
                    b.Rod(new Vector3(0,.4f,0),new Vector3(.1f,.75f,0),.065f,.03f,Green);break;
                case FoodKind.Broccoli:
                    b.Rod(new Vector3(0,-.6f,0),new Vector3(0,.15f,0),.15f,.21f,new Color(.5f,.71f,.23f));
                    for(int i=0;i<9;i++){float a=i*2.4f,r=Mathf.Sqrt(i/9f)*.38f;var p=new Vector3(Mathf.Sin(a)*r,.23f+(i%2)*.1f,Mathf.Cos(a)*r);b.Ball(p,Vector3.one*.25f,i%2==0?Green:new Color(.12f,.39f,.08f));}break;
                case FoodKind.Lemon:
                    b.Ball(Vector3.zero,new Vector3(.56f,.32f,.33f),new Color(1,.83f,.045f),12,7);
                    b.Ball(new Vector3(.53f,0,0),new Vector3(.10f,.08f,.08f),new Color(1,.83f,.045f));
                    b.Leaf(new Vector3(-.4f,.08f,0),new Vector3(-.65f,.42f,0),.15f,Green);break;
                case FoodKind.Corn:
                    b.Rod(new Vector3(0,-.5f,0),new Vector3(0,.5f,0),.23f,.18f,new Color(1,.72f,.06f));
                    for(int y=0;y<8;y++)for(int i=0;i<8;i++){float a=i*Mathf.PI/4;b.Ball(new Vector3(Mathf.Sin(a)*.23f,-.43f+y*.12f,Mathf.Cos(a)*.23f),new Vector3(.075f,.06f,.075f),new Color(1,.83f,.15f),6,3);}
                    b.Leaf(new Vector3(0,-.5f,0),new Vector3(-.45f,.36f,0),.18f,Green);break;
            }
            return b.Finish(kind+" ingredient");
        }
        static void Shrimp(FeastMeshes.Builder b,Vector3 p,float scale)
        {
            for(int i=0;i<7;i++){float a=-.7f+i*.4f;b.Ball(p+new Vector3(Mathf.Cos(a)*.4f,0,Mathf.Sin(a)*.4f)*scale,new Vector3(.16f,.13f,.13f)*scale,i%2==0?new Color(1,.40f,.22f):new Color(1,.72f,.52f),8,4);}
            b.Leaf(p+new Vector3(.05f,0,.4f)*scale,p+new Vector3(-.3f,0,.64f)*scale,.16f*scale,new Color(.94f,.24f,.12f));
        }
        public static Mesh Meal(FoodKind kind,bool detailed)
        {
            var b=new FeastMeshes.Builder(detailed);var recipe=Cookbook.All[(int)kind-Cookbook.RawCount];
            var ingredients=new List<FoodKind>();Gather(recipe,ingredients);
            Color sauce=ingredients.Contains(FoodKind.Tomato)?new Color(.78f,.19f,.055f):ingredients.Contains(FoodKind.Milk)?Cream:ingredients.Contains(FoodKind.Spice)?new Color(.66f,.23f,.055f):new Color(.54f,.31f,.10f);
            bool riceSoup=kind==FoodKind.RicePorridge || kind==FoodKind.Ochazuke || kind==FoodKind.Zosui || kind==FoodKind.ChickenZosui;
            if(riceSoup)sauce=kind==FoodKind.RicePorridge?new Color(.96f,.94f,.83f):new Color(.84f,.74f,.49f);
            if(kind==FoodKind.Chawanmushi)sauce=new Color(1,.83f,.43f);
            switch(recipe.Shape)
            {
                case MealShape.RiceBall:
                    for(int i=0;i<3;i++)
                    {
                        float a=i*Mathf.PI*2/3;var p=new Vector3(Mathf.Sin(a)*.44f,.29f,Mathf.Cos(a)*.4f);
                        b.TrianglePrism(p,new Vector3(.58f,.54f,.32f),kind==FoodKind.YakiOnigiri?new Color(.86f,.59f,.28f):Cream);
                        b.Box(p+new Vector3(0,-.12f,-.17f),new Vector3(.23f,.29f,.018f),new Color(.035f,.12f,.065f));
                        for(int j=0;j<8;j++)b.Ball(p+new Vector3((j%3-1)*.075f,.04f+(j/3)*.04f,-.166f),new Vector3(.025f,.018f,.012f),Color.white,6,3);
                        if(ingredients.Contains(FoodKind.Egg)||ingredients.Contains(FoodKind.Beef))
                            b.Ball(p+new Vector3(0,.04f,-.185f),new Vector3(.09f,.07f,.025f),ingredients.Contains(FoodKind.Beef)?Brown:new Color(1,.67f,.09f),8,4);
                    }
                    break;
                case MealShape.Gratin:
                    b.Profile(Vector3.zero,new[]{0f,.65f,.95f,.94f,.83f,0f},new[]{.01f,.01f,.24f,.29f,.22f,.22f},Cream,24);
                    b.Ball(new Vector3(0,.24f,0),new Vector3(.86f,.055f,.83f),new Color(1,.81f,.37f),24,5);
                    for(int i=0;i<28;i++){float a=i*2.4f,r=Mathf.Sqrt(i/28f)*.78f;b.Ball(new Vector3(Mathf.Sin(a)*r,.29f,Mathf.Cos(a)*r),new Vector3(.065f,.012f,.045f),i%3==0?Brown:new Color(.90f,.48f,.13f),6,3);}
                    break;
                case MealShape.Flatbread:
                    b.Ball(new Vector3(0,.1f,0),new Vector3(.93f,.10f,.9f),new Color(.83f,.55f,.23f),24,6);
                    for(int i=0;i<14;i++){float a=i*2.4f,r=Mathf.Sqrt(i/14f)*.75f;var p=new Vector3(Mathf.Sin(a)*r,.18f,Mathf.Cos(a)*r);b.Rod(p,p+new Vector3(.12f,.01f,.05f),.022f,.015f,Green);}
                    for(int i=-3;i<=3;i++)
                    {
                        float x=i*.19f,extent=Mathf.Sqrt(.73f*.73f-x*x);
                        b.Rod(new Vector3(x,.21f,-extent),new Vector3(x,.21f,extent),.025f,.025f,Brown);
                        b.Rod(new Vector3(-extent,.225f,x),new Vector3(extent,.225f,x),.013f,.013f,Cream);
                    }
                    if(ingredients.Contains(FoodKind.Shrimp))for(int i=0;i<3;i++)Shrimp(b,new Vector3((i-1)*.37f,.24f,0),.30f);
                    if(ingredients.Contains(FoodKind.Cheese))Topping(b,FoodKind.Cheese,new Vector3(0,.24f,.3f),0);
                    break;
                case MealShape.Sandwich:
                    for(int i=0;i<2;i++)
                    {
                        var p=new Vector3((i-.5f)*.72f,.35f,0);
                        b.TrianglePrism(p+Vector3.forward*.24f,new Vector3(.68f,.64f,.11f),Cream);
                        b.TrianglePrism(p,new Vector3(.64f,.60f,.35f),ingredients.Contains(FoodKind.Beef)||ingredients.Contains(FoodKind.Pork)?Brown:new Color(1,.72f,.19f));
                        b.TrianglePrism(p-Vector3.forward*.18f,new Vector3(.66f,.62f,.035f),ingredients.Contains(FoodKind.Cabbage)?Green:new Color(1,.86f,.42f));
                        b.TrianglePrism(p-Vector3.forward*.25f,new Vector3(.68f,.64f,.11f),Cream);
                    }
                    break;
                case MealShape.Burger:
                    b.Ball(new Vector3(0,.13f,0),new Vector3(.67f,.13f,.66f),new Color(.84f,.51f,.20f),24,6);
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;b.Leaf(new Vector3(0,.25f,0),new Vector3(Mathf.Sin(a)*.76f,.27f,Mathf.Cos(a)*.76f),.23f,Green);}
                    b.Ball(new Vector3(0,.35f,0),new Vector3(.65f,.10f,.64f),ingredients.Contains(FoodKind.Fish)||ingredients.Contains(FoodKind.Chicken)?new Color(.88f,.52f,.15f):Brown,24,5);
                    if(ingredients.Contains(FoodKind.Cheese))b.Box(new Vector3(0,.455f,0),new Vector3(1.12f,.025f,1.12f),new Color(1,.77f,.15f));
                    if(ingredients.Contains(FoodKind.Tomato))for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;b.Ball(new Vector3(Mathf.Sin(a)*.31f,.49f,Mathf.Cos(a)*.31f),new Vector3(.28f,.035f,.28f),new Color(.90f,.15f,.06f),12,4);}
                    b.Ball(new Vector3(0,.65f,0),new Vector3(.69f,.26f,.68f),new Color(.89f,.58f,.23f),24,8);
                    for(int i=0;i<23;i++){float a=i*2.4f,r=Mathf.Sqrt(i/23f)*.55f;b.Ball(new Vector3(Mathf.Sin(a)*r,.66f+.26f*Mathf.Sqrt(1-r*r/(.69f*.69f)),Mathf.Cos(a)*r),new Vector3(.022f,.012f,.043f),Cream,6,3);}
                    break;
                case MealShape.Omelette:
                    b.Ball(new Vector3(0,.18f,0),new Vector3(.87f,.22f,.47f),new Color(1,.72f,.10f),24,8);
                    b.Rod(new Vector3(-.59f,.37f,0),new Vector3(.59f,.37f,0),.018f,.018f,new Color(.89f,.47f,.065f));
                    if(ingredients.Contains(FoodKind.Cheese))for(int i=0;i<6;i++)b.Rod(new Vector3(-.4f+i*.15f,.36f,-.13f),new Vector3(-.34f+i*.15f,.37f,.13f),.02f,.016f,Cream);
                    break;
                case MealShape.Cream:
                    b.Profile(Vector3.zero,new[]{0f,.45f,.66f,.64f,.54f,0f},new[]{.01f,.01f,.22f,.26f,.2f,.2f},Cream,24);
                    b.Ball(new Vector3(0,.24f,0),new Vector3(.55f,.13f,.55f),new Color(1,.83f,.42f),20,6);
                    for(int i=0;i<30;i++){float a=i*.6f,r=.42f*(1-i/35f);b.Ball(new Vector3(Mathf.Sin(a)*r,.28f+i*.005f,Mathf.Cos(a)*r),new Vector3(.065f,.035f,.065f),new Color(1,.9f,.58f),8,4);}
                    break;
                case MealShape.Rice:
                    b.Ball(new Vector3(0,.12f,0),new Vector3(.90f,.24f,.87f),Cream,20,7);
                    for(int i=0;i<42;i++){float a=i*2.4f,r=Mathf.Sqrt(i/42f)*.8f;b.Ball(new Vector3(Mathf.Sin(a)*r,.28f,Mathf.Cos(a)*r),new Vector3(.045f,.03f,.075f),Color.white,6,3);}
                    if(ingredients.Contains(FoodKind.Spice))b.Ball(new Vector3(.2f,.29f,0),new Vector3(.67f,.06f,.77f),sauce,20,5);
                    break;
                case MealShape.Noodle:
                case MealShape.Soup:
                    b.Profile(Vector3.zero,new[]{0f,.55f,.87f,.96f,.91f,.72f,0f},new[]{.01f,.01f,.18f,.37f,.39f,.14f,.14f},new Color(.94f,.89f,.72f),24);
                    b.Ball(new Vector3(0,.28f,0),new Vector3(.84f,.06f,.84f),sauce,20,5);
                    if(riceSoup)for(int i=0;i<38;i++){float a=i*2.4f,r=Mathf.Sqrt(i/38f)*.72f;b.Ball(new Vector3(Mathf.Sin(a)*r,.34f,Mathf.Cos(a)*r),new Vector3(.03f,.018f,.055f),Cream,6,3);}
                    if(recipe.Shape==MealShape.Noodle)
                        for(int i=0;i<12;i++)for(int j=0;j<6;j++)
                        {
                            float x=-.63f+j*.21f,y=.36f+(i%3)*.015f,z=(i-5.5f)*.1f;
                            b.Rod(new Vector3(x,y,z+Mathf.Sin(j+i)*.04f),new Vector3(x+.21f,y,z+Mathf.Sin(j+1+i)*.04f),.024f,.024f,Cream,6);
                        }
                    break;
                case MealShape.Fried:
                    for(int i=0;i<7;i++)
                    {
                        float a=i*2.4f,r=Mathf.Sqrt(i/7f)*.62f;var p=new Vector3(Mathf.Sin(a)*r,.18f,Mathf.Cos(a)*r);
                        if(kind==FoodKind.FriedPotato)b.RoundedBox(p,new Vector3(.18f,.18f,.62f),.04f,new Color(1,.67f,.12f),Quaternion.Euler(0,i*39,0));
                        else b.Ball(p,new Vector3(.29f,.19f,.24f),new Color(.87f,.47f,.10f),12,6);
                        for(int j=0;j<5;j++)b.Ball(p+new Vector3(Mathf.Sin(j*2.4f)*.16f,.16f,Mathf.Cos(j*2.4f)*.16f),Vector3.one*.035f,Cream,6,3);
                    }
                    break;
                case MealShape.Dumpling:
                    for(int i=0;i<6;i++)
                    {
                        float a=i*Mathf.PI/3;var p=new Vector3(Mathf.Sin(a)*.52f,.16f,Mathf.Cos(a)*.52f);
                        if(kind==FoodKind.SpringRoll)b.Rod(p+new Vector3(-.18f,0,0),p+new Vector3(.18f,0,0),.14f,.14f,new Color(.87f,.57f,.20f),12);
                        else if(kind==FoodKind.CheeseGyoza || kind==FoodKind.ShrimpGyoza || kind==FoodKind.VegetableGyoza)
                        {
                            b.Ball(p,new Vector3(.26f,.15f,.13f),Cream,12,6);
                            for(int j=0;j<5;j++)b.Rod(p+new Vector3(-.18f+j*.09f,.1f,-.1f),p+new Vector3(-.15f+j*.08f,.20f,.01f),.023f,.018f,new Color(.88f,.71f,.43f));
                        }
                        else if(kind==FoodKind.Shumai || kind==FoodKind.ShrimpShumai){b.Profile(p,new[]{.18f,.2f,.23f,.17f},new[]{-.14f,-.12f,.16f,.17f},Cream);b.Ball(p+Vector3.up*.15f,new Vector3(.13f,.04f,.13f),new Color(.87f,.54f,.36f));}
                        else {b.Ball(p,new Vector3(.26f,.19f,.23f),Cream,12,6);for(int j=0;j<6;j++){float c=j*Mathf.PI/3;b.Rod(p+new Vector3(Mathf.Sin(c)*.15f,.12f,Mathf.Cos(c)*.15f),p+new Vector3(0,.23f,0),.018f,.012f,new Color(.79f,.66f,.43f));}}
                    }
                    break;
                case MealShape.Bread:
                    if(kind==FoodKind.Pizza){b.Ball(new Vector3(0,.08f,0),new Vector3(.94f,.10f,.94f),new Color(.83f,.51f,.19f),24,5);b.Ball(new Vector3(0,.15f,0),new Vector3(.85f,.04f,.85f),new Color(.81f,.19f,.06f),24,5);b.Ball(new Vector3(0,.18f,0),new Vector3(.77f,.025f,.77f),new Color(1,.77f,.29f),24,5);}
                    else for(int i=0;i<2;i++){b.RoundedBox(new Vector3((i-.5f)*.65f,.13f,0),new Vector3(.59f,.22f,1.05f),.08f,new Color(.75f,.40f,.13f),Quaternion.identity);b.RoundedBox(new Vector3((i-.5f)*.65f,.25f,0),new Vector3(.49f,.025f,.93f),.01f,new Color(.98f,.76f,.34f),Quaternion.identity);}
                    break;
                case MealShape.Dessert:
                    if(kind==FoodKind.CreamPuff)
                    {
                        for(int i=0;i<3;i++)
                        {
                            float a=i*Mathf.PI*2/3;var p=new Vector3(Mathf.Sin(a)*.45f,.13f,Mathf.Cos(a)*.43f);
                            b.Ball(p,new Vector3(.31f,.13f,.29f),new Color(.82f,.49f,.17f),16,6);
                            b.Ball(p+Vector3.up*.13f,new Vector3(.29f,.07f,.28f),new Color(1,.91f,.64f),16,5);
                            b.Ball(p+Vector3.up*.25f,new Vector3(.30f,.16f,.28f),new Color(.9f,.60f,.25f),16,6);
                            for(int j=0;j<7;j++){float c=j*2.4f;b.Ball(p+new Vector3(Mathf.Sin(c)*.18f,.34f,Mathf.Cos(c)*.18f),new Vector3(.05f,.035f,.04f),new Color(.97f,.75f,.41f),6,3);}
                        }
                    }
                    else if(kind==FoodKind.Pudding){b.Profile(Vector3.zero,new[]{0f,.65f,.5f,0f},new[]{.02f,.02f,.55f,.55f},new Color(1,.77f,.29f),24);b.Ball(new Vector3(0,.56f,0),new Vector3(.51f,.035f,.51f),Brown,24,4);}
                    else if(kind==FoodKind.Pancake){for(int i=0;i<3;i++)b.Ball(new Vector3(0,.09f+i*.12f,0),new Vector3(.77f,.09f,.77f),new Color(.88f,.56f,.23f),24,5);b.Box(new Vector3(0,.43f,0),new Vector3(.25f,.07f,.23f),new Color(1,.9f,.42f));}
                    else for(int i=0;i<7;i++){float a=i*2.4f,r=Mathf.Sqrt(i/7f)*.6f;var p=new Vector3(Mathf.Sin(a)*r,.22f,Mathf.Cos(a)*r);b.Ball(p,Vector3.one*.24f,new Color(.80f,.48f,.14f),12,6);for(int j=0;j<7;j++){float c=j*2.4f;b.Ball(p+new Vector3(Mathf.Sin(c)*.16f,.18f,Mathf.Cos(c)*.16f),new Vector3(.022f,.015f,.038f),Cream,6,3);}}
                    break;
                case MealShape.Salad:
                    for(int i=0;i<12;i++){float a=i*2.4f;b.Leaf(new Vector3(0,.05f,0),new Vector3(Mathf.Sin(a)*.84f,.15f+(i%3)*.08f,Mathf.Cos(a)*.84f),.28f,i%2==0?Green:new Color(.57f,.78f,.25f));}
                    break;
                default:
                    b.Ball(new Vector3(0,.07f,0),new Vector3(.9f,.055f,.88f),sauce,20,5);break;
            }
            // Visible toppings follow the recipe, including ingredients inherited from completed dishes.
            if(recipe.Shape==MealShape.Rice || recipe.Shape==MealShape.Noodle || recipe.Shape==MealShape.Soup || recipe.Shape==MealShape.StirFry || recipe.Shape==MealShape.Bread || recipe.Shape==MealShape.Salad || recipe.Shape==MealShape.Gratin)
            {
                int count=0;
                foreach(var ingredient in ingredients)
                {
                    if(!HasTopping(ingredient) || (kind==FoodKind.Chawanmushi && ingredient==FoodKind.Egg))continue;
                    for(int j=0;j<3;j++)
                    {
                        float a=(count*3+j)*2.39996f+(int)kind*.12f,r=.22f+((count*3+j)%5)*.12f;
                        float y=recipe.Shape==MealShape.Noodle||recipe.Shape==MealShape.Soup?.4f:recipe.Shape==MealShape.Rice?.37f:recipe.Shape==MealShape.Gratin?.34f:.22f;
                        Topping(b,ingredient,new Vector3(Mathf.Sin(a)*r,y,Mathf.Cos(a)*r),j);
                    }
                    if(++count==5)break;
                }
            }
            if(recipe.Shape==MealShape.Fried || recipe.Shape==MealShape.Dumpling)
                b.Leaf(new Vector3(.5f,.03f,-.3f),new Vector3(.92f,.08f,-.4f),.23f,Green);
            return b.Finish(kind+" plated meal");
        }
        static void Gather(Recipe recipe,List<FoodKind> kinds)
        {
            foreach(var need in recipe.Needs)
            {
                var k=need.Any[0];
                if((int)k>=Cookbook.RawCount)Gather(Cookbook.All[(int)k-Cookbook.RawCount],kinds);
                else if(!kinds.Contains(k))kinds.Add(k);
            }
        }
        static bool HasTopping(FoodKind k) => k==FoodKind.Egg || k==FoodKind.Pork || k==FoodKind.Chicken || k==FoodKind.Beef || k==FoodKind.Shrimp || k==FoodKind.Fish || k==FoodKind.Squid || k==FoodKind.Carrot || k==FoodKind.Onion || k==FoodKind.Tomato || k==FoodKind.Cheese || k==FoodKind.Tofu || k==FoodKind.Cabbage || k==FoodKind.Mushroom || k==FoodKind.Potato || k==FoodKind.Scallion || k==FoodKind.Seaweed || k==FoodKind.BellPepper || k==FoodKind.Eggplant || k==FoodKind.Broccoli || k==FoodKind.Corn;
        static void Topping(FeastMeshes.Builder b,FoodKind k,Vector3 p,int variant)
        {
            switch(k)
            {
                case FoodKind.Shrimp:Shrimp(b,p,.35f);break;
                case FoodKind.Egg:b.Ball(p,new Vector3(.20f,.045f,.23f),Color.white,10,4);b.Ball(p+Vector3.up*.04f,new Vector3(.11f,.035f,.12f),new Color(1,.68f,.06f));break;
                case FoodKind.Fish:b.Ball(p,new Vector3(.24f,.06f,.13f),new Color(.98f,.51f,.35f));for(int i=0;i<3;i++)b.Rod(p+new Vector3((i-1)*.1f,.065f,-.07f),p+new Vector3((i-1)*.1f+.04f,.065f,.07f),.009f,.006f,Cream);break;
                case FoodKind.Squid:b.Profile(p,new[]{.11f,.16f,.16f,.11f,.11f},new[]{0f,0f,.07f,.07f,0f},Color.white,12);break;
                case FoodKind.Tofu:b.RoundedBox(p,new Vector3(.24f,.2f,.23f),.02f,Cream,Quaternion.Euler(0,variant*31,0));break;
                case FoodKind.Mushroom:b.Rod(p,p+Vector3.up*.14f,.04f,.04f,Cream);b.Ball(p+Vector3.up*.14f,new Vector3(.16f,.08f,.13f),Brown);break;
                case FoodKind.Cheese:for(int i=0;i<4;i++)b.Rod(p+new Vector3((i-2)*.06f,0,-.1f),p+new Vector3((i-2)*.06f+.06f,.03f,.12f),.015f,.015f,new Color(1,.82f,.26f));break;
                case FoodKind.Broccoli:for(int i=0;i<3;i++)b.Ball(p+new Vector3((i-1)*.07f,.05f,(i%2)*.08f),Vector3.one*.1f,Green);break;
                case FoodKind.Scallion:for(int i=0;i<3;i++)b.Rod(p+new Vector3((i-1)*.06f,0,0),p+new Vector3((i-1)*.06f,.03f,.08f),.025f,.025f,Green);break;
                case FoodKind.Cabbage:case FoodKind.BellPepper:b.Leaf(p,p+new Vector3(.2f,.05f,.17f),.12f,Green);break;
                case FoodKind.Seaweed:b.Box(p,new Vector3(.07f,.01f,.27f),new Color(.035f,.12f,.07f));break;
                case FoodKind.Corn:for(int i=0;i<4;i++)b.Ball(p+new Vector3((i%2)*.06f,0,(i/2)*.07f),new Vector3(.045f,.04f,.045f),new Color(1,.77f,.08f),6,3);break;
                default:
                    Color color=k==FoodKind.Tomato?new Color(.90f,.18f,.07f):k==FoodKind.Carrot?new Color(1,.43f,.07f):k==FoodKind.Eggplant?new Color(.30f,.09f,.36f):k==FoodKind.Onion?Cream:k==FoodKind.Potato?new Color(.97f,.77f,.35f):k==FoodKind.Chicken?new Color(.80f,.46f,.20f):Brown;
                    b.Ball(p,new Vector3(.17f,.10f,.13f),color,10,5);break;
            }
        }
    }
}
