using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Spawn.PartySize
{
    /**
     * Notifies the registered observers that a the size limit of a party has been calculated.
     */
    public class PartySizeCalculatedSubject
    {
        private static readonly List<IPartySizeLimitObserver> Observers = new ();

        /**
         * Registers an observer to be notified when the party size limit has been calculated.
         * @param observer the observer to be notified
         */
        public void Register(IPartySizeLimitObserver observer)
        {
            Observers.Add(observer);
        }

        /**
         * Unregisters an observer from being notified when the party size limit has been calculated.
         */
        public void Unregister(IPartySizeLimitObserver observer)
        {
            Observers.Remove(observer);
        }

        /**
         * Notifies all registered observers that the party size limit has been calculated.
         * @param mobileParty the mobile party for which the party size limit has been calculated
         * @param explainedPartySizeLimit the party size limit that has been calculated
         * @return the party size limit that should be used
         */
        public ExplainedNumber NotifyObservers(MobileParty mobileParty, ExplainedNumber explainedPartySizeLimit)
        {
            return Observers.Aggregate(explainedPartySizeLimit, 
                    (current, observer) => observer.OnPartySizeLimitCalculated(mobileParty, current));
        }
    }
}