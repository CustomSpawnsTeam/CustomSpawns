namespace CustomSpawns.Invasions.GarrisonArmies
{
    /**
     * Which heroes are the one-time lords leading the invasions' garrison armies. The rules that treat every invasion
     * lord alike ask here first: such a lord is no lord of the realm, so he counts toward no lord cap, and the men he
     * leads out are the garrison's, so his party may hold all of them without deserting.
     */
    public interface IInvasionGarrisonArmyLeaders
    {
        bool LeadsGarrisonArmy(string? heroId);

        // The size limit of his party, his men and himself; null for any other hero.
        int? FindPartySizeLimit(string? heroId);
    }
}
