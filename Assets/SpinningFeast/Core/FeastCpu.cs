using System;

namespace SpinningFeast
{
    // All levels use the same shared stock and cooking transaction as the player.
    public static class FeastCpu
    {
        public static int ClampLevel(int level)=>Math.Max(0,Math.Min(2,level));
        public static string Name(int level)=>level<=0?"やさしい":level>=2?"むずかしい":"ふつう";
        public static string Description(int level)=>level<=0?"のんびり、小さな得点から":level>=2?"素早く、大量調理をねらう":"いろいろな料理と連鎖をねらう";
        public static float FirstMove(int level)=>level<=0?25:level>=2?4:8;
        public static float Interval(int level)=>level<=0?20:level>=2?3:6;
        public static bool UsesPreparedFood(Recipe recipe)
        {
            foreach(var need in recipe.Needs)foreach(var kind in need.Any)
                if((int)kind>=Cookbook.RawCount)return true;
            return false;
        }
        public static int Choose(int level,int[] inventory,int turn)
        {
            level=ClampLevel(level);
            int count=Cookbook.All.Length,limit=level==0?8:level==1?32:count;
            int start=(int)((uint)turn*37u%(uint)(level==2?count:200)),considered=0,chosen=-1;
            double best=level==0?double.MaxValue:0;
            for(int offset=0;offset<count && considered<limit;offset++)
            {
                int index=(start+offset)%count;var recipe=Cookbook.All[index];
                if(level==0 && UsesPreparedFood(recipe))continue;
                considered++;
                var plan=RecipeSolver.Solve(recipe,inventory);
                if(plan.Servings<=0)continue;
                // Easy chooses the lowest-scoring available batch in its small sample.
                // Normal weighs a rotating subset; hard maximizes immediate score.
                double value=(double)plan.Servings*recipe.Points;
                if(level==1)value*=.7+((uint)(index*97+turn*31)%101)/100.0*.6;
                if(level==0?value<best:value>best){best=value;chosen=index;}
            }
            return chosen;
        }
    }
}
