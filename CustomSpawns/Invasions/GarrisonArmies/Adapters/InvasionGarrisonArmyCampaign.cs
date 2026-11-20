using System;
using System.Collections.Generic;
using CustomSpawns.Data.Dao;
using CustomSpawns.Invasions.Bank;
using CustomSpawns.Invasions.Bank.Adapters;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Lords.Adapters;
using CustomSpawns.Invasions.Reinforcements.Adapters;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.GarrisonArmies.Adapters
{
    /**
     * Puts the garrison armies together and answers the campaign's events for them, so the invasion behaviour only
     * passes those events on.
     */
    public sealed class InvasionGarrisonArmyCampaign
    {
        private readonly InvasionGarrisonArmies _invasionGarrisonArmies;
        private readonly InvasionGarrisonArmyClock _invasionGarrisonArmyClock = new();
        private readonly InvasionGarrisonArmyLords _invasionGarrisonArmyLords;
        private readonly BannerlordInvasionGarrisonArmyWorld _bannerlordInvasionGarrisonArmyWorld;
        private readonly InvasionGarrisonArmyRulerRelease _invasionGarrisonArmyRulerRelease;

        public InvasionGarrisonArmyCampaign(Func<InvasionState> state, SpawnDao spawnDao,
            InvasionTroopBank invasionTroopBank, IInvasionConquestTargets invasionConquestTargets,
            InvaderHeroCreator invaderHeroCreator, InvasionGarrisonArmyLords invasionGarrisonArmyLords)
        {
            _invasionGarrisonArmyLords = invasionGarrisonArmyLords;
            BannerlordInvasionGarrisonArmyLordParties lordParties =
                new(invaderHeroCreator, new BannerlordInvasionWarbands(invasionTroopBank));
            BannerlordInvasionGarrisonArmyTargets targets = new(invasionConquestTargets, new InvasionNearestChoice());
            InvasionGarrisonArmySizing sizing = new();
            _bannerlordInvasionGarrisonArmyWorld = new BannerlordInvasionGarrisonArmyWorld(state,
                new BannerlordInvasionGarrisonArmyConfigurations(spawnDao), lordParties, targets,
                new BannerlordInvasionCommissariat(), invasionGarrisonArmyLords);
            _invasionGarrisonArmies = new InvasionGarrisonArmies(_bannerlordInvasionGarrisonArmyWorld,
                new InvasionGarrisonArmyMuster(sizing), sizing, _invasionGarrisonArmyClock, invasionGarrisonArmyLords);
            InvasionGarrisonArmyOrder = new InvasionGarrisonArmyOrder(state, targets, invasionGarrisonArmyLords);
            _invasionGarrisonArmyRulerRelease = new InvasionGarrisonArmyRulerRelease(state);
        }

        // Keeps each garrison army fighting; Main hands it to the AI with the other orders.
        public InvasionGarrisonArmyOrder InvasionGarrisonArmyOrder { get; }

        public void UpdateDaily() => _invasionGarrisonArmies.UpdateDaily(CampaignTime.Now.ToDays);

        // Ends the armies whose lord was beaten, taken or lost his army, reports those that finished gathering, and
        // sends the realm's ruler out of any besieging army he follows.
        public void Review()
        {
            _invasionGarrisonArmies.Review();
            _invasionGarrisonArmyRulerRelease.Release();
        }

        public void ObserveDispersal(Army army, Army.ArmyDispersionReason reason) =>
            _bannerlordInvasionGarrisonArmyWorld.ObserveDispersal(army, reason);

        public Dictionary<string, string> Export() => _invasionGarrisonArmyClock.Export();

        public void Restore(Dictionary<string, string>? saved) => _invasionGarrisonArmyClock.Restore(saved);

        public Dictionary<string, string> ExportLords() => _invasionGarrisonArmyLords.Export();

        public void RestoreLords(Dictionary<string, string>? saved) => _invasionGarrisonArmyLords.Restore(saved);
    }
}
