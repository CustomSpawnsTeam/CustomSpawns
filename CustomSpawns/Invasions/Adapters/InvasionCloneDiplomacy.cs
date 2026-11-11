using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Gives a copy of an invasion the diplomacy its model clan is configured with. Diplomacy is configured per
     * clan id, and a copy's clan is founded in the campaign, so without this it would lack the forced wars and the
     * refusal to join a kingdom its model has. The configuration is not saved, so every copy inherits again on load.
     */
    public sealed class InvasionCloneDiplomacy
    {
        private readonly IDictionary<string, Data.Model.Diplomacy> _diplomacy;

        public InvasionCloneDiplomacy(IDictionary<string, Data.Model.Diplomacy> diplomacy)
        {
            _diplomacy = diplomacy;
        }

        // A copy that already has diplomacy of its own keeps it.
        public void Inherit(string cloneClanId, string modelClanId)
        {
            if (_diplomacy.ContainsKey(cloneClanId) ||
                !_diplomacy.TryGetValue(modelClanId, out Data.Model.Diplomacy? model) || model == null)
            {
                return;
            }

            _diplomacy[cloneClanId] = new Data.Model.Diplomacy
            {
                clanString = cloneClanId,
                ForceNoKingdom = model.ForceNoKingdom,
                ForcedWarPeaceDataInstance = model.ForcedWarPeaceDataInstance == null
                    ? null
                    : new Data.Model.Diplomacy.ForcedWarPeaceData
                    {
                        AtPeaceWithClans = model.ForcedWarPeaceDataInstance.AtPeaceWithClans.ToList(),
                        ExceptionKingdoms = model.ForcedWarPeaceDataInstance.ExceptionKingdoms.ToList()
                    }
            };
        }

        public void InheritAll(InvasionLineClans invasionLineClans)
        {
            foreach ((string cloneClanId, string modelClanId) in invasionLineClans.All.ToList())
            {
                Inherit(cloneClanId, modelClanId);
            }
        }
    }
}
