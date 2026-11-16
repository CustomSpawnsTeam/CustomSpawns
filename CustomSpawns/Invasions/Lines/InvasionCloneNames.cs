namespace CustomSpawns.Invasions.Lines
{
    /**
     * Names the copies of an invasion's clan after their place in its line: the clan itself stands first, so its
     * first copy is the Second Humar Horde, the next the Third Humar Horde, and so on.
     */
    public sealed class InvasionCloneNames
    {
        private static readonly string[] Ordinals =
        {
            "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth", "Ninth", "Tenth", "Eleventh",
            "Twelfth"
        };

        public string Name(string modelName, int cloneNumber) =>
            Ordinal(cloneNumber + 1) + " " + modelName + " Horde";

        private static string Ordinal(int place)
        {
            if (place >= 2 && place - 2 < Ordinals.Length)
            {
                return Ordinals[place - 2];
            }

            string suffix = (place % 100) is 11 or 12 or 13 ? "th" : (place % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };

            return place + suffix;
        }
    }
}
