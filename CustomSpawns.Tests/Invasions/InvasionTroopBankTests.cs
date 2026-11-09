using System;
using System.Linq;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionTroopBankTests
    {
        private readonly InvasionTroopBank _invasionTroopBank = new();

        [Test]
        public void EachWarbandDrawsARepresentativeMixInsteadOfDrainingTheLastTroopType()
        {
            Assert.That(_invasionTroopBank.Distribute(new[] { 600, 300, 100 }, 200),
                Is.EqualTo(new[] { 120, 60, 20 }));
        }

        [Test]
        public void AWaveOverItsCapIsCutDownKeepingTheTemplatesMix()
        {
            Assert.That(_invasionTroopBank.CapWave(new[] { 400, 300, 100 }, 600), Is.EqualTo(new[] { 300, 225, 75 }));
            Assert.That(_invasionTroopBank.CapWave(new[] { 400, 300, 100 }, 600).Sum(), Is.EqualTo(600));
            // A roll under the cap, or no cap at all, is banked whole.
            Assert.That(_invasionTroopBank.CapWave(new[] { 200, 100 }, 600), Is.EqualTo(new[] { 200, 100 }));
            Assert.That(_invasionTroopBank.CapWave(new[] { 400, 300 }, 0), Is.EqualTo(new[] { 400, 300 }));
        }

        [Test]
        public void AWaveLargerThanOneRollRollsAgainUntilItIsFull()
        {
            int rolls = 0;

            int[] wave = _invasionTroopBank.RollWave(() =>
            {
                rolls++;
                return new[] { 400, 300 };
            }, 1400);

            Assert.That(rolls, Is.EqualTo(2));
            Assert.That(wave, Is.EqualTo(new[] { 800, 600 }));
        }

        [Test]
        public void AWaveKeepsOneRollWithoutASizeOrWhenTheTemplateRollsNobody()
        {
            Assert.That(_invasionTroopBank.RollWave(() => new[] { 400, 300 }, 0), Is.EqualTo(new[] { 400, 300 }));
            Assert.That(_invasionTroopBank.RollWave(() => new[] { 0, 0 }, 600), Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public void FractionalSharesFillTheRequestedSizeWithoutExceedingTheAvailableTroops()
        {
            Assert.That(_invasionTroopBank.Distribute(new[] { 7, 2, 1, 0 }, 4),
                Is.EqualTo(new[] { 3, 1, 0, 0 }));
            Assert.That(_invasionTroopBank.Distribute(new[] { 1, 1, 1 }, 2),
                Is.EqualTo(new[] { 1, 1, 0 }));
            Assert.That(_invasionTroopBank.Distribute(new[] { 7, 2, 1, 0 }, 100),
                Is.EqualTo(new[] { 7, 2, 1, 0 }));
        }

        [Test]
        public void EmptyGarrisonsAndNonpositiveRequestsTransferNothing()
        {
            Assert.That(_invasionTroopBank.Distribute(Array.Empty<int>(), 10), Is.Empty);
            Assert.That(_invasionTroopBank.Distribute(new[] { 0, 0 }, 10), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(_invasionTroopBank.Distribute(new[] { 7, 3 }, 0), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(_invasionTroopBank.Distribute(new[] { 7, 3 }, -1), Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public void SuccessiveLordsAndTheCapitalKeepTheSameMix()
        {
            int[] garrison = { 600, 300, 100 };

            for (int lord = 0; lord < 3; lord++)
            {
                int wanted = _invasionTroopBank.Drawable(200, garrison.Sum(), 400);
                int[] drawn = _invasionTroopBank.Distribute(garrison, wanted);

                Assert.That(drawn, Is.EqualTo(new[] { 120, 60, 20 }));

                for (int index = 0; index < garrison.Length; index++)
                {
                    garrison[index] -= drawn[index];
                }
            }

            Assert.That(garrison, Is.EqualTo(new[] { 240, 120, 40 }));
            Assert.That(_invasionTroopBank.Drawable(200, garrison.Sum(), 400), Is.Zero);
        }

        [Test]
        public void RoundingPreservesTheTotalAndEveryTroopTypesProportionalShare()
        {
            int[][] rosters = { new[] { 53, 17, 11, 3, 1, 0 }, new[] { 1, 1, 1, 1 }, new[] { 900, 2, 1 } };

            foreach (int[] roster in rosters)
            {
                int total = roster.Sum();

                for (int wanted = 0; wanted <= total + 1; wanted++)
                {
                    int[] drawn = _invasionTroopBank.Distribute(roster, wanted);
                    Assert.That(drawn.Sum(), Is.EqualTo(Math.Min(wanted, total)));

                    for (int index = 0; index < roster.Length; index++)
                    {
                        double share = (double)roster[index] * Math.Min(wanted, total) / total;
                        Assert.That(drawn[index], Is.InRange(0, roster[index]));
                        Assert.That(Math.Abs(drawn[index] - share), Is.LessThan(1));
                    }
                }
            }
        }

        [Test]
        public void ALordDrawsOnlyWhatStandsAboveTheMinimumCapitalGarrison()
        {
            Assert.AreEqual(200, _invasionTroopBank.Drawable(500, 600, 400));
        }

        [Test]
        public void ALordNeverDrawsMoreThanHeAskedFor()
        {
            Assert.AreEqual(120, _invasionTroopBank.Drawable(120, 800, 400));
        }

        [Test]
        public void AGarrisonAtItsFloorGivesNothing()
        {
            Assert.AreEqual(0, _invasionTroopBank.Drawable(200, 400, 400));
        }

        [Test]
        public void AGarrisonBelowItsFloorGivesNothing()
        {
            Assert.AreEqual(0, _invasionTroopBank.Drawable(200, 150, 400));
        }

        [Test]
        public void ALordKeepsHisFreeWarbandWhenTheBankCannotMatchIt()
        {
            Assert.IsFalse(_invasionTroopBank.ReplacesFreeWarband(60, 430, 400));
            Assert.IsTrue(_invasionTroopBank.ReplacesFreeWarband(60, 460, 400));
        }

        [Test]
        public void LordsAreCappedByTheBaseAndTheTownsHeld()
        {
            // 6 + 3 * 1 = 9 allowed, 7 alive: two of the wave's three come.
            Assert.AreEqual(2, _invasionTroopBank.LordsToField(3, 6, 3, 1, 7));
        }

        [Test]
        public void AnInvasionAtItsLordCapFieldsNone()
        {
            Assert.AreEqual(0, _invasionTroopBank.LordsToField(3, 6, 3, 0, 20));
        }

        [Test]
        public void AnUncappedInvasionFieldsTheWholeWave()
        {
            Assert.AreEqual(3, _invasionTroopBank.LordsToField(3, 0, 3, 1, 20));
        }
    }
}
