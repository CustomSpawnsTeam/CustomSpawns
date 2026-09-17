using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    /**
     * Where an invasion is allowed to land, and which parties land together. Siege targeting restricts and
     * ranks settlements through this without knowing what an invasion is.
     */
    internal interface IInvasionLandingQuery
    {
        // The towns a party may take while its invasion holds no coastal town; null once it may march inland.
        Func<Settlement, bool>? GetInitialCoastalTargetFilter(MobileParty party);

        // The parties that land together, so one town can be the nearest for the invasion as a whole.
        IReadOnlyList<MobileParty> GetLandingParties(MobileParty party);
    }
}
