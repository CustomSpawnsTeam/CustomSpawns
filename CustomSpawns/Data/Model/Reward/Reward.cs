namespace CustomSpawns.Data.Model.Reward
{
    public class Reward
    {
        public RewardType Type { get; set; }
        public string? ItemId { get; set; }
        public int? RenownInfluenceMoneyAmount { get; set; }
        public float? Chance { get; set; }
    }
}