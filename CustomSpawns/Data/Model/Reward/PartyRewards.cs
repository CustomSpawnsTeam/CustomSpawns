using System.Collections.Generic;
using System.Xml.Serialization;

namespace CustomSpawns.Data.Model.Reward
{
    [XmlRoot("PartyRewardsTemplate")]
    public class PartyRewards
    {
        [XmlElement("PartyReward")]
        public List<PartyReward> AllPartyRewards
        {
            get;
            set;
        } = new ();
    }
}