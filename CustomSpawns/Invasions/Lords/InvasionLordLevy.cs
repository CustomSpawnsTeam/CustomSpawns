using CustomSpawns.Invasions.Bank;
using CustomSpawns.Invasions.Reinforcements;

namespace CustomSpawns.Invasions.Lords
{
    // The lords of the campaign. Bannerlord access stays behind this boundary.
    public interface IInvasionLords
    {
        // The towns of the invasion's kingdom, the same ones that raise its strength ceiling.
        int CountTownsHeld(Invasion invasion);

        // Every adult lord of the invasion's clans, prisoners included.
        int CountLivingLords(Invasion invasion);

        // Fields one new lord of the invasion at the capital, false when no clan has room for him.
        bool Field(Invasion invasion, string capitalId);
    }

    // The lords a wave brings, held to the invasion's lord cap.
    public sealed class InvasionLordLevy
    {
        private readonly IInvasionLords _invasionLords;
        private readonly InvasionTroopBank _invasionTroopBank;

        public InvasionLordLevy(IInvasionLords invasionLords, InvasionTroopBank invasionTroopBank)
        {
            _invasionLords = invasionLords;
            _invasionTroopBank = invasionTroopBank;
        }

        public int Levy(Invasion invasion, InvasionWave wave, string capitalId)
        {
            int allowed = _invasionTroopBank.LordsToField(wave.LordsPerWave, wave.MaxLords, wave.LordsPerTownHeld,
                _invasionLords.CountTownsHeld(invasion), _invasionLords.CountLivingLords(invasion));
            int fielded = 0;

            while (fielded < allowed && _invasionLords.Field(invasion, capitalId))
            {
                fielded++;
            }

            return fielded;
        }
    }
}
