namespace SpinningFeast
{
    public static class FeastPlayerNames
    {
        public const string Self="自分",Opponent="相手";
        public const string OpponentStrength=Opponent+"の強さ";
        public static string For(int player)=>player==0?Self:Opponent;
        public static string Outcome(int winner)=>winner<0?"引き分け":For(winner)+"の勝ち！";
    }
}
