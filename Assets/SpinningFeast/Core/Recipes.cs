using System;
using System.Collections.Generic;
using System.Text;

namespace SpinningFeast
{
    public sealed class Requirement
    {
        public readonly int Units;
        public readonly FoodKind[] Any;
        public Requirement(int units, params FoodKind[] any) { Units = units; Any = any; }
    }

    public sealed class Recipe
    {
        public MealShape Shape;
        public readonly FoodKind Output;
        public readonly string Name;
        public readonly string[] Aliases;
        public readonly int Points;
        public readonly Requirement[] Needs;
        public Recipe(FoodKind output, string name, int points, string aliases, params Requirement[] needs)
        { Output = output; Name = name; Points = points; Aliases = aliases.Split('|'); Needs = needs; }
    }

    public sealed class CookPlan
    {
        public int Servings;
        public readonly int[] Consume = new int[Cookbook.KindCount];
        public readonly List<FoodKind> Missing = new List<FoodKind>();
    }

    // Bipartite max flow avoids double-counting an ingredient shared by several slots.
    // Feasibility is monotonic: binary search finds the exact largest batch, not a greedy approximation.
    public static class RecipeSolver
    {
        public static CookPlan Solve(Recipe recipe, int[] stock)
        {
            int upper = int.MaxValue;
            foreach (Requirement need in recipe.Needs)
            {
                int sum = 0;
                foreach (FoodKind k in need.Any) sum += Math.Max(0, stock[(int)k]);
                upper = Math.Min(upper, sum / need.Units);
            }
            int lo = 0, hi = upper;
            while (lo < hi)
            {
                int mid = lo + (hi - lo + 1) / 2;
                if (Allocate(recipe, stock, mid, null, null)) lo = mid; else hi = mid - 1;
            }
            var plan = new CookPlan { Servings = lo };
            if (lo > 0) Allocate(recipe, stock, lo, plan.Consume, null);
            else Allocate(recipe, stock, 1, null, plan.Missing);
            return plan;
        }

        static bool Allocate(Recipe r, int[] stock, int count, int[] used, List<FoodKind> missing)
        {
            // Build only the ingredient nodes used by this recipe, so 200-recipe bot scans stay small.
            var active = new List<FoodKind>();
            foreach (var need in r.Needs) foreach (var kind in need.Any)
                if (!active.Contains(kind)) active.Add(kind);
            int kinds = active.Count, slots = r.Needs.Length;
            int source = kinds + slots, sink = source + 1, n = sink + 1;
            var cap = new int[n,n];
            for (int k = 0; k < kinds; k++) cap[source,k] = Math.Max(0, stock[(int)active[k]]);
            int demand = 0;
            for (int s = 0; s < slots; s++)
            {
                int amount = count * r.Needs[s].Units;
                cap[kinds+s,sink] = amount; demand += amount;
                foreach (FoodKind k in r.Needs[s].Any) cap[active.IndexOf(k),kinds+s] = amount;
            }
            int flow = 0;
            var previous = new int[n]; var queue = new int[n];
            while (true)
            {
                for (int i = 0; i < n; i++) previous[i] = -1;
                previous[source] = source; int head = 0, tail = 0; queue[tail++] = source;
                while (head < tail && previous[sink] < 0)
                {
                    int a = queue[head++];
                    for (int b = 0; b < n; b++) if (previous[b] < 0 && cap[a,b] > 0)
                    { previous[b] = a; queue[tail++] = b; }
                }
                if (previous[sink] < 0) break;
                int push = int.MaxValue;
                for (int b = sink; b != source; b = previous[b]) push = Math.Min(push, cap[previous[b],b]);
                for (int b = sink; b != source; b = previous[b]) { int a = previous[b]; cap[a,b] -= push; cap[b,a] += push; }
                flow += push;
            }
            if (used != null) for (int k = 0; k < kinds; k++) used[(int)active[k]] = Math.Max(0,stock[(int)active[k]]) - cap[source,k];
            if (missing != null)
                for (int s = 0; s < slots; s++) if (cap[kinds+s,sink] > 0)
                {
                    // One representative model per unsatisfied substitution slot, no quantities/text.
                    FoodKind representative = r.Needs[s].Any[0];
                    if (!missing.Contains(representative)) missing.Add(representative);
                }
            return flow == demand;
        }
    }
}
