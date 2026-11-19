using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Reader;
using CustomSpawns.Exception;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public class CustomSpawnsDiplomacyFactionsProvider
    {
        private readonly IDataReader<Dictionary<string, Data.Model.Diplomacy>> _diplomacyDataReader;
        private readonly IClanFinder _clanFinder;

        public CustomSpawnsDiplomacyFactionsProvider(IDataReader<Dictionary<string, Data.Model.Diplomacy>> diplomacyDataReader,
            IClanFinder clanFinder)
        {
            _diplomacyDataReader = diplomacyDataReader;
            _clanFinder = clanFinder;
        }

        public IList<IFaction> GetCustomSpawnDiplomacyFactions()
        {
            IList<Clan> clans = GetConfiguredClans(out IList<string> clanIdErrors);

            if (clanIdErrors.Count > 0)
            {
                throw new TechnicalException("Could not find " + String.Join(", ", clanIdErrors) +
                                             " clan ids after the loading of all clans into the game. " +
                                             "The consequence is that the wars for these clans could not be set.");
            }
            
            return clans.Cast<IFaction>().ToList();
        }

        public IList<Clan> GetConfiguredClans(out IList<string> missingClanIds)
        {
            missingClanIds = new List<string>();
            IList<Clan> clans = new List<Clan>();
            foreach (KeyValuePair<string, Data.Model.Diplomacy> clanData in _diplomacyDataReader.Data)
            {
                Clan? clan = _clanFinder.FindClan(clanData.Key);
                if (clan == null)
                {
                    missingClanIds.Add(clanData.Key);
                    continue;
                }

                clans.Add(clan);
            }

            return clans;
        }
    }
}
