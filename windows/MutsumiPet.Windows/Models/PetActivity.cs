using System.Windows;

namespace MutsumiPet.Models
{
    public enum PetActivity
    {
        Idle,
        Walking,
        DrinkingTea,
        EatingSnack
    }

    public static class PetActivities
    {
        /// The generated sprite sheets are this many pixels tall; alignment
        /// offsets below are expressed in sheet pixels and normalized by it.
        public const double SheetHeight = 724;

        public static readonly PetActivity[] Animated =
        {
            PetActivity.Walking,
            PetActivity.DrinkingTea,
            PetActivity.EatingSnack
        };

        private static readonly int[] IdleFrames = { 0 };
        private static readonly int[] WalkFrames = { 0, 1, 2, 3 };

        private static readonly int[] SipFrames =
        {
            0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 2, 2, 1, 1, 0, 0, 0, 0
        };

        private static readonly Vector[] IdleOffsets = { new Vector(0, 0) };

        private static readonly Vector[] WalkOffsets =
        {
            new Vector(-46.5, 3),
            new Vector(-13, -6),
            new Vector(20, 11),
            new Vector(40.5, -7)
        };

        private static readonly Vector[] TeaOffsets =
        {
            new Vector(-18, 0),
            new Vector(-5, 0),
            new Vector(10, 0),
            new Vector(13, 0)
        };

        private static readonly Vector[] SnackOffsets =
        {
            new Vector(-9, 0),
            new Vector(-9, 1),
            new Vector(2.5, 0),
            new Vector(15.5, 0)
        };

        public static int[] FrameSequence(PetActivity activity)
        {
            switch (activity)
            {
                case PetActivity.Walking: return WalkFrames;
                case PetActivity.DrinkingTea:
                case PetActivity.EatingSnack: return SipFrames;
                default: return IdleFrames;
            }
        }

        /// Offsets are normalized to the generated sheet height so the character's
        /// visual center and feet stay fixed when adjacent frames have different bounds.
        public static Vector AlignmentOffset(PetActivity activity, int frame)
        {
            Vector[] offsets;
            switch (activity)
            {
                case PetActivity.Walking: offsets = WalkOffsets; break;
                case PetActivity.DrinkingTea: offsets = TeaOffsets; break;
                case PetActivity.EatingSnack: offsets = SnackOffsets; break;
                default: offsets = IdleOffsets; break;
            }

            int index = frame;
            if (index < 0) index = 0;
            if (index > offsets.Length - 1) index = offsets.Length - 1;

            Vector offset = offsets[index];
            return new Vector(offset.X / SheetHeight, offset.Y / SheetHeight);
        }

        /// The generated walk sheet faces screen-left in its unmirrored form.
        public static double HorizontalScale(PetActivity activity, int walkingDirection)
        {
            if (activity != PetActivity.Walking) return 1;
            return walkingDirection < 0 ? 1 : -1;
        }

        public static string StripAssetName(PetActivity activity)
        {
            switch (activity)
            {
                case PetActivity.Walking: return "mutsumi_walk_strip";
                case PetActivity.DrinkingTea: return "mutsumi_tea_strip";
                case PetActivity.EatingSnack: return "mutsumi_snack_strip";
                default: return null;
            }
        }
    }
}
