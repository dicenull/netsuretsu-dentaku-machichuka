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
            Check<Button>(title,"start","music","cpu-0","cpu-1","cpu-2");
            Check<Label>(hud,"score-0","score-1","timer");Check<TextField>(hud,"dish-input");Check<Button>(hud,"submit","book-open");Check<VisualElement>(hud,"input-dock");
            Check<Button>(result,"replay","title","result-prev","result-next");Check<Label>(result,"result-heading","result-score-0","result-score-1","result-header-0","result-header-1","result-total","result-page");
            Check<Button>(book,"book-close","book-prev","book-next");Check<Label>(book,"book-count","book-page");
            for(int i=0;i<20;i++)Check<Button>(book,"recipe-"+i);
            for(int p=0;p<2;p++)for(int i=0;i<6;i++)Check<Label>(result,$"result-name-{p}-{i}",$"result-count-{p}-{i}");
            Assert(book.Q<ScrollView>()!=null,"Book needs scrolling so all 20 entries remain reachable on small screens.");
            Assert(result.Q<ScrollView>()!=null,"Result needs a shared scrolling receipt on mobile.");
            Assert(Resources.Load<Font>("NotoSansJP-Regular")!=null,"Original Japanese font missing.");
            Assert(Resources.Load<ThemeStyleSheet>("FeastUI/Theme")!=null,"Runtime theme missing.");
            Layout(320,640,true);Layout(390,844,true);Layout(1280,800,false);
            var resized=ReceiptUILayout.Calculate(390,360,new Rect(0,0,390,360),true);
            Assert(resized.Portrait && resized.StagePixels.yMax<=360.01f,"Keyboard resize must retain portrait without exceeding the resized viewport.");
            var notched=ReceiptUILayout.Calculate(390,844,new Rect(0,34,390,763));
            Assert(Mathf.Abs(notched.StagePixels.y-47)<.01f,"Safe top conversion mismatch.");
            Assert(notched.StagePixels.yMax<=810.01f,"Safe bottom not respected.");
            // Partition every catalogue entry once; the last partial page must not drop anything.
            for(int count=0;count<=Cookbook.All.Length;count++)CheckPages(count,20);
            for(int count=0;count<=Cookbook.All.Length;count++)CheckPages(count,6);
            Assert(ReceiptUILayout.TurnPage(0,-1,20)==19,"Backward wrap failed.");
            Assert(ReceiptUILayout.TurnPage(19,1,20)==0,"Forward wrap failed.");
            Debug.Log("RECEIPT_UI_STATIC_PASS: 4 shared UXML instantiated; required controls, font/theme, responsive layout/safe area and pagination contracts passed. This is NOT a rendered UI, IME, mobile keyboard or WebGL runtime test.");
        }
        static VisualElement Load(string name)
        {
            var tree=Resources.Load<VisualTreeAsset>("FeastUI/"+name);Assert(tree!=null,"Missing "+name+" UXML");
            var root=tree.CloneTree();Assert(root.childCount>0,"Empty "+name+" UXML");return root;
        }
        static void Check<T>(VisualElement root,params string[] names) where T:VisualElement
        {foreach(string name in names){int count=0;root.Query<T>(name).ForEach(_=>count++);Assert(count==1,typeof(T).Name+" #"+name+" must occur once, got "+count);}}
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
