namespace CustomSpawns.Diplomacy
{
    public enum ForceNoKingdomMembershipAction
    {
        None,
        LeaveKingdom,
        LeaveKingdomAsMercenary
    }

    public class ForceNoKingdomMembershipProvider
    {
        private readonly NonMercenaryCustomSpawnsFactionsProvider _factionsProvider;

        public ForceNoKingdomMembershipProvider(NonMercenaryCustomSpawnsFactionsProvider factionsProvider)
        {
            _factionsProvider = factionsProvider;
        }

        /**
         * The policy is that the clan serves nobody, not that it is never sovereign: a clan ruling the
         * kingdom it belongs to stays in it. Evicting it leaves the kingdom it founded standing without
         * it, as a separate faction holding its town that the clan is then free to be at war with.
         */
        public ForceNoKingdomMembershipAction GetAction(string clanId, bool hasKingdom, bool isMercenary,
            bool rulesOwnKingdom)
        {
            if (!hasKingdom || rulesOwnKingdom ||
                !_factionsProvider.GetNonMercenaryCustomSpawnsFactions().Contains(clanId))
            {
                return ForceNoKingdomMembershipAction.None;
            }

            return isMercenary
                ? ForceNoKingdomMembershipAction.LeaveKingdomAsMercenary
                : ForceNoKingdomMembershipAction.LeaveKingdom;
        }
    }
}
