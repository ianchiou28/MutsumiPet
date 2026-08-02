using System;
using System.Windows.Media.Imaging;
using MutsumiPet.Models;
using MutsumiPet.Stores;
using MutsumiPet.Support;

namespace MutsumiPet.Tests
{
    /// Port of Tests/MutsumiPetTests/PetStoreTests.swift. Each test gets a fresh
    /// in-memory settings store, mirroring the per-test `UserDefaults` suite the
    /// macOS tests create.
    public static class PetStoreTests
    {
        private static PetStore MakeStore()
        {
            return new PetStore(new InMemoryPetSettings(), new ManualPetScheduler());
        }

        private static PetStore MakeStore(ManualPetScheduler scheduler)
        {
            return new PetStore(new InMemoryPetSettings(), scheduler);
        }

        public static void TestTapCyclesMessageAndMood()
        {
            PetStore store = MakeStore();
            store.ReactToTap(1);

            Assert.Equal(PetDialogue.Lines(PetDialogueScene.Curious)[1], store.Message, "tap message");
            Assert.Equal(PetMood.Curious, store.Mood, "tap mood");
            Assert.Equal(PetPose.Curious, store.Pose, "tap pose");
            Assert.True(store.ShowsBubble, "tap shows the bubble");
        }

        public static void TestScaleIsClamped()
        {
            PetStore store = MakeStore();
            store.SetScale(9);
            Assert.Equal(1.4, store.Scale, "scale clamps to the maximum");
            store.SetScale(0.1);
            Assert.Equal(0.6, store.Scale, "scale clamps to the minimum");
        }

        public static void TestIdleTickUsesRequestedPhrase()
        {
            PetStore store = MakeStore();
            store.ToggleBubble();
            store.IdleTick(4, true);
            Assert.Equal(PetDialogue.Lines(PetDialogueScene.Sleeping)[4], store.Message, "idle tick message");
            Assert.Equal(PetMood.Sleepy, store.Mood, "idle tick mood");
            Assert.Equal(PetPose.Sleeping, store.Pose, "idle tick pose");
        }

        public static void TestDraggingUsesGrabbedPose()
        {
            PetStore store = MakeStore();
            store.BeginDrag(0);
            Assert.Equal(PetPose.Grabbed, store.Pose, "drag pose");
            Assert.Equal(PetDialogue.Lines(PetDialogueScene.Grabbed)[0], store.Message, "drag message");

            store.EndDrag(0);
            Assert.Equal(PetPose.Curious, store.Pose, "released pose");
            Assert.Equal(PetDialogue.Lines(PetDialogueScene.Released)[0], store.Message, "released message");
        }

        public static void TestWindowLayerCanMoveBehindApps()
        {
            PetStore store = MakeStore();
            store.SetLayerMode(WindowLayerMode.Desktop);
            Assert.Equal(WindowLayerMode.Desktop, store.LayerMode, "layer mode");
        }

        public static void TestLifestyleCanChooseTeaAndSnack()
        {
            PetStore teaStore = MakeStore();
            teaStore.LifestyleTick(4, 0);
            Assert.Equal(PetActivity.DrinkingTea, teaStore.Activity, "tea activity");
            Assert.Equal(PetDialogue.Lines(PetDialogueScene.DrinkingTea)[0], teaStore.Message, "tea message");

            PetStore snackStore = MakeStore();
            snackStore.LifestyleTick(5, 0);
            Assert.Equal(PetActivity.EatingSnack, snackStore.Activity, "snack activity");
            Assert.Equal(PetDialogue.Lines(PetDialogueScene.EatingSnack)[0], snackStore.Message, "snack message");
        }

        public static void TestWanderingCanBeDisabledAndSpeedIsClamped()
        {
            PetStore store = MakeStore();
            store.SetWanderSpeed(999);
            Assert.Equal(90.0, store.WanderSpeed, "wander speed clamps");
            store.SetWanderingEnabled(false);
            store.LifestyleTick(0, null);
            Assert.Equal(PetActivity.Idle, store.Activity, "disabled wandering stays idle");
        }

        public static void TestWalkingFramesFollowActualDistance()
        {
            PetStore store = MakeStore();
            store.PerformLifestyle(PetActivity.Walking);

            Assert.Equal(0, store.AnimationFrame, "walk starts on frame 0");
            store.AdvanceWalkingFrame(10.9);
            Assert.Equal(0, store.AnimationFrame, "still frame 0 before a quarter stride");
            store.AdvanceWalkingFrame(0.2);
            Assert.Equal(1, store.AnimationFrame, "frame 1 after a quarter stride");
            store.AdvanceWalkingFrame(11);
            Assert.Equal(2, store.AnimationFrame, "frame 2");
            store.AdvanceWalkingFrame(11);
            Assert.Equal(3, store.AnimationFrame, "frame 3");
            store.AdvanceWalkingFrame(11);
            Assert.Equal(0, store.AnimationFrame, "wraps back to frame 0");
        }

