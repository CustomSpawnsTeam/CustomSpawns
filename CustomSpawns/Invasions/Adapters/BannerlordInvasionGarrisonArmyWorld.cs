using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Invasions.Adapters
{
    // The invasions' garrison armies and their one-time lords on the campaign map.
    public sealed class BannerlordInvasionGarrisonArmyWorld : IInvasionGarrisonArmyWorld
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionGarrisonArmyConfigurations _bannerlordInvasionGarrisonArmyConfigurations;
        private readonly BannerlordInvasionGarrisonArmyLordParties _bannerlordInvasionGarrisonArmyLordParties;
        private readonly BannerlordInvasionGarrisonArmyTargets _bannerlordInvasionGarrisonArmyTargets;
        private readonly IInvasionCommissariat _invasionCommissariat;
        private readonly IInvasionGarrisonArmyLeaders _invasionGarrisonArmyLeaders;
        private readonly Dictionary<string, Army.ArmyDispersionReason> _dispersalReasons = new();

        public BannerlordInvasionGarrisonArmyWorld(Func<InvasionState> state,
            BannerlordInvasionGarrisonArmyConfigurations bannerlordInvasionGarrisonArmyConfigurations,
            BannerlordInvasionGarrisonArmyLordParties bannerlordInvasionGarrisonArmyLordParties,
            BannerlordInvasionGarrisonArmyTargets bannerlordInvasionGarrisonArmyTargets,
            IInvasionCommissariat invasionCommissariat, IInvasionGarrisonArmyLeaders invasionGarrisonArmyLeaders)
        {
            _state = state;
            _bannerlordInvasionGarrisonArmyConfigurations = bannerlordInvasionGarrisonArmyConfigurations;
            _bannerlordInvasionGarrisonArmyLordParties = bannerlordInvasionGarrisonArmyLordParties;
            _bannerlordInvasionGarrisonArmyTargets = bannerlordInvasionGarrisonArmyTargets;
            _invasionCommissariat = invasionCommissariat;
            _invasionGarrisonArmyLeaders = invasionGarrisonArmyLeaders;
        }

        public IReadOnlyList<string> FindInvasionClanIds() => _state().Invasions
            .Where(invasion => _state().FindFightingInvasion(invasion.ClanId) == invasion)
            .Select(invasion => invasion.ClanId).ToList();

        public InvasionGarrisonArmyRules? FindRules(string invasionClanId)
        {
            Invasion? invasion = _state().FindInvasion(invasionClanId);

            return invasion == null ? null : _bannerlordInvasionGarrisonArmyConfigurations.FindRules(invasion.SpawnTemplateId);
        }

        public InvasionGarrisonArmyFacts? Observe(string invasionClanId)
        {
            Invasion? invasion = _state().FindInvasion(invasionClanId);
            Settlement? capital = invasion == null ? null : _bannerlordInvasionGarrisonArmyTargets.FindHeldCapital(invasion);

            return capital == null
                ? null
                : new InvasionGarrisonArmyFacts(capital.IsUnderSiege, capital.Town.GarrisonParty.MemberRoster.TotalRegulars,
                    _bannerlordInvasionGarrisonArmyTargets.FindTarget(invasion!, capital.GatePosition) != null);
        }

        public float FindMinimumFoodDays() => _invasionCommissariat.FindMinimumFoodDaysForArmy();

        public int FindMenPerFoodPerDay() => _invasionCommissariat.FindMenPerFoodPerDay();

        public string? CreateLord(string invasionClanId)
        {
            Invasion? invasion = _state().FindInvasion(invasionClanId);
            Settlement? capital = invasion == null ? null : _bannerlordInvasionGarrisonArmyTargets.FindHeldCapital(invasion);

            return capital == null ? null : _bannerlordInvasionGarrisonArmyLordParties.CreateLord(capital)?.StringId;
        }

        public bool Raise(string invasionClanId, string heroId, int men, int food)
        {
            Invasion invasion = _state().FindInvasion(invasionClanId)!;
            Settlement capital = _bannerlordInvasionGarrisonArmyTargets.FindHeldCapital(invasion)!;
            Settlement target = _bannerlordInvasionGarrisonArmyTargets.FindTarget(invasion, capital.GatePosition)!;
            Hero lord = FindHero(heroId)!;
            MobileParty party = _bannerlordInvasionGarrisonArmyLordParties.Field(lord, capital, men, food);
            MBList<MobileParty> called = _bannerlordInvasionGarrisonArmyTargets.FindFreeLords(invasion, party);

            if (party.MapFaction is Kingdom kingdom)
            {
                kingdom.CreateArmy(lord, target, Army.ArmyTypes.Besieger, called);
            }

            if (party.Army == null)
            {
                ReportSkip(invasionClanId, "army_refused");
                return false;
            }

            Logger.Info("Invasion garrison army: raised spawn=" + invasion.SpawnTemplateId +
                        " capital=" + capital.StringId + " leader=" + lord.StringId +
                        " men=" + party.MemberRoster.TotalRegulars + " called=" + called.Count +
                        " garrison_left=" + capital.Town.GarrisonParty.MemberRoster.TotalRegulars +
                        " target=" + target.StringId);
            return true;
        }

        public void ReportSkip(string invasionClanId, string reason)
        {
            Invasion? invasion = _state().FindInvasion(invasionClanId);
            Settlement? capital = invasion?.CapitalSettlementId == null ? null : Settlement.Find(invasion.CapitalSettlementId);

            Logger.Info("Invasion garrison army: skipped spawn=" + invasion?.SpawnTemplateId +
                        " capital=" + capital?.StringId +
                        " garrison_men=" + capital?.Town?.GarrisonParty?.MemberRoster.TotalRegulars +
                        " reason=" + reason);
        }

        // A lord still fighting a battle is left to finish it: its end says whether he lost.
        public bool HasEnded(string heroId)
        {
            Hero? lord = FindHero(heroId);
            MobileParty? party = FindLedParty(lord);

            if (lord == null || !lord.IsAlive || lord.IsPrisoner || lord.HeroState == Hero.CharacterStates.Fugitive ||
                party == null)
            {
                return true;
            }

            return party.MapEvent == null && party.Army == null;
        }

        public bool HasGathered(string heroId)
        {
            Army? army = FindLedParty(FindHero(heroId))?.Army;

            return army != null && !army.IsWaitingForArmyMembers();
        }

        public void ReportGathered(string invasionClanId, string heroId)
        {
            Army army = FindLedParty(FindHero(heroId))!.Army;

            Logger.Info("Invasion garrison army: gathered spawn=" + _state().FindInvasion(invasionClanId)?.SpawnTemplateId +
                        " leader=" + heroId + " lords=" + (army.Parties.Count - 1) +
                        " joined=" + army.LeaderParty.AttachedParties.Count + " men=" + army.TotalManCount +
                        " target=" + (army.AiBehaviorObject as Settlement)?.StringId);
        }

        public void End(string invasionClanId, string heroId)
        {
            Invasion? invasion = _state().FindInvasion(invasionClanId);
            Hero? lord = FindHero(heroId);
            MobileParty? party = FindLedParty(lord);
            Settlement? home = party == null || invasion == null
                ? null
                : _bannerlordInvasionGarrisonArmyTargets.FindHeldCapital(invasion) ??
                  _bannerlordInvasionGarrisonArmyTargets.FindNearestRealmFortification(invasion, party);
            int returned = party != null && home != null && party.MapEvent == null
                ? _bannerlordInvasionGarrisonArmyLordParties.Return(party, home)
                : 0;
            string reason = FindEndReason(lord, party);
            bool wasAlive = lord?.IsAlive == true;

            if (lord != null)
            {
                _bannerlordInvasionGarrisonArmyLordParties.Remove(lord, party);
            }

            _dispersalReasons.Remove(heroId);
            Logger.Info("Invasion garrison army: disbanded spawn=" + invasion?.SpawnTemplateId + " reason=" + reason +
                        " leader=" + heroId + " settlement=" + home?.StringId + " returned=" + returned +
                        " lord_removed=" + (wasAlive && lord?.IsAlive == false));
        }

        // Remembered only to say in the log why an army broke up; nothing is decided by it.
        public void ObserveDispersal(Army army, Army.ArmyDispersionReason reason)
        {
            string? heroId = army.LeaderParty?.LeaderHero?.StringId;

            if (heroId != null && _invasionGarrisonArmyLeaders.LeadsGarrisonArmy(heroId))
            {
                _dispersalReasons[heroId] = reason;
            }
        }

        private string FindEndReason(Hero? lord, MobileParty? party)
        {
            if (lord == null || !lord.IsAlive)
            {
                return "lord_dead";
            }

            if (lord.IsPrisoner)
            {
                return "lord_captured";
            }

            if (party == null)
            {
                return "party_lost";
            }

            return _dispersalReasons.TryGetValue(lord.StringId, out Army.ArmyDispersionReason reason)
                ? reason.ToString()
                : "unknown";
        }

        // His own party, never a captor's he is held in.
        private static MobileParty? FindLedParty(Hero? lord)
        {
            MobileParty? party = lord?.PartyBelongedTo;

            return party is { IsActive: true } && party.LeaderHero == lord ? party : null;
        }

        private static Hero? FindHero(string heroId) => Campaign.Current.CampaignObjectManager.Find<Hero>(heroId);
    }
}
