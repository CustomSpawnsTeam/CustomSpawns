namespace CustomSpawns.Invasions
{
    // What the campaign says about an invasion's capital on a day it may raise a garrison army.
    public readonly struct InvasionGarrisonArmyFacts
    {
        public InvasionGarrisonArmyFacts(bool isCapitalBesieged, int garrisonMen, bool hasTarget)
        {
            IsCapitalBesieged = isCapitalBesieged;
            GarrisonMen = garrisonMen;
            HasTarget = hasTarget;
        }

        public bool IsCapitalBesieged { get; }

        // The garrison's men, its heroes left out.
        public int GarrisonMen { get; }

        // Whether the conquered kingdom still holds a fortification the army can reach.
        public bool HasTarget { get; }
    }
}
