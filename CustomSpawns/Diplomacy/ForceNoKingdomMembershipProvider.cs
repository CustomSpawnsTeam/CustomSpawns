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

        public ForceNoKingdomMembershipAction GetAction(string clanId, bool hasKingdom, bool isMercenary)
        {
            if (!hasKingdom || !_factionsProvider.GetNonMercenaryCustomSpawnsFactions().Contains(clanId))
            {
                return ForceNoKingdomMembershipAction.None;
            }

            return isMercenary
                ? ForceNoKingdomMembershipAction.LeaveKingdomAsMercenary
                : ForceNoKingdomMembershipAction.LeaveKingdom;
        }
    }
}
