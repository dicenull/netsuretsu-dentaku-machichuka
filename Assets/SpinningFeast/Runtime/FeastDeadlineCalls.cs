namespace SpinningFeast
{
    public static class FeastDeadlineCalls
    {
        // Check in urgency order: a suspended or slow frame may cross multiple cutoffs.
        public static int Crossed(float previous,float remaining,int alreadyCalled)
        {
            if(alreadyCalled<3 && previous>10 && remaining<=10)return 3;
            if(alreadyCalled<2 && previous>30 && remaining<=30)return 2;
            if(alreadyCalled<1 && previous>60 && remaining<=60)return 1;
            return 0;
        }

        public static string Text(int call)=>call switch
        {
            1=>"あと一分！",
            2=>"ラストオーダーまで30秒！",
            3=>"残り10秒、急げ！",
            _=>""
        };
    }
}
