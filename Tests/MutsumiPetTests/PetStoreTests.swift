import XCTest
@testable import MutsumiPet

@MainActor
final class PetStoreTests: XCTestCase {
    private func makeStore() -> PetStore {
        let suite = "MutsumiPetTests.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defaults.removePersistentDomain(forName: suite)
        return PetStore(defaults: defaults)
    }

    func testTapCyclesMessageAndMood() {
        let store = makeStore()
        store.reactToTap(randomDialogueIndex: 1)

        XCTAssertEqual(store.message, PetDialogueScene.curious.lines[1])
        XCTAssertEqual(store.mood, .curious)
        XCTAssertEqual(store.pose, .curious)
        XCTAssertTrue(store.showsBubble)
    }

    func testScaleIsClamped() {
        let store = makeStore()
        store.setScale(9)
        XCTAssertEqual(store.scale, 1.4)
        store.setScale(0.1)
        XCTAssertEqual(store.scale, 0.6)
    }

    func testIdleTickUsesRequestedPhrase() {
        let store = makeStore()
        store.toggleBubble()
        store.idleTick(randomIndex: 4, sleeping: true)
        XCTAssertEqual(store.message, PetDialogueScene.sleeping.lines[4])
        XCTAssertEqual(store.mood, .sleepy)
        XCTAssertEqual(store.pose, .sleeping)
    }

    func testDraggingUsesGrabbedPose() {
        let store = makeStore()
        store.beginDrag(randomDialogueIndex: 0)
        XCTAssertEqual(store.pose, .grabbed)
        XCTAssertEqual(store.message, PetDialogueScene.grabbed.lines[0])

        store.endDrag(randomDialogueIndex: 0)
        XCTAssertEqual(store.pose, .curious)
        XCTAssertEqual(store.message, PetDialogueScene.released.lines[0])
    }

    func testWindowLayerCanMoveBehindApps() {
        let store = makeStore()
        store.setLayerMode(.desktop)
        XCTAssertEqual(store.layerMode, .desktop)
    }

    func testLifestyleCanChooseTeaAndSnack() {
        let teaStore = makeStore()
        teaStore.lifestyleTick(randomEvent: 4, randomDialogueIndex: 0)
        XCTAssertEqual(teaStore.activity, .drinkingTea)
        XCTAssertEqual(teaStore.message, PetDialogueScene.drinkingTea.lines[0])

        let snackStore = makeStore()
        snackStore.lifestyleTick(randomEvent: 5, randomDialogueIndex: 0)
        XCTAssertEqual(snackStore.activity, .eatingSnack)
        XCTAssertEqual(snackStore.message, PetDialogueScene.eatingSnack.lines[0])
    }

    func testWanderingCanBeDisabledAndSpeedIsClamped() {
        let store = makeStore()
        store.setWanderSpeed(999)
        XCTAssertEqual(store.wanderSpeed, 90)
        store.setWanderingEnabled(false)
        store.lifestyleTick(randomEvent: 0)
        XCTAssertEqual(store.activity, .idle)
    }

    func testWalkingFramesFollowActualDistance() {
        let store = makeStore()
        store.performLifestyle(.walking)

        XCTAssertEqual(store.animationFrame, 0)
        store.advanceWalkingFrame(distance: 10.9)
        XCTAssertEqual(store.animationFrame, 0)
        store.advanceWalkingFrame(distance: 0.2)
        XCTAssertEqual(store.animationFrame, 1)
        store.advanceWalkingFrame(distance: 11)
        XCTAssertEqual(store.animationFrame, 2)
        store.advanceWalkingFrame(distance: 11)
        XCTAssertEqual(store.animationFrame, 3)
        store.advanceWalkingFrame(distance: 11)
        XCTAssertEqual(store.animationFrame, 0)
    }

    func testWalkSheetFacesTheDirectionOfTravel() {
        XCTAssertEqual(PetActivity.walking.horizontalScale(walkingDirection: -1), 1)
        XCTAssertEqual(PetActivity.walking.horizontalScale(walkingDirection: 1), -1)
        XCTAssertEqual(PetActivity.drinkingTea.horizontalScale(walkingDirection: -1), 1)
    }

