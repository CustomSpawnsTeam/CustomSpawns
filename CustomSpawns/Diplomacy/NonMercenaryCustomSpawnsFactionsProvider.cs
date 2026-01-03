using System.Collections.Generic;
using System.Linq;
using CustomSpawns.CampaignData.Implementations;
using CustomSpawns.Data.Reader;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public class NonMercenaryCustomSpawnsFactionsProvider
    {
        private readonly DailyLogger _dailyLogger;
        private readonly IDataReader<Dictionary<string,Data.Model.Diplomacy>> _dataReader;

        public NonMercenaryCustomSpawnsFactionsProvider(IDataReader<Dictionary<string,Data.Model.Diplomacy>> dataReader, DailyLogger dailyLogger)
        {
            _dataReader = dataReader;
            _dailyLogger = dailyLogger;
        }

        public IList<string> GetNonMercenaryCustomSpawnsFactions()
        {
            return _dataReader.Data.Where(diplomacy => diplomacy.Value != null && diplomacy.Value.ForceNoKingdom).Select(diplomacy => diplomacy.Key).ToList();
        }
    }
}