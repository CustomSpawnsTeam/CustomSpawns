namespace CustomSpawns.AI
{
    /**
     * Whether an invasion group has lost every town it took. Lord parties are steered by vanilla except while
     * their group holds nothing, so the AI layer needs to ask without knowing about invasions.
     */
    public interface IInvasionFootholdQuery
    {
        bool IsGroupWithoutTown(string? invasionGroupId);
    }
}
