namespace MutsumiPet.Models
{
    public enum PetPose
    {
        Idle,
        Curious,
        Happy,
        Sleeping,
        Grabbed
    }

    public static class PetPoses
    {
        public static readonly PetPose[] AllCases =
        {
            PetPose.Idle,
            PetPose.Curious,
            PetPose.Happy,
            PetPose.Sleeping,
            PetPose.Grabbed
        };

        public static string AssetName(PetPose pose)
        {
            switch (pose)
            {
                case PetPose.Curious: return "mutsumi_curious";
                case PetPose.Happy: return "mutsumi_happy";
                case PetPose.Sleeping: return "mutsumi_sleeping";
                case PetPose.Grabbed: return "mutsumi_grabbed";
                default: return "mutsumi_pet";
            }
        }
    }
}
