using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpinningFeast.Editor
{
    /// <summary>Batch-safe asset/contract checks. Does not enter Play mode, build or publish.</summary>
    public static class ReceiptUITests
    {
        [MenuItem("Spinning Feast/Validate Receipt UI")]
        public static void ValidateReceiptUI()
        {
            var title=Load("Title");var hud=Load("MatchHUD");var result=Load("Result");var book=Load("Book");
            Check<Button>(title,"start","cpu-0","cpu-1","cpu-2");
            var titleRoot=title.Q<VisualElement>("title-screen");
            Check<VisualElement>(titleRoot,"title-masthead","title-order-sheet");
            Assert(titleRoot.childCount==4 && titleRoot.Q<VisualElement>("title-hero")==null,
                "Title should only have masthead, order slip, gear, and its hidden modal.");
            Assert(titleRoot.Q<VisualElement>("title-order-sheet").Q<Button>("start")!=null,
                "Start belongs inside the order slip.");
            Assert(title.Q<Label>("cpu-heading").text==FeastPlayerNames.OpponentStrength,"Title player wording must match the shared lexicon.");
            for(int p=0;p<2;p++)
            {
                string expected=FeastPlayerNames.For(p);
                Assert(hud.Q<Label>("hud-static-"+(p*2)).text==expected &&
                    result.Q<Button>("checkout-tab-"+p).text==expected,"HUD and checkout authoring labels must match runtime.");
            }
            foreach(var screen in new[]{title,hud})
            {
                Check<Button>(screen,"settings-open","settings-audio","settings-close");
                Check<VisualElement>(screen,"settings-panel");
            }
            Check<Label>(hud,"score-0","score-1","timer","ordered-stamp","deadline-call");Check<TextField>(hud,"dish-input");Check<Button>(hud,"submit","book-open");Check<VisualElement>(hud,"input-dock");
            Check<VisualElement>(result,"cashier-feed");
            Check<Button>(result,"replay","title","checkout-tab-0","checkout-tab-1");
            var paper=Load("ReceiptPaper");
            Check<Label>(paper,"checkout-serial","checkout-owner","checkout-score","checkout-outcome","checkout-total");
            Check<VisualElement>(paper,"checkout-dishes");
            Check<Button>(book,"book-close","book-prev","book-next");Check<Label>(book,"book-count","book-page");
            for(int i=0;i<20;i++)Check<Button>(book,"recipe-"+i);
            Assert(book.Q<ScrollView>()!=null,"Book needs scrolling so all 20 entries remain reachable on small screens.");
            for(int p=0;p<2;p++)
            {
                var scroll=result.Q<ScrollView>("checkout-scroll-"+p);
                Assert(scroll!=null && scroll.verticalScrollerVisibility==ScrollerVisibility.Hidden &&
                    scroll.horizontalScrollerVisibility==ScrollerVisibility.Hidden,"Each full receipt needs a hidden scrollbar.");
            }
            Assert(Resources.Load<Font>("NotoSansJP-Regular")!=null,"Original Japanese font missing.");
            var displayFont=Resources.Load<Font>("FeastUI/ReggaeOne-Regular");
            Assert(displayFont!=null,"Japanese menu display font missing.");
            foreach(var tree in new[]{title,hud,result,book,paper})
                tree.Query<TextElement>(className:"menu-display").ForEach(e=>CheckDisplayGlyphs(displayFont,e.text));
            foreach(var recipe in Cookbook.All)CheckDisplayGlyphs(displayFont,recipe.Name);
            CheckDisplayGlyphs(displayFont,"勝利再挑戦引分特製");
            for(int i=1;i<=3;i++)CheckDisplayGlyphs(displayFont,FeastDeadlineCalls.Text(i));
            Assert(FeastDeadlineCalls.Crossed(60.1f,60,0)==1,"The one-minute call should trigger on equality.");
            Assert(FeastDeadlineCalls.Crossed(61,59,0)==1,"Time steps may skip the exact threshold.");
            Assert(FeastDeadlineCalls.Crossed(59,29,1)==2,"The 30-second call should supersede the previous call.");
            Assert(FeastDeadlineCalls.Crossed(31,9,1)==3,"Skipped cutoffs should select the most urgent call.");
            Assert(FeastDeadlineCalls.Crossed(31,29,2)==0 && FeastDeadlineCalls.Crossed(10,9,3)==0,"A call must not repeat.");
            CameraFrame(320,640,new Rect(0,0,320,640));
            CameraFrame(1080,2400,new Rect(15,95,1035,2200));
            Assert(Mathf.Abs(FeastPortraitFrame.VisibleBottom(2400,2400,900)-1500)<.01f,"Overlay IME should reserve its measured height.");
            Assert(Mathf.Abs(FeastPortraitFrame.VisibleBottom(1500,2400,900)-1500)<.01f,"A fully resized Unity viewport must not subtract the IME twice.");
            Assert(Mathf.Abs(FeastPortraitFrame.VisibleBottom(2000,2400,900)-1500)<.01f,"Only the unconsumed part of a partial resize should be reserved.");
            var safe= new Rect(15,95,1035,2200);
            Vector3 baseline=CameraFrame(1080,2400,safe,2400);
            Vector3 resizedCamera=CameraFrame(1080,1500,safe,2400);
            Assert((baseline-resizedCamera).sqrMagnitude<.0001f,"Changing only surface height must keep top-down table position and width.");
            Assert(!hud.Q<TextField>("dish-input").ClassListContains("menu-display"),"Editable input should keep the body font.");
            Assert(Resources.Load<ThemeStyleSheet>("FeastUI/Theme")!=null,"Runtime theme missing.");
            Layout(320,640,true);Layout(390,844,true);Layout(1280,800,false);
            var resized=ReceiptUILayout.Calculate(390,360,new Rect(0,0,390,360),true);
            Assert(resized.Portrait && resized.StagePixels.yMax<=360.01f,"Keyboard resize must retain portrait without exceeding the resized viewport.");
            var notched=ReceiptUILayout.Calculate(390,844,new Rect(0,34,390,763));
            Assert(Mathf.Abs(notched.StagePixels.y-47)<.01f,"Safe top conversion mismatch.");
            Assert(notched.StagePixels.yMax<=810.01f,"Safe bottom not respected.");
            // Partition every catalogue entry once; the last partial page must not drop anything.
            for(int count=0;count<=Cookbook.All.Length;count++)CheckPages(count,20);
            Assert(ReceiptUILayout.TurnPage(0,-1,20)==19,"Backward wrap failed.");
            Assert(ReceiptUILayout.TurnPage(19,1,20)==0,"Forward wrap failed.");
            Debug.Log("RECEIPT_UI_STATIC_PASS: 4 shared screens plus the checkout paper instantiated; required controls, font/theme, responsive layout/safe area and pagination contracts passed. This is NOT a rendered UI, IME, mobile keyboard or WebGL runtime test.");
        }
        static VisualElement Load(string name)
        {
            var tree=Resources.Load<VisualTreeAsset>("FeastUI/"+name);Assert(tree!=null,"Missing "+name+" UXML");
            var root=tree.CloneTree();Assert(root.childCount>0,"Empty "+name+" UXML");return root;
        }
        static void Check<T>(VisualElement root,params string[] names) where T:VisualElement
        {foreach(string name in names){int count=0;root.Query<T>(name).ForEach(_=>count++);Assert(count==1,typeof(T).Name+" #"+name+" must occur once, got "+count);}}
        static Vector3 CameraFrame(int width,int height,Rect safe,int layoutHeight=0)
        {
            if(layoutHeight==0)layoutHeight=height;
            const float r=FeastPortraitFrame.TableRadius;
            float size=FeastPortraitFrame.FrameTable(width,height,layoutHeight,safe,12.5f,out Vector2 shift);
            float ppu=height/(2*size),centerX=width*.5f-shift.x*ppu,centerY=height*.5f+shift.y*ppu;
            Assert(centerX-r*ppu>=safe.xMin-.01f && centerX+r*ppu<=safe.xMax+.01f &&
                2*r*ppu>=safe.width*.93f,"Full tabletop should use nearly all safe width.");
            Assert(centerY<layoutHeight*.46f && float.IsFinite(size),"Portrait table should stay high.");
            return new Vector3(centerX,centerY,ppu);
        }
        static void CheckDisplayGlyphs(Font displayFont,string label)
        {
            foreach(char c in label??"")
                if(!char.IsWhiteSpace(c))Assert(displayFont.HasCharacter(c),$"Missing menu display glyph {c} (U+{(int)c:X4}).");
        }
        static void Layout(int w,int h,bool portrait)
        {
            var l=ReceiptUILayout.Calculate(w,h,new Rect(0,0,w,h));Assert(l.Portrait==portrait,"Orientation "+w+"x"+h);
            Assert(l.Scale>0 && float.IsFinite(l.Scale),"Invalid scale");
            Assert(l.StagePixels.x>=-.01f && l.StagePixels.y>=-.01f && l.StagePixels.xMax<=w+.01f && l.StagePixels.yMax<=h+.01f,"Stage exceeds viewport");
        }
        static void CheckPages(int count,int size)
        {
            int pages=ReceiptUILayout.PageCount(count,size),seen=0;
            for(int page=0;page<pages;page++)for(int slot=0;slot<size;slot++)if(page*size+slot<count)seen++;
            Assert(seen==count,"Page coverage "+count+"/"+size);Assert(pages>=1,"Empty page contract");
        }
        static void Assert(bool condition,string message){if(!condition)throw new InvalidOperationException("Receipt UI validation: "+message);}
    }
}
