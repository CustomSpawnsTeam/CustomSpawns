using System.Collections.Generic;
using CustomSpawns.Invasions.Alliances;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public enum DiplomacyReconciliationAction
    {
        None,
        DeclareWar,
        MakePeace
    }

    public class CustomSpawnsDiplomacyReconciliationProvider
    {
        private readonly CustomSpawnsClanDiplomacyProvider _clanDiplomacyProvider;

        public CustomSpawnsDiplomacyReconciliationProvider(CustomSpawnsClanDiplomacyProvider clanDiplomacyProvider)
        {
            _clanDiplomacyProvider = clanDiplomacyProvider;
        }

        public DiplomacyReconciliationAction GetAction(IFaction policyClan, IFaction targetClan, bool isAtWar)
        {
            DiplomacyReconciliationAction desiredAction = GetDesiredAction(policyClan, targetClan);
            if (desiredAction == DiplomacyReconciliationAction.MakePeace)
            {
                return isAtWar ? DiplomacyReconciliationAction.MakePeace : DiplomacyReconciliationAction.None;
            }

            if (desiredAction == DiplomacyReconciliationAction.DeclareWar)
            {
                return isAtWar ? DiplomacyReconciliationAction.None : DiplomacyReconciliationAction.DeclareWar;
            }

            return DiplomacyReconciliationAction.None;
        }

        public DiplomacyReconciliationAction GetDesiredAction(IFaction policyClan, IFaction targetClan)
        {
            if (_clanDiplomacyProvider.IsPeaceDeclarationPossible(policyClan, targetClan))
            {
                return DiplomacyReconciliationAction.MakePeace;
            }

            return _clanDiplomacyProvider.IsWarDeclarationPossible(policyClan, targetClan)
                ? DiplomacyReconciliationAction.DeclareWar
                : DiplomacyReconciliationAction.None;
        }

        // A conquest's stance outranks the configured policy: its target stays at war, its ally at peace, and
        // other kingdoms decide.
        public DiplomacyReconciliationAction ApplyStance(DiplomacyReconciliationAction action,
            InvasionWarStance stance)
        {
            if (stance == InvasionWarStance.War)
            {
                return DiplomacyReconciliationAction.DeclareWar;
            }

            if (stance == InvasionWarStance.Peace)
            {
                return DiplomacyReconciliationAction.MakePeace;
            }

            return stance == InvasionWarStance.Open ? DiplomacyReconciliationAction.None : action;
        }

        public static DiplomacyReconciliationAction Aggregate(IList<DiplomacyReconciliationAction> actions,
            out bool hasConflict)
        {
            bool requiresWar = actions.Contains(DiplomacyReconciliationAction.DeclareWar);
            bool requiresPeace = actions.Contains(DiplomacyReconciliationAction.MakePeace);
            hasConflict = requiresWar && requiresPeace;

            // Constant-war policies take precedence so an applicable war rule cannot be silently cancelled.
            return requiresWar
                ? DiplomacyReconciliationAction.DeclareWar
                : requiresPeace
                    ? DiplomacyReconciliationAction.MakePeace
                    : DiplomacyReconciliationAction.None;
        }
    }
}
