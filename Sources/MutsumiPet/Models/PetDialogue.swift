enum PetDialogueScene: CaseIterable {
    case idle
    case curious
    case happy
    case sleeping
    case grabbed
    case released
    case walking
    case drinkingTea
    case eatingSnack

    var lines: [String] {
        switch self {
        case .idle:
            [
                "……在。", "不说话，也可以待在一起。", "今天很安静。",
                "桌面有点乱。", "我没有发呆。", "吉他，之后会练。",
                "今天的黄瓜……长高了一点。", "你忙你的，我在这里。"
            ]
        case .curious:
            [
                "你叫我？", "那边有什么？", "刚才是不是动了一下？",
                "……怎么了？", "我有在听。", "要一起看看吗？"
            ]
        case .happy:
            [
                "嗯，今天还不错。", "你在就好。", "稍微……有点开心。",
                "再待一会儿吧。", "这个，我很喜欢。", "也分你一点好心情。"
            ]
        case .sleeping:
            [
                "稍微……休息一下。", "再睡五分钟。", "这里很安静……",
                "晚安，不对……还没到晚上。", "醒来的时候再练琴。", "呼……"
            ]
        case .grabbed:
            [
                "……被抓住了。", "等等，脚碰不到地了。", "你要把我带去哪里？",
                "这样有点高。", "衣服不要弄皱。", "……先放我下来。"
            ]
        case .released:
            [
                "……放下来了。", "脚终于碰到地了。", "下次先说一声。",
                "我没有被吓到。", "站稳了……嗯。", "就当什么都没发生。"
            ]
        case .walking:
            [
                "去那边看看。", "稍微走一走。", "一直坐着也不好。",
                "前面应该没有东西。", "散步的时候，不用说话。", "我很快就回来。",
                "一步、两步……"
            ]
        case .drinkingTea:
            [
                "茶……温度刚好。", "先吹一下。", "今天想喝淡一点的。",
                "这杯很香。", "你也要一杯吗？", "慢慢喝就不会烫。",
                "喝完再继续。"
            ]
        case .eatingSnack:
            [
                "点心，分你一点。", "只吃一小块。", "这个没有太甜。",
                "要配茶才好。", "最后一口……", "你也想吃吗？",
                "吃完要把碎屑收好。"
            ]
        }
    }

    func line(at requestedIndex: Int? = nil) -> String {
        guard let requestedIndex else { return lines.randomElement() ?? "……" }
        return lines[min(max(requestedIndex, 0), lines.count - 1)]
    }
}
