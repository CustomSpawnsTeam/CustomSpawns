using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Conquest.Adapters;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Lines.Adapters
{
    // The standing realms of an invasion line and the threat they make together.
    public sealed class BannerlordInvasionLines
    {
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;
        private readonly InvasionConquestFronts _invasionConquestFronts;

        public BannerlordInvasionLines(BannerlordInvasionRealms bannerlordInvasionRealms,
            InvasionConquests invasionConquests, InvasionConquestFronts invasionConquestFronts)
        {
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
            _invasionConquestFronts = invasionConquestFronts;
        }

        // Every standing realm of the conquest's line, the given one among them.
        public List<IFaction> FindRealms(InvasionConquest conquest, IFaction realm) =>
            FindRealms(conquest).Append(realm).Distinct().ToList();

        public List<IFaction> FindRealms(InvasionConquest conquest) =>
            _invasionConquests.FindAllies(conquest).Append(conquest).Select(_bannerlordInvasionRealms.FindRealm)
                .OfType<IFaction>().Distinct().ToList();

        public float FindThreat(InvasionConquestRules rules, IReadOnlyCollection<IFaction> realms) =>
            _invasionConquestFronts.FindThreat(rules, realms.Sum(realm => realm.Fiefs.Count),
                realms.Sum(realm => Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(realm)));
    }
}
