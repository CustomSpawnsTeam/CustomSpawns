using System.Collections.Generic;

namespace CustomSpawns.Data.Model
{
    public class Diplomacy
    {
         public string clanString;

         public class ForcedWarPeaceData
         {
             public List<string> AtPeaceWithClans = new();
             public List<string> ExceptionKingdoms = new();
         }

         public ForcedWarPeaceData? ForcedWarPeaceDataInstance;

         public bool ForceNoKingdom { get; set; }
    }
}