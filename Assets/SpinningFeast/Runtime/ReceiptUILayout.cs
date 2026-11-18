using UnityEngine;

namespace SpinningFeast
{
    /// <summary>One node tree, two arrangements. No platform-specific game UI.</summary>
    public static class ReceiptUILayout
    {
        public struct Layout
        {
            public bool Portrait, Compact;
            public float Scale, Width, Height;
            public Rect StagePixels;
        }
        public static Layout Calculate(int width,int height,Rect safeArea,bool? portraitOverride=null)
        {
            width=Mathf.Max(1,width);height=Mathf.Max(1,height);
            float x=Mathf.Clamp(safeArea.x,0,width),y=Mathf.Clamp(safeArea.y,0,height);
            float w=Mathf.Min(safeArea.width,width-x),h=Mathf.Min(safeArea.height,height-y);
            if(w<=0 || h<=0){x=y=0;w=width;h=height;}
            bool portrait=portraitOverride ?? width<height;
            float scale=portrait?w/360f:Mathf.Min(w/1280f,h/800f);
            float logicalWidth=portrait?360:1280,logicalHeight=portrait?h/scale:800;
            return new Layout{Portrait=portrait,Compact=portrait?logicalHeight<760:w<1000,Scale=scale,
                Width=logicalWidth,Height=logicalHeight,
                StagePixels=new Rect(x+(w-logicalWidth*scale)*.5f,height-y-h+(h-logicalHeight*scale)*.5f,logicalWidth*scale,logicalHeight*scale)};
        }
        public static int PageCount(int count,int pageSize)=>Mathf.Max(1,(Mathf.Max(0,count)+pageSize-1)/pageSize);
        public static int TurnPage(int page,int delta,int pages)=>(page+delta%Mathf.Max(1,pages)+Mathf.Max(1,pages))%Mathf.Max(1,pages);
    }
}
