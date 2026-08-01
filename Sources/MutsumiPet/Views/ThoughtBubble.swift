import SwiftUI

struct ThoughtBubble: View {
    let text: String
    let mood: PetMood

    var body: some View {
        VStack(alignment: .trailing, spacing: -4) {
            HStack(spacing: 9) {
                Text(mood.symbol)
                    .font(.system(size: 17, weight: .heavy, design: .rounded))
                    .foregroundStyle(Color(red: 0.05, green: 0.24, blue: 0.12))
                Text(text)
                    .font(.system(size: 15, weight: .semibold, design: .rounded))
                    .foregroundStyle(Color(red: 0.07, green: 0.20, blue: 0.12))
                    .lineLimit(2)
            }
            .padding(.horizontal, 18)
            .padding(.vertical, 13)
            .background(.white.opacity(0.94), in: Capsule())
            .overlay(Capsule().stroke(Color(red: 0.04, green: 0.25, blue: 0.12), lineWidth: 3))

            HStack(spacing: 7) {
                Circle().fill(.white.opacity(0.94)).frame(width: 13, height: 13)
                    .overlay(Circle().stroke(Color(red: 0.04, green: 0.25, blue: 0.12), lineWidth: 2.5))
                Circle().fill(.white.opacity(0.94)).frame(width: 8, height: 8)
                    .overlay(Circle().stroke(Color(red: 0.04, green: 0.25, blue: 0.12), lineWidth: 2))
            }
            .padding(.trailing, 34)
        }
        .fixedSize(horizontal: false, vertical: true)
        .shadow(color: .black.opacity(0.10), radius: 8, y: 3)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("若叶睦说：\(text)")
    }
}
