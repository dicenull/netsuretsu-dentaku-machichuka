using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SpinningFeast
{
    /// <summary>Compose the table at full width in portrait, independently of HUD and IME height.</summary>
    public static class FeastPortraitFrame
    {
        public const float TableRadius=ArenaGeometry.TableRadius+.125f; // lacquer rim
        public const float ArenaExtent=ArenaGeometry.BounceSphereRadius+3f; // clip depth only

        public static float FrameTable(int width,int height,int layoutHeight,Rect safeArea,
            float projectedTableHalfHeight,out Vector2 cameraShift)
        {
            float left=Mathf.Max(0,safeArea.xMin),right=Mathf.Min(width,safeArea.xMax);
            float top=Mathf.Max(0,layoutHeight-safeArea.yMax),bottom=Mathf.Min(layoutHeight,layoutHeight-safeArea.yMin);
            if(right<=left || bottom<=top){left=0;right=width;top=0;bottom=layoutHeight;}
            float safeWidth=right-left;
            // 95% of safe width, whether the keyboard overlays the surface or physically
            // resizes it. Keep the same screen-pixels-per-world-unit as height changes.
            float pixelsPerUnit=safeWidth*.95f/(2*TableRadius);
            float worldHalfHeight=height/(2*pixelsPerUnit);
            float tableHalfPixels=projectedTableHalfHeight*pixelsPerUnit;
            float centerY=top+(bottom-top)*.38f;
            centerY=Mathf.Max(top+tableHalfPixels+2,centerY);
            var center=new Vector2((left+right)*.5f,height-centerY);
            cameraShift=(new Vector2(width,height)*.5f-center)/pixelsPerUnit;
            return worldHalfHeight;
        }

        // Unity can already have lost part (or all) of the keyboard height to adjustResize.
        public static float VisibleBottom(int height,int unobscuredHeight,float keyboardHeight)
        {
            float resized=Mathf.Max(0,unobscuredHeight-height);
            return Mathf.Clamp(height-Mathf.Max(0,keyboardHeight-resized),1,height);
        }

    }
    /// <summary>One frame sample shared by layout and rendering, regardless of script order.</summary>
    internal static class FeastKeyboardViewport
    {
        internal struct Sample {public bool Keyboard,Portrait;public float VisibleBottom;public int LayoutHeight;public Rect SafeArea;}
        static Sample current;
        static int frame=-1,fullWidth,fullHeight;
        static bool lastPortrait;
        static Rect fullSafeArea;
        // Set only by the explicit graphical QA coroutine. Never used in normal play.
        static float? smokeVisibleBottom;
        internal static void SetSmokeKeyboard(float? visibleBottom){smokeVisibleBottom=visibleBottom;frame=-1;}
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern float FeastWebInput_VisibleBottom();
        [DllImport("__Internal")] static extern int FeastWebInput_Orientation();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        static float pollAt;
        static int imeMethod,windowHeight;
        static bool nativeUnavailable;
        static float inset;

        // Root window insets use Android pixels; scale them to the current Unity surface.
        // Polling is capped; portrait drawing and HUD never perform separate native queries.
        static void PollInsets()
        {
            float now=Time.unscaledTime;
            if(nativeUnavailable || now<pollAt)return;
            pollAt=now+.05f;
            try
            {
                if(imeMethod==0)
                {
                    using(var version=new AndroidJavaClass("android.os.Build$VERSION"))
                        if(version.GetStatic<int>("SDK_INT")<30){nativeUnavailable=true;return;}
                    using(var type=new AndroidJavaClass("android.view.WindowInsets$Type"))
                        imeMethod=type.CallStatic<int>("ime");
                }
                using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if(activity==null)return;
                    using(var window=activity.Call<AndroidJavaObject>("getWindow"))
                    using(var decor=window.Call<AndroidJavaObject>("getDecorView"))
                    using(var insets=decor.Call<AndroidJavaObject>("getRootWindowInsets"))
                    {
                        if(insets==null)return;
                        if(!insets.Call<bool>("isVisible",imeMethod)){inset=0;windowHeight=0;return;}
                        using(var manager=activity.Call<AndroidJavaObject>("getWindowManager"))
                        using(var metrics=manager.Call<AndroidJavaObject>("getCurrentWindowMetrics"))
                        using(var bounds=metrics.Call<AndroidJavaObject>("getBounds"))
                        using(var padding=insets.Call<AndroidJavaObject>("getInsets",imeMethod))
                        {
                            int w=bounds.Call<int>("width"),h=bounds.Call<int>("height");
                            float scale=Screen.width/(float)Mathf.Max(1,w);
                            inset=Mathf.Max(0,padding.Get<int>("bottom")*scale);
                            windowHeight=Mathf.RoundToInt(h*scale);
                        }
                    }
                }
            }
            catch(System.Exception)
            {
                nativeUnavailable=true;inset=0;windowHeight=0;
                // The same path still handles Unity keyboard bounds and adjustResize.
            }
        }
#endif
        internal static Sample Read()
        {
            if(frame==Time.frameCount)return current;
            frame=Time.frameCount;
            int w=Mathf.Max(1,Screen.width),h=Mathf.Max(1,Screen.height);
            bool keyboard,portrait;
            float visibleBottom=h;
#if UNITY_WEBGL && !UNITY_EDITOR
            int orientation=FeastWebInput_Orientation();
            visibleBottom=Mathf.Clamp01(FeastWebInput_VisibleBottom())*h;
            keyboard=visibleBottom<h-40;
            portrait=orientation>=0?orientation==1:keyboard && fullWidth>0?lastPortrait:w<h;
#else
            keyboard=TouchScreenKeyboard.visible;
            float keyboardHeight=keyboard?TouchScreenKeyboard.area.height:0;
            int nativeHeight=0;
#if UNITY_ANDROID && !UNITY_EDITOR
            PollInsets();
            keyboard|=inset>0;
            keyboardHeight=Mathf.Max(keyboardHeight,inset);
            nativeHeight=windowHeight;
#endif
            bool sameWidth=fullWidth>0 && Mathf.Abs(w-fullWidth)<=fullWidth*.2f;
            portrait=keyboard && sameWidth?lastPortrait:w<h;
            if(keyboard)
            {
                int unobscured=nativeHeight>=h?nativeHeight:sameWidth?fullHeight:h;
                visibleBottom=FeastPortraitFrame.VisibleBottom(h,unobscured,keyboardHeight);
            }
#endif
            if(smokeVisibleBottom.HasValue)
            {keyboard=true;visibleBottom=Mathf.Clamp(smokeVisibleBottom.Value,1,h);}
            var safe=Screen.safeArea;
            if(!keyboard || fullWidth==0 || Mathf.Abs(w-fullWidth)>fullWidth*.2f)
            {fullWidth=w;fullHeight=h;lastPortrait=portrait;fullSafeArea=safe;}
            // Even on an Android variant that shrinks the Unity surface, retain the
            // same top-down game and HUD coordinates; let the keyboard crop at bottom.
            current=new Sample{Keyboard=keyboard,Portrait=portrait,VisibleBottom=visibleBottom,
                LayoutHeight=keyboard?fullHeight:h,SafeArea=keyboard?fullSafeArea:safe};
            return current;
        }
    }

}
