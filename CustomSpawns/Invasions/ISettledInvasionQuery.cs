namespace CustomSpawns.Invasions
{
    // Whether an invasion has conquered its target and become one more realm of Calradia.
    public interface ISettledInvasionQuery
    {
        bool IsSettled(string? invasionClanId);
    }
}
