import CoreGraphics

struct WanderMotion {
    private(set) var targetX: CGFloat?
    private(set) var lastAppliedDirection = -1

    mutating func resetTarget() {
        targetX = nil
    }

    mutating func ensureTarget(
        currentX: CGFloat,
        minX: CGFloat,
        maxX: CGFloat,
        preferredDirection: Int? = nil,
        requestedDistance: CGFloat? = nil
    ) {
        guard targetX == nil, maxX > minX else { return }
        let current = min(max(currentX, minX), maxX)
        let edgeThreshold = min(140, maxX - minX)

        var direction: Int
        if let preferredDirection {
            direction = preferredDirection < 0 ? -1 : 1
        } else if current - minX < edgeThreshold {
            direction = 1
        } else if maxX - current < edgeThreshold {
            direction = -1
        } else {
            direction = -lastAppliedDirection
        }

        func availableDistance(for direction: Int) -> CGFloat {
            direction < 0 ? current - minX : maxX - current
        }

        if availableDistance(for: direction) < 2 {
            direction *= -1
        }
        let available = availableDistance(for: direction)
        guard available > 0 else { return }

        let maximum = min(320, available)
        let minimum = min(140, maximum)
        let distance = min(
            max(requestedDistance ?? CGFloat.random(in: minimum...maximum), 0),
            maximum
        )
        targetX = min(max(current + CGFloat(direction) * distance, minX), maxX)
    }

    mutating func nextStep(currentX: CGFloat, maximumDistance: CGFloat) -> WanderMotionStep {
        guard let targetX else { return .paused }
        let difference = targetX - currentX
        if abs(difference) < 2 {
            self.targetX = nil
            return .arrived(x: targetX)
        }

        let direction: CGFloat = difference < 0 ? -1 : 1
        let distance = min(abs(difference), max(0.5, maximumDistance))
        return .move(toX: currentX + direction * distance)
    }

    mutating func recordApplied(deltaX: CGFloat) {
        guard abs(deltaX) > 0.01 else { return }
        lastAppliedDirection = deltaX < 0 ? -1 : 1
    }
}

enum WanderMotionStep: Equatable {
    case paused
    case move(toX: CGFloat)
    case arrived(x: CGFloat)
}
