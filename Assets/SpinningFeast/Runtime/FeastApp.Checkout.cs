using UnityEngine;
using UnityEngine.UIElements;

namespace SpinningFeast
{
    public sealed partial class FeastApp
    {
        readonly ScrollView[] checkoutScroll=new ScrollView[2];
        readonly VisualElement[] checkoutDishes=new VisualElement[2];
        readonly Button[] checkoutTabs=new Button[2];
        readonly Label[] checkoutScores=new Label[2],checkoutOutcomes=new Label[2],checkoutTotals=new Label[2];
        CookedDish[] checkoutRendered;
        int checkoutPlayer,checkoutEpoch=-1;

        void InitializeCheckout()
        {
            var paper=Resources.Load<VisualTreeAsset>("FeastUI/ReceiptPaper");
            if(paper==null)throw new System.InvalidOperationException("Missing FeastUI/ReceiptPaper.uxml");
            for(int p=0;p<2;p++)
            {
                int player=p;
                var scroll=checkoutScroll[p]=Required<ScrollView>(resultUI,"checkout-scroll-"+p);
                paper.CloneTree(scroll.contentContainer);
                var tab=checkoutTabs[p]=Required<Button>(resultUI,"checkout-tab-"+p);
                tab.text=FeastPlayerNames.For(p);
                tab.clicked+=()=>ShowCheckoutPlayer(player,true);
                Required<Label>(scroll,"checkout-owner").text=tab.text;
                Required<Label>(scroll,"checkout-serial").text=p==0?"RECEIPT  /  N° 01":"RECEIPT  /  N° 02";
                checkoutScores[p]=Required<Label>(scroll,"checkout-score");
                checkoutOutcomes[p]=Required<Label>(scroll,"checkout-outcome");
                checkoutTotals[p]=Required<Label>(scroll,"checkout-total");
                checkoutDishes[p]=Required<VisualElement>(scroll,"checkout-dishes");
                scroll.focusable=true;
                scroll.RegisterCallback<KeyDownEvent>(e=>
                {
                    if(sim.State.Phase!=MatchPhase.Result)return;
                    float step=e.keyCode switch
                    {
                        KeyCode.UpArrow=>-32,KeyCode.DownArrow=>32,
                        KeyCode.PageUp=>-scroll.contentViewport.layout.height,
                        KeyCode.PageDown=>scroll.contentViewport.layout.height,
                        KeyCode.Home=>-scroll.verticalScroller.highValue,
                        KeyCode.End=>scroll.verticalScroller.highValue,
                        _=>0
                    };
                    if(step==0)return;
                    scroll.scrollOffset=new Vector2(0,Mathf.Clamp(scroll.scrollOffset.y+step,0,scroll.verticalScroller.highValue));
                    e.StopPropagation();
                },TrickleDown.TrickleDown);
            }
            ResetCheckout();
        }

        void ResetCheckout()
        {
            if(checkoutScroll[0]==null)return;
            checkoutEpoch=-1;checkoutRendered=null;checkoutPlayer=0;
            for(int p=0;p<2;p++)
            {
                checkoutScroll[p].scrollOffset=Vector2.zero;
                SetCheckoutSheetVisibility(p,p==0);
            }
        }

        void ShowCheckoutPlayer(int p,bool feedback)
        {
            if(p==checkoutPlayer || sim.State.Phase!=MatchPhase.Result)return;
            checkoutPlayer=p;
            for(int i=0;i<2;i++)SetCheckoutSheetVisibility(i,p==i);
            if(feedback)sound.Page();
        }

        void SetCheckoutSheetVisibility(int player,bool active)
        {
            // Hidden rather than display:none: the sheet retains its layout and ScrollView
            // offset between tabs. Hidden elements and their descendants aren't pickable.
            checkoutScroll[player].style.visibility=active?Visibility.Visible:Visibility.Hidden;
            checkoutScroll[player].focusable=active;
            checkoutTabs[player].EnableInClassList("selected",active);
        }

        void RefreshCheckout(MatchState state)
        {
            if(checkoutEpoch!=state.Epoch){ResetCheckout();checkoutEpoch=state.Epoch;}
            int winner=state.CyanScore==state.OrangeScore?-1:state.CyanScore>state.OrangeScore?0:1;
            string outcome=FeastPlayerNames.Outcome(winner);
            SetLabel(receiptStamp,winner<0?"引\n分":winner==0?"勝\n利":"再\n挑\n戦");
            for(int p=0;p<2;p++)
            {
                SetLabel(checkoutScores[p],(p==0?state.CyanScore:state.OrangeScore).ToString("N0"));
                SetLabel(checkoutOutcomes[p],outcome);
            }
            if(ReferenceEquals(checkoutRendered,resultDishes))return;
            checkoutRendered=resultDishes;
            for(int p=0;p<2;p++)
            {
                var dishes=checkoutDishes[p];dishes.Clear();
                int total=0,count=0;
                foreach(var entry in checkoutRendered)
                {
                    if(entry.Player!=p)continue;
                    string name=Cookbook.All[entry.Recipe].Name;
                    var row=new VisualElement {pickingMode=PickingMode.Ignore};
                    row.AddToClassList("checkout-row");
                    AddCheckoutText(row,"checkout-dish",name,"menu-display","checkout-dish");
                    AddCheckoutText(row,"checkout-count",$"{entry.Count:N0} 人前","right","checkout-count");
                    dishes.Add(row);count++;total+=entry.Count;
                }
                if(count==0)AddCheckoutText(dishes,"checkout-empty","まだ料理がありません","secondary","checkout-empty");
                SetLabel(checkoutTotals[p],$"計 {count:N0} 品 / {total:N0} 人前");
            }
        }

        static void AddCheckoutText(VisualElement parent,string name,string value,string a,string b)
        {
            var label=new Label(value){name=name,pickingMode=PickingMode.Ignore};
            label.AddToClassList(a);label.AddToClassList(b);parent.Add(label);
        }
    }
}