        public static void TestWalkSheetFacesTheDirectionOfTravel()
        {
            Assert.Equal(1.0, PetActivities.HorizontalScale(PetActivity.Walking, -1), "walking left is unmirrored");
            Assert.Equal(-1.0, PetActivities.HorizontalScale(PetActivity.Walking, 1), "walking right mirrors");
            Assert.Equal(1.0, PetActivities.HorizontalScale(PetActivity.DrinkingTea, -1), "only walking mirrors");
        }

        public static void TestWanderMotionProducesDisplacementInBothDirections()
        {
            var rightMotion = new WanderMotion();
            rightMotion.EnsureTarget(200, 0, 500, 1, 100);
            Assert.Equal(WanderMotionStep.Move(207), rightMotion.NextStep(200, 7), "steps right");

            var leftMotion = new WanderMotion();
            leftMotion.EnsureTarget(200, 0, 500, -1, 100);
            Assert.Equal(WanderMotionStep.Move(193), leftMotion.NextStep(200, 7), "steps left");
        }

        public static void TestWanderMotionTurnsInwardAtScreenEdges()
        {
            var rightEdge = new WanderMotion();
            rightEdge.EnsureTarget(500, 0, 500, null, 100);
            Assert.Equal(WanderMotionStep.Move(493), rightEdge.NextStep(500, 7), "turns left at the right edge");

            var leftEdge = new WanderMotion();
            leftEdge.EnsureTarget(0, 0, 500, null, 100);
            Assert.Equal(WanderMotionStep.Move(7), leftEdge.NextStep(0, 7), "turns right at the left edge");
        }

        public static void TestWalkingReturnsToDefaultImmediatelyOnArrival()
        {
            PetStore store = MakeStore();
            store.PerformLifestyle(PetActivity.Walking);
            store.AdvanceWalkingFrame(20);
            Assert.Equal(PetActivity.Walking, store.Activity, "walking before arrival");

            store.FinishWalking();

            Assert.Equal(PetActivity.Idle, store.Activity, "idle after arrival");
            Assert.Equal(PetPose.Idle, store.Pose, "idle pose after arrival");
            Assert.Equal(PetMood.Sleepy, store.Mood, "sleepy mood after arrival");
            Assert.Equal(0, store.AnimationFrame, "frame resets after arrival");
        }

        public static void TestTeaAnimationHoldsFramesForAVisibleSip()
        {
            PetStore store = MakeStore();
            store.PerformLifestyle(PetActivity.DrinkingTea);

            Assert.Equal(0, store.AnimationFrame, "sip starts on frame 0");
            store.AdvanceActivityFrame();
            store.AdvanceActivityFrame();
            store.AdvanceActivityFrame();
            Assert.Equal(0, store.AnimationFrame, "frame 0 is held for four ticks");
            store.AdvanceActivityFrame();
            Assert.Equal(1, store.AnimationFrame, "then advances to frame 1");
        }

        public static void TestTapImmediatelyInterruptsLifestyleAnimation()
        {
            PetStore store = MakeStore();
            store.PerformLifestyle(PetActivity.EatingSnack);
            store.AdvanceActivityFrame();
            Assert.Equal(PetActivity.EatingSnack, store.Activity, "snack in progress");

            store.ReactToTap(0);

            Assert.Equal(PetActivity.Idle, store.Activity, "tap interrupts the activity");
            Assert.Equal(0, store.AnimationFrame, "tap resets the frame");
            Assert.Equal(PetDialogue.Lines(PetDialogueScene.Idle)[0], store.Message, "tap message");
        }

        public static void TestGeneratedLifestyleAssetsLoadAsRealImages()
        {
            foreach (PetActivity activity in PetActivities.Animated)
            {
                BitmapSource image = PetAssets.Character(activity, 0, PetPose.Idle);
                Assert.GreaterThan(image.PixelWidth, 1, activity + " frame width");
                Assert.GreaterThan(image.PixelHeight, 1, activity + " frame height");
            }
        }

