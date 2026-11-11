using System.Collections.Generic;

namespace CustomSpawns.Invasions
{
    // The campaign as the invasions' garrison armies see it, each invasion named by its founding clan.
    public interface IInvasionGarrisonArmyWorld
    {
        // The invasions still being fought.
        IReadOnlyList<string> FindInvasionClanIds();

        // Null when the invasion never raises an army out of its capital's garrison.
        InvasionGarrisonArmyRules? FindRules(string invasionClanId);

        // Null while the invasion does not hold its capital.
        InvasionGarrisonArmyFacts? Observe(string invasionClanId);

        // Vanilla's fewest days of food an army must carry before it is called.
        float FindMinimumFoodDays();

        // How many men eat one food a day.
        int FindMenPerFoodPerDay();

        // A new lord of the clan holding the capital, standing in it with no party yet; null when none can be made.
        string? CreateLord(string invasionClanId);

        /**
         * Gives the lord a party of so many of the garrison's men and so much food, and makes him the leader of a
         * besieger army that calls the realm's free lords. False when the campaign refused the army.
         */
        bool Raise(string invasionClanId, string heroId, int men, int food);

        void ReportSkip(string invasionClanId, string reason);

        // Whether the lord's army is over: he is beaten, captured or gone, or vanilla broke his army up.
        bool HasEnded(string heroId);

        // Whether the lords called to his army have stopped gathering and it has set out.
        bool HasGathered(string heroId);

        void ReportGathered(string invasionClanId, string heroId);

        // Sends the lord's surviving men back into a garrison and removes him for good.
        void End(string invasionClanId, string heroId);
    }
}
