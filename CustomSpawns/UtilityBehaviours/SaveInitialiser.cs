using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.UtilityBehaviours
{
    public class SaveInitialiser : CampaignBehaviorBase
    {
        private readonly List<Action> _actions = new();
        private bool _alreadyRun;

        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, TickBehaviour);
        }

        public override void SyncData(IDataStore dataStore) { }
        
        public void RunCallbackOnFirstCampaignTick(Action action)
        {
            _actions.Add(action);
        }

        private void TickBehaviour(float dt)
        {
            if (_alreadyRun)
                return;
            _alreadyRun = true;
            foreach (var action in _actions)
            {
                action();
            }
        }
    }
}