        public static void TestEveryPoseLoadsAsARealImage()
        {
            foreach (PetPose pose in PetPoses.AllCases)
            {
                BitmapSource image = PetAssets.Character(pose);
                Assert.GreaterThan(image.PixelWidth, 1, pose + " width");
                Assert.GreaterThan(image.PixelHeight, 1, pose + " height");
            }
        }

        public static void TestEveryActionHasFiveToTenPresetLines()
        {
            foreach (PetDialogueScene scene in PetDialogue.AllCases)
            {
                Assert.InRange(
                    PetDialogue.Lines(scene).Length, 5, 10,
                    scene + " needs five to ten lines");
            }
        }

        public static void TestSpeakForCurrentActionUsesMatchingDialogueSet()
        {
            PetStore store = MakeStore();
            store.PerformLifestyle(PetActivity.Walking, 2);
            store.SpeakForCurrentState(3);

            Assert.Equal(PetDialogue.Lines(PetDialogueScene.Walking)[3], store.Message, "speaks the walking line");
            Assert.True(store.ShowsBubble, "speaking shows the bubble");
        }

        // --- Windows-specific behaviour -------------------------------------

        public static void TestBubbleDismissTimerHidesTheBubble()
        {
            var scheduler = new ManualPetScheduler();
            PetStore store = MakeStore(scheduler);
            store.ReactToTap(0);
            Assert.True(store.ShowsBubble, "bubble shown after a tap");

            scheduler.Fire(PetStore.DismissDelay);

            Assert.False(store.ShowsBubble, "bubble hidden once the dismiss timer runs");
            Assert.Equal(PetMood.Sleepy, store.Mood, "mood settles back to sleepy");
            Assert.Equal(PetPose.Idle, store.Pose, "pose settles back to idle");
        }

        public static void TestDraggingCancelsThePendingDismiss()
        {
            var scheduler = new ManualPetScheduler();
            PetStore store = MakeStore(scheduler);
            store.ReactToTap(0);
            store.BeginDrag(0);

            scheduler.Fire(PetStore.DismissDelay);

            Assert.True(store.ShowsBubble, "a grabbed pet keeps talking");
            Assert.Equal(PetPose.Grabbed, store.Pose, "pose stays grabbed");
        }

        public static void TestSettingsRoundTripThroughTheStore()
        {
            var settings = new InMemoryPetSettings();
            var store = new PetStore(settings, new ManualPetScheduler());
            store.SetScale(1.2);
            store.SetLayerMode(WindowLayerMode.Desktop);
            store.SetWanderingEnabled(false);
            store.SetWanderSpeed(60);

            var reloaded = new PetStore(settings, new ManualPetScheduler());
            Assert.Equal(1.2, reloaded.Scale, "scale persists");
            Assert.Equal(WindowLayerMode.Desktop, reloaded.LayerMode, "layer mode persists");
            Assert.False(reloaded.WanderingEnabled, "wandering flag persists");
            Assert.Equal(60.0, reloaded.WanderSpeed, "wander speed persists");
        }

        public static void TestStoreDefaultsWhenNothingIsSaved()
        {
            PetStore store = MakeStore();
            Assert.Equal(1.0, store.Scale, "default scale");
            Assert.Equal(WindowLayerMode.Front, store.LayerMode, "default layer mode");
            Assert.True(store.WanderingEnabled, "wandering defaults on");
            Assert.Equal(42.0, store.WanderSpeed, "default wander speed");
        }

        public static void TestLayerModeRawValuesRoundTrip()
        {
            foreach (WindowLayerMode mode in WindowLayerModes.AllCases)
            {
                string raw = WindowLayerModes.RawValue(mode);
                Assert.Equal(mode, WindowLayerModes.Parse(raw, WindowLayerMode.Normal), raw + " round-trips");
            }
            Assert.Equal(
                WindowLayerMode.Front,
                WindowLayerModes.Parse(null, WindowLayerMode.Front),
                "missing value falls back");
        }

        public static void TestAlignmentOffsetsAreNormalizedToTheSheet()
        {
            // The first walk frame sits 46.5px left of centre on a 724px-tall sheet.
            System.Windows.Vector offset = PetActivities.AlignmentOffset(PetActivity.Walking, 0);
            Assert.Close(-46.5 / 724.0, offset.X, 1e-9, "walk frame 0 x offset");
            Assert.Close(3.0 / 724.0, offset.Y, 1e-9, "walk frame 0 y offset");

            System.Windows.Vector clamped = PetActivities.AlignmentOffset(PetActivity.Walking, 99);
            Assert.Close(40.5 / 724.0, clamped.X, 1e-9, "out-of-range frame clamps to the last offset");
        }
    }
}