    func testWanderMotionProducesDisplacementInBothDirections() {
        var rightMotion = WanderMotion()
        rightMotion.ensureTarget(
            currentX: 200,
            minX: 0,
            maxX: 500,
            preferredDirection: 1,
            requestedDistance: 100
        )
        XCTAssertEqual(rightMotion.nextStep(currentX: 200, maximumDistance: 7), .move(toX: 207))

        var leftMotion = WanderMotion()
        leftMotion.ensureTarget(
            currentX: 200,
            minX: 0,
            maxX: 500,
            preferredDirection: -1,
            requestedDistance: 100
        )
        XCTAssertEqual(leftMotion.nextStep(currentX: 200, maximumDistance: 7), .move(toX: 193))
    }

    func testWanderMotionTurnsInwardAtScreenEdges() {
        var rightEdge = WanderMotion()
        rightEdge.ensureTarget(currentX: 500, minX: 0, maxX: 500, requestedDistance: 100)
        XCTAssertEqual(rightEdge.nextStep(currentX: 500, maximumDistance: 7), .move(toX: 493))

        var leftEdge = WanderMotion()
        leftEdge.ensureTarget(currentX: 0, minX: 0, maxX: 500, requestedDistance: 100)
        XCTAssertEqual(leftEdge.nextStep(currentX: 0, maximumDistance: 7), .move(toX: 7))
    }

    func testWalkingReturnsToDefaultImmediatelyOnArrival() {
        let store = makeStore()
        store.performLifestyle(.walking)
        store.advanceWalkingFrame(distance: 20)
        XCTAssertEqual(store.activity, .walking)

        store.finishWalking()

        XCTAssertEqual(store.activity, .idle)
        XCTAssertEqual(store.pose, .idle)
        XCTAssertEqual(store.mood, .sleepy)
        XCTAssertEqual(store.animationFrame, 0)
    }

    func testTeaAnimationHoldsFramesForAVisibleSip() {
        let store = makeStore()
        store.performLifestyle(.drinkingTea)

        XCTAssertEqual(store.animationFrame, 0)
        store.advanceActivityFrame()
        store.advanceActivityFrame()
        store.advanceActivityFrame()
        XCTAssertEqual(store.animationFrame, 0)
        store.advanceActivityFrame()
        XCTAssertEqual(store.animationFrame, 1)
    }

    func testTapImmediatelyInterruptsLifestyleAnimation() {
        let store = makeStore()
        store.performLifestyle(.eatingSnack)
        store.advanceActivityFrame()
        XCTAssertEqual(store.activity, .eatingSnack)

        store.reactToTap(randomDialogueIndex: 0)

        XCTAssertEqual(store.activity, .idle)
        XCTAssertEqual(store.animationFrame, 0)
        XCTAssertEqual(store.message, PetDialogueScene.idle.lines[0])
    }

    func testGeneratedLifestyleAssetsLoadAsRealImages() {
        for activity in [PetActivity.walking, .drinkingTea, .eatingSnack] {
            let image = PetAssets.character(for: activity, frame: 0, fallback: .idle)
            XCTAssertGreaterThan(image.size.width, 1)
            XCTAssertGreaterThan(image.size.height, 1)
        }
    }

    func testEveryActionHasFiveToTenPresetLines() {
        for scene in PetDialogueScene.allCases {
            XCTAssertGreaterThanOrEqual(scene.lines.count, 5, "\(scene) needs at least five lines")
            XCTAssertLessThanOrEqual(scene.lines.count, 10, "\(scene) should stay concise")
        }
    }

    func testSpeakForCurrentActionUsesMatchingDialogueSet() {
        let store = makeStore()
        store.performLifestyle(.walking, randomDialogueIndex: 2)
        store.speakForCurrentState(randomDialogueIndex: 3)

        XCTAssertEqual(store.message, PetDialogueScene.walking.lines[3])
        XCTAssertTrue(store.showsBubble)
    }
}
