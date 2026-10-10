using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    /**
     * The single gate every CustomSpawns grip on a party passes through: it hands back the spawn record
     * only while the party is both one of ours and still ours to steer. Once a party is released to
     * vanilla it answers with nothing, so every grip lets go at the same moment.
     */
    internal sealed class SteeredPartyLookup
    {
        private readonly DynamicSpawnData _dynamicSpawnData;
        private readonly IPartySteeringQuery _partySteeringQuery;

        public SteeredPartyLookup(DynamicSpawnData dynamicSpawnData,
            IPartySteeringQuery partySteeringQuery)
        {
            _dynamicSpawnData = dynamicSpawnData;
            _partySteeringQuery = partySteeringQuery;
        }

        public CsPartyData? GetSteeredPartyData(MobileParty? party)
        {
            CsPartyData? partyData = _dynamicSpawnData.GetDynamicSpawnData(party);
            if (partyData is null || party is null)
            {
                return null;
            }

            return _partySteeringQuery.RequiresCustomSpawnsSteering(party) ? partyData : null;
        }
    }
}
