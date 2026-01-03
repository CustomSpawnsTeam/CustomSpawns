using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Reader.Impl;
using CustomSpawns.Exception;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public class CustomSpawnsDiplomacyFactionsProvider
    {
        private readonly DiplomacyDataReader _diplomacyDataReader;

        public CustomSpawnsDiplomacyFactionsProvider(DiplomacyDataReader diplomacyDataReader)
        {
            _diplomacyDataReader = diplomacyDataReader;
        }

        public IList<IFaction> GetCustomSpawnDiplomacyFactions()
        {
            List<string> clanIdErrors = new();
            IList<IFaction> clanDiplomacy = new List<IFaction>();
            IDictionary<string,Data.Model.Diplomacy> diplomacy = _diplomacyDataReader.Data;
            foreach (KeyValuePair<string,Data.Model.Diplomacy> clanData in diplomacy)
            {
                if (!Clan.All.Any(clan => clan.StringId == clanData.Key))
                {
                    clanIdErrors.Add(clanData.Key);
                }

                IFaction clan = Clan.All.First(clan1 => clan1.StringId == clanData.Key);
                clanDiplomacy.Add(clan);
            }

            if (clanIdErrors.Count > 0)
            {
                throw new TechnicalException("Could not find " + String.Join(", ", clanIdErrors) +
                                             " clan ids after the loading of all clans into the game. " +
                                             "The consequence is that the wars for these clans could not be set.");
            }
            
            return clanDiplomacy;
        }
    }
}