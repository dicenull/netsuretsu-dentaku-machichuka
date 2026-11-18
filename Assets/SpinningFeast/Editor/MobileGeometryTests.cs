using System;
using UnityEditor;
using UnityEngine;
namespace SpinningFeast.Editor
{
    public static class MobileGeometryTests
    {
        [MenuItem("Spinning Feast/Validate Full Density Mobile Geometry")]
        public static void Validate()
        {
            long full=0,mobile=0,small=0,rawMobile=0;
            for(int i=0;i<Cookbook.KindCount;i++)
            {
                Mesh a=FeastMeshes.Food((FoodKind)i,true),b=FeastMeshes.Food((FoodKind)i,false);
                try
                {
                    long x=(long)a.GetIndexCount(0)/3,y=(long)b.GetIndexCount(0)/3;
                    if(y<=0 || y>x || b.bounds.size.sqrMagnitude<=0)throw new Exception("Invalid mobile food geometry: "+i);
                    full+=x;mobile+=y;
                    var low=FeastMeshes.Food((FoodKind)i,false,true);
                    try{
                        long z=(long)low.GetIndexCount(0)/3;
                        if(z<=0 || z>y)throw new Exception("Invalid small food geometry: "+i);
                        if(i<Cookbook.RawCount){small+=z;rawMobile+=y;if(low.GetVertexAttributeFormat(UnityEngine.Rendering.VertexAttribute.Color)!=UnityEngine.Rendering.VertexAttributeFormat.UNorm8)throw new Exception("Lightweight color is not packed: "+i);}
                        else if(z!=y)throw new Exception("Cooked food changed with lightweight flag: "+i);
                    }finally{UnityEngine.Object.DestroyImmediate(low);}
                }
                finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
            }
            Debug.Log($"MOBILE_GEOMETRY_PASS kinds={Cookbook.KindCount} fullTriangles={full} mobileTriangles={mobile} rawMobileTriangles={rawMobile} rawSmallTriangles={small}. All kinds retained; mesh tessellation only, not food count.");
        }
    }
}
