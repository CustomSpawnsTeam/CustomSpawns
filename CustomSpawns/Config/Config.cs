﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomSpawns.Config
{
    [Serializable]
    public class Config
    {

        public bool IsDebugMode { get; set; }
        public bool ShowAIDebug { get; set; }
        public bool ShowDeathTrackDebug { get; set; }
        public bool SpawnAtOneHideout { get; set; }
        public bool IsRemovalMode { get; set; }
        public bool IsAllSpawnMode { get; set; }
        public int UpdatePartyRedundantDataPerHour { get; set; }

        public int SameErrorShowUntil { get; set; } 

        public float SpawnChanceFlatMultiplier { get; set; }
    }
}
