using System;
using SpinningFeast;

public static class RecipeTests
{
    static int checks;
    static void Check(bool condition,string name) {checks++;if(!condition)throw new Exception(name);}
    static int[] Stock(params int[] v) {var s=new int[Cookbook.KindCount];Array.Copy(v,s,v.Length);return s;}
    static bool Brute(Recipe r,int[] stock,int portions,int slot=0,int unit=0)
    {
        if(slot==r.Needs.Length)return true;
        if(unit==r.Needs[slot].Units*portions)return Brute(r,stock,portions,slot+1,0);
        foreach(FoodKind kind in r.Needs[slot].Any)
        {
            int k=(int)kind;if(stock[k]==0)continue;stock[k]--;
            bool ok=Brute(r,stock,portions,slot,unit+1);stock[k]++;if(ok)return true;
        }
        return false;
    }
    public static void Main()
    {
        Check(Cookbook.Find(" ＦＲＩＥＤ ＲＩＣＥ ")==0,"Unicode/space normalization");
        Check(Cookbook.Find("炒飯")==0 && Cookbook.Find("ちゃーはん")==0,"Japanese aliases");
        Check(Cookbook.Find("unknown")==-1,"unknown input");
        var overlap=new Recipe(FoodKind.FriedRice,"overlap",1,"",new Requirement(1,FoodKind.Egg,FoodKind.Rice),new Requirement(1,FoodKind.Rice));
        var result=RecipeSolver.Solve(overlap,Stock(1,1));Check(result.Servings==1,"overlap needs reassignment, not greedy matching");
        result=RecipeSolver.Solve(overlap,Stock(1,0));Check(result.Servings==0 && result.Missing.Count>0,"cannot double consume shared stock");
        var r=Cookbook.All[0];var stocks=Stock(1000,1000,0,0,200,300);
        result=RecipeSolver.Solve(r,stocks);Check(result.Servings==500,"no artificial maximum batch cap");Check(result.Consume[0]==1000 && result.Consume[1]==500,"exact accounting");
        var random=new Random(6088);
        for(int trial=0;trial<800;trial++)
        {
            var needs=new Requirement[3];
            for(int i=0;i<3;i++)
            {
                int mask=random.Next(1,8);var options=new System.Collections.Generic.List<FoodKind>();
                for(int k=0;k<3;k++)if((mask&(1<<k))!=0)options.Add((FoodKind)k);
                needs[i]=new Requirement(random.Next(1,3),options.ToArray());
            }
            var recipe=new Recipe(FoodKind.Curry,"fuzz",1,"",needs);
            var stock=Stock(random.Next(5),random.Next(5),random.Next(5));
            var plan=RecipeSolver.Solve(recipe,stock);int best=0;
            for(int p=1;p<=4;p++)if(Brute(recipe,(int[])stock.Clone(),p))best=p;
            Check(plan.Servings==best,"exact maximum agrees with exhaustive search "+trial);
            for(int k=0;k<stock.Length;k++)Check(plan.Consume[k]>=0 && plan.Consume[k]<=stock[k],"bounded consumption");
            int sum=0,demand=0;foreach(int n in plan.Consume)sum+=n;foreach(var n in needs)demand+=n.Units*best;
            Check(sum==demand,"all slots fulfilled exactly");
        }
        Check(Cookbook.All.Length==400 && Cookbook.RawCount==35,"400 recipes and unchanged 35 ingredients");
        Check(Cookbook.Find("ねぎ入りチャーハン")==-1,"removed placeholder dish cannot be ordered");
        Check(Cookbook.Find("油淋鶏")>=200 && Cookbook.Find("肉じゃが")>=200 && Cookbook.Find("パエリア")>=200,"established dishes replace placeholders");
        var names=new System.Collections.Generic.Dictionary<string,int>();
        for(int i=0;i<Cookbook.All.Length;i++)
        {
            var recipe=Cookbook.All[i];
            Check(!recipe.Name.Contains("入り"),"no placeholder ingredient-added dish: "+recipe.Name);
            Check((int)recipe.Output==Cookbook.RawCount+i,"output ordering agrees with catalogue");
            var allNames=new System.Collections.Generic.List<string>(recipe.Aliases);allNames.Add(recipe.Name);
            foreach(var alias in allNames)
            {
                string key=Cookbook.Normalize(alias);
                Check(!names.ContainsKey(key) || names[key]==i,"no ambiguous alias: "+alias);names[key]=i;
                Check(Cookbook.Find(alias)==i,"name resolves: "+alias);
            }
            var available=new int[Cookbook.KindCount];
            foreach(var need in recipe.Needs)available[(int)need.Any[0]]+=need.Units*7;
            var plan=RecipeSolver.Solve(recipe,available);
            Check(plan.Servings==7,"each recipe cooks exactly seven portions: "+recipe.Name);
            Check(RecipeSolver.Solve(recipe,new int[Cookbook.KindCount]).Missing.Count>0,"missing effects exist: "+recipe.Name);
        }
        foreach(var recipe in Cookbook.All)
            foreach(var need in recipe.Needs)
                foreach(var kind in need.Any)
                    Check((int)kind<(int)recipe.Output,"acyclic recipe graph blocks score loops");
        Check(FeastCpu.FirstMove(0)>FeastCpu.FirstMove(1) && FeastCpu.FirstMove(1)>FeastCpu.FirstMove(2),"easier CPU gives more opening time");
        Check(FeastCpu.Interval(0)>FeastCpu.Interval(1) && FeastCpu.Interval(1)>FeastCpu.Interval(2),"CPU action rates increase with difficulty");
        Check(FeastCpu.FirstMove(0)==25 && FeastCpu.Interval(0)==20,"easy CPU allows an opening practice window and at most five turns in 120 seconds");
        Check(FeastCpu.FirstMove(1)==8 && FeastCpu.Interval(1)==6 && FeastCpu.FirstMove(2)==4 && FeastCpu.Interval(2)==3,"normal and hard CPU timing stays unchanged");
        var gentleStock=new int[Cookbook.KindCount];
        gentleStock[(int)FoodKind.Rice]=20;gentleStock[(int)FoodKind.Egg]=10;gentleStock[(int)FoodKind.Carrot]=10;
        gentleStock[(int)FoodKind.Flour]=1;gentleStock[(int)FoodKind.Pork]=1;
        Check(FeastCpu.Choose(0,gentleStock,0)==Cookbook.Find("餃子"),"easy CPU chooses one gyoza instead of ten fried-rice portions when both are possible");
        Check(FeastCpu.Choose(2,gentleStock,0)!=Cookbook.Find("餃子"),"hard CPU still prefers the larger available score");
        var cpuStock=new int[Cookbook.KindCount];for(int i=0;i<cpuStock.Length;i++)cpuStock[i]=i%7+3;
        var cpuBefore=(int[])cpuStock.Clone();long maximum=0;
        foreach(var recipe in Cookbook.All)maximum=Math.Max(maximum,(long)RecipeSolver.Solve(recipe,cpuStock).Servings*recipe.Points);
        var normalChoices=new System.Collections.Generic.HashSet<int>();
        for(int level=0;level<3;level++)
        {
            Check(FeastCpu.Choose(level,new int[Cookbook.KindCount],0)==-1,"CPU cannot cook without ingredients");
            for(int turn=0;turn<40;turn++)
            {
                int choice=FeastCpu.Choose(level,cpuStock,turn);
                Check(choice>=0 && choice<Cookbook.All.Length,"CPU chooses from the real catalogue");
                var recipe=Cookbook.All[choice];var plan=RecipeSolver.Solve(recipe,cpuStock);
                Check(plan.Servings>0,"CPU only chooses a currently possible recipe");
                if(level==0)Check(!FeastCpu.UsesPreparedFood(recipe),"easy CPU leaves prepared dishes for the player");
                if(level==1)normalChoices.Add(choice);
                if(level==2)Check((long)plan.Servings*recipe.Points==maximum,"hard CPU finds the maximum immediate score across all recipes");
            }
        }
        Check(normalChoices.Count>1,"normal CPU rotates its recipe choices");
        for(int i=0;i<cpuStock.Length;i++)Check(cpuStock[i]==cpuBefore[i],"CPU decision does not consume or create shared stock");
        Console.WriteLine("PASS: "+checks+" assertions (800 randomized exhaustive comparisons; three CPU difficulties)");
    }
}
