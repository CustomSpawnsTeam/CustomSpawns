namespace CustomSpawns.Invasions
{
    /**
     * Now and then an invasion holding its capital leads part of the garrison out as one army, under
     * a lord made for it alone, against the kingdom it conquers. The army fights on, siege after siege, until that
     * lord is beaten or captured; then his men go back into a garrison and he is gone.
     */
    public sealed class InvasionGarrisonArmies
    {
        private readonly IInvasionGarrisonArmyWorld _invasionGarrisonArmyWorld;
        private readonly InvasionGarrisonArmyMuster _invasionGarrisonArmyMuster;
        private readonly InvasionGarrisonArmySizing _invasionGarrisonArmySizing;
        private readonly InvasionGarrisonArmyClock _invasionGarrisonArmyClock;
        private readonly InvasionGarrisonArmyLords _invasionGarrisonArmyLords;

        public InvasionGarrisonArmies(IInvasionGarrisonArmyWorld invasionGarrisonArmyWorld,
            InvasionGarrisonArmyMuster invasionGarrisonArmyMuster, InvasionGarrisonArmySizing invasionGarrisonArmySizing,
            InvasionGarrisonArmyClock invasionGarrisonArmyClock, InvasionGarrisonArmyLords invasionGarrisonArmyLords)
        {
            _invasionGarrisonArmyWorld = invasionGarrisonArmyWorld;
            _invasionGarrisonArmyMuster = invasionGarrisonArmyMuster;
            _invasionGarrisonArmySizing = invasionGarrisonArmySizing;
            _invasionGarrisonArmyClock = invasionGarrisonArmyClock;
            _invasionGarrisonArmyLords = invasionGarrisonArmyLords;
        }

        public void UpdateDaily(double today)
        {
            foreach (string clanId in _invasionGarrisonArmyWorld.FindInvasionClanIds())
            {
                InvasionGarrisonArmyRules? rules = _invasionGarrisonArmyWorld.FindRules(clanId);

                if (rules != null)
                {
                    Update(clanId, rules.Value, today);
                }
            }
        }

        /**
         * Hourly, and at once when a lord is captured, so a beaten lord is gone before vanilla can offer a ransom
         * for him, and men whose army broke up do not wander the map for long before they are home.
         */
        public void Review()
        {
            foreach (string clanId in _invasionGarrisonArmyLords.FindClanIds())
            {
                InvasionGarrisonArmyLord lord = _invasionGarrisonArmyLords.Find(clanId)!.Value;

                if (_invasionGarrisonArmyWorld.HasEnded(lord.HeroId))
                {
                    _invasionGarrisonArmyWorld.End(clanId, lord.HeroId);
                    _invasionGarrisonArmyLords.Forget(clanId);
                    continue;
                }

                if (!lord.HasGathered && _invasionGarrisonArmyWorld.HasGathered(lord.HeroId))
                {
                    _invasionGarrisonArmyWorld.ReportGathered(clanId, lord.HeroId);
                    _invasionGarrisonArmyLords.MarkGathered(clanId);
                }
            }
        }

        private void Update(string clanId, InvasionGarrisonArmyRules rules, double today)
        {
            if (!_invasionGarrisonArmyMuster.IsDue(rules,
                    _invasionGarrisonArmyClock.FindDaysSinceLastRaise(clanId, today),
                    _invasionGarrisonArmyLords.IsArmyOut(clanId)))
            {
                return;
            }

            InvasionGarrisonArmyFacts? facts = _invasionGarrisonArmyWorld.Observe(clanId);

            if (facts == null)
            {
                return;
            }

            InvasionGarrisonArmyMusterDecision decision = _invasionGarrisonArmyMuster.Decide(rules, facts.Value);

            if (decision.SkipReason != null)
            {
                Skip(clanId, decision.SkipReason);
                return;
            }

            Raise(clanId, decision.Men, today);
        }

        private void Raise(string clanId, int men, double today)
        {
            string? heroId = _invasionGarrisonArmyWorld.CreateLord(clanId);

            if (heroId == null)
            {
                Skip(clanId, InvasionGarrisonArmyMuster.NoLord);
                return;
            }

            // Recorded before his party exists, so the party is sized for all its men from the moment it is made.
            _invasionGarrisonArmyLords.Record(clanId, heroId, men + 1);
            int food = _invasionGarrisonArmySizing.FindFood(men, _invasionGarrisonArmyWorld.FindMenPerFoodPerDay(),
                _invasionGarrisonArmyWorld.FindMinimumFoodDays());

            if (_invasionGarrisonArmyWorld.Raise(clanId, heroId, men, food))
            {
                _invasionGarrisonArmyClock.MarkRaised(clanId, today);
                return;
            }

            _invasionGarrisonArmyWorld.End(clanId, heroId);
            _invasionGarrisonArmyLords.Forget(clanId);
        }

        private void Skip(string clanId, string reason)
        {
            if (_invasionGarrisonArmyClock.ShouldLogSkip(clanId, reason))
            {
                _invasionGarrisonArmyWorld.ReportSkip(clanId, reason);
            }
        }
    }
}
