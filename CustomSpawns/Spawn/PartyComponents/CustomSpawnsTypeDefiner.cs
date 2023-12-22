using TaleWorlds.SaveSystem;

namespace CustomSpawns.Spawn.PartyComponents
{
    /**
     * This class defines the CustomSpawnsPartyComponent class as a saveable type.
     * This class does not need to be instantiated as Bannerlord will detect it automatically via reflection.
     */
    public class CustomSpawnsTypeDefiner : SaveableTypeDefiner
    {
        // This is a random number that is used to identify the type definition.
        // It must be unique from all other mods.
        public CustomSpawnsTypeDefiner() : base(7_955_635) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(CustomSpawnsPartyComponent), 1);
        }
    }
}