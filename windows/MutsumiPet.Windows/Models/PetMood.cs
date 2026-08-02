namespace MutsumiPet.Models
{
    public enum PetMood
    {
        Sleepy = 0,
        Curious = 1,
        Pleased = 2
    }

    public static class PetMoods
    {
        public static readonly PetMood[] AllCases =
        {
            PetMood.Sleepy,
            PetMood.Curious,
            PetMood.Pleased
        };

        public static string Symbol(PetMood mood)
        {
            switch (mood)
            {
                case PetMood.Curious: return "?";
                case PetMood.Pleased: return "♪";
                default: return "zzz";
            }
        }
    }
}
