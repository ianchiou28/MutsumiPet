namespace MutsumiPet.Models
{
    public enum PetDialogueScene
    {
        Idle,
        Curious,
        Happy,
        Sleeping,
        Grabbed,
        Released,
        Walking,
        DrinkingTea,
        EatingSnack
    }

    public static class PetDialogue
    {
        public static readonly PetDialogueScene[] AllCases =
        {
            PetDialogueScene.Idle,
            PetDialogueScene.Curious,
            PetDialogueScene.Happy,
            PetDialogueScene.Sleeping,
            PetDialogueScene.Grabbed,
            PetDialogueScene.Released,
            PetDialogueScene.Walking,
            PetDialogueScene.DrinkingTea,
            PetDialogueScene.EatingSnack
        };

        private static readonly string[] IdleLines =
        {
            "……在。", "不说话，也可以待在一起。", "今天很安静。",
            "桌面有点乱。", "我没有发呆。", "吉他，之后会练。",
            "今天的黄瓜……长高了一点。", "你忙你的，我在这里。"
        };

        private static readonly string[] CuriousLines =
        {
            "你叫我？", "那边有什么？", "刚才是不是动了一下？",
            "……怎么了？", "我有在听。", "要一起看看吗？"
        };

        private static readonly string[] HappyLines =
        {
            "嗯，今天还不错。", "你在就好。", "稍微……有点开心。",
            "再待一会儿吧。", "这个，我很喜欢。", "也分你一点好心情。"
        };

        private static readonly string[] SleepingLines =
        {
            "稍微……休息一下。", "再睡五分钟。", "这里很安静……",
            "晚安，不对……还没到晚上。", "醒来的时候再练琴。", "呼……"
        };

        private static readonly string[] GrabbedLines =
        {
            "……被抓住了。", "等等，脚碰不到地了。", "你要把我带去哪里？",
            "这样有点高。", "衣服不要弄皱。", "……先放我下来。"
        };

        private static readonly string[] ReleasedLines =
        {
            "……放下来了。", "脚终于碰到地了。", "下次先说一声。",
            "我没有被吓到。", "站稳了……嗯。", "就当什么都没发生。"
        };

        private static readonly string[] WalkingLines =
        {
            "去那边看看。", "稍微走一走。", "一直坐着也不好。",
            "前面应该没有东西。", "散步的时候，不用说话。", "我很快就回来。",
            "一步、两步……"
        };

        private static readonly string[] DrinkingTeaLines =
        {
            "茶……温度刚好。", "先吹一下。", "今天想喝淡一点的。",
            "这杯很香。", "你也要一杯吗？", "慢慢喝就不会烫。",
            "喝完再继续。"
        };

        private static readonly string[] EatingSnackLines =
        {
            "点心，分你一点。", "只吃一小块。", "这个没有太甜。",
            "要配茶才好。", "最后一口……", "你也想吃吗？",
            "吃完要把碎屑收好。"
        };

        public static string[] Lines(PetDialogueScene scene)
        {
            switch (scene)
            {
                case PetDialogueScene.Curious: return CuriousLines;
                case PetDialogueScene.Happy: return HappyLines;
                case PetDialogueScene.Sleeping: return SleepingLines;
                case PetDialogueScene.Grabbed: return GrabbedLines;
                case PetDialogueScene.Released: return ReleasedLines;
                case PetDialogueScene.Walking: return WalkingLines;
                case PetDialogueScene.DrinkingTea: return DrinkingTeaLines;
                case PetDialogueScene.EatingSnack: return EatingSnackLines;
                default: return IdleLines;
            }
        }

        public static string Line(PetDialogueScene scene)
        {
            return Line(scene, null);
        }

        public static string Line(PetDialogueScene scene, int? requestedIndex)
        {
            string[] lines = Lines(scene);
            if (lines.Length == 0) return "……";
            if (requestedIndex == null) return lines[PetRandom.Next(0, lines.Length)];

            int index = requestedIndex.Value;
            if (index < 0) index = 0;
            if (index > lines.Length - 1) index = lines.Length - 1;
            return lines[index];
        }
    }
}
