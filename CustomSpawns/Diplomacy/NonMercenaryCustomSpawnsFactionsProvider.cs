using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Reader;

namespace CustomSpawns.Diplomacy
{
    public class NonMercenaryCustomSpawnsFactionsProvider
    {
        private readonly IDataReader<Dictionary<string,Data.Model.Diplomacy>> _dataReader;

        public NonMercenaryCustomSpawnsFactionsProvider(
            IDataReader<Dictionary<string, Data.Model.Diplomacy>> dataReader)
        {
            _dataReader = dataReader;
        }

        public IList<string> GetNonMercenaryCustomSpawnsFactions()
        {
            return _dataReader.Data.Where(diplomacy => diplomacy.Value != null && diplomacy.Value.ForceNoKingdom).Select(diplomacy => diplomacy.Key).ToList();
        }
    }
}