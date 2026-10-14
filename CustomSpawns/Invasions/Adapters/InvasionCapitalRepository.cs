using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class InvasionCapitalRepository : CampaignBehaviorBase, IInvasionCapitalRepository
    {
        public const string SaveKey = "_invasionCapitalSettlementIds";
        private Dictionary<string, string> _capitalSettlementIdsByKingdomId = new(StringComparer.Ordinal);

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _capitalSettlementIdsByKingdomId);
            _capitalSettlementIdsByKingdomId = _capitalSettlementIdsByKingdomId == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(_capitalSettlementIdsByKingdomId, StringComparer.Ordinal);
        }

        public IReadOnlyList<InvasionCapital> FindAll() => _capitalSettlementIdsByKingdomId
            .Select(entry => new InvasionCapital(entry.Key, EmptyToNull(entry.Value)))
            .ToList();

        public InvasionCapital? FindByCapital(string settlementId)
        {
            foreach (KeyValuePair<string, string> entry in _capitalSettlementIdsByKingdomId)
            {
                if (string.Equals(entry.Value, settlementId, StringComparison.Ordinal))
                {
                    return new InvasionCapital(entry.Key, entry.Value);
                }
            }

            return null;
        }

        public void Register(string invaderKingdomId, string? capitalSettlementId)
        {
            if (string.IsNullOrWhiteSpace(invaderKingdomId))
            {
                throw new ArgumentException("An invader kingdom id is required.", nameof(invaderKingdomId));
            }

            if (!_capitalSettlementIdsByKingdomId.TryGetValue(invaderKingdomId, out string? existing))
            {
                _capitalSettlementIdsByKingdomId.Add(invaderKingdomId, capitalSettlementId ?? string.Empty);
                return;
            }

            // The first successful occupation is the capital. A null placeholder may be completed later,
            // but a later conquest must never move an established capital.
            if (string.IsNullOrEmpty(existing) && !string.IsNullOrEmpty(capitalSettlementId))
            {
                _capitalSettlementIdsByKingdomId[invaderKingdomId] = capitalSettlementId;
            }
        }

        private static string? EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
