using System;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace CustomSpawns.Spawn.PartySize
{
    /**
     * Resizes the party size limit of a custom spawns party to its initial spawn party size.
     * 
     * By default, all parties have a party size limit of 20. This causes issues as a speed penalty
     * is applied to parties that exceed their party size limit.
     *
     * In order to address this issue, this class will resize the party size when it is first calculated.
     * This applies at spawn time and on save load.
     */
    public class PartySizeResizerOnFirstCalculation : IPartySizeLimitObserver
    {
        private static SpawnDao? _spawnDao;
        private static readonly TextObject ResizeInitialPartySizeText = new ("CustomSpawns: Resize initial party size");
        
        /**
         * @param spawnDao the repository for all spawns
         */
        public PartySizeResizerOnFirstCalculation(SpawnDao spawnDao)
        {
            _spawnDao = spawnDao;
        }

        /**
         * Resizes the party size limit of a custom spawns party to its initial spawn party size.
         */
        public ExplainedNumber OnPartySizeLimitCalculated(MobileParty mobileParty, ExplainedNumber explainedPartySizeLimit)
        {
            if (IsCustomSpawnsParty(mobileParty) && !IsInitialPartySizeResized(explainedPartySizeLimit))
            {
                int partySizeLimit = (int) explainedPartySizeLimit.ResultNumber;
                int partySize = mobileParty.Party.NumberOfAllMembers;
                int oversizePartySize = partySize - partySizeLimit;
                explainedPartySizeLimit.Add(Math.Max(oversizePartySize, 0), ResizeInitialPartySizeText);
            }
            return explainedPartySizeLimit;
        }

        private static bool IsCustomSpawnsParty(MobileParty mobileParty)
        {
            string isolatedPartyStringId = CampaignUtils.IsolateMobilePartyStringID(mobileParty);
            return _spawnDao!.FindAllPartyTemplateId().Any(spawnId => spawnId.Equals(isolatedPartyStringId))
                   || _spawnDao.FindAllSubPartyTemplateId().Any(spawnId => spawnId.Equals(isolatedPartyStringId));
        }
        
        private static bool IsInitialPartySizeResized(ExplainedNumber explainedPartySize)
        {
            return explainedPartySize
                .GetLines()
                .Any(line => line.name.Contains(explainedPartySize.ToString()));
        }
    }
}