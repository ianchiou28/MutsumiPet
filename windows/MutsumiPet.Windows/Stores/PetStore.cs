using System;
using System.Collections.Generic;
using System.ComponentModel;
using MutsumiPet.Models;
using MutsumiPet.Support;

namespace MutsumiPet.Stores
{
    /// All of the pet's behaviour lives here, deliberately free of window and
    /// rendering types so the mood/activity state machine can be unit tested.
    public sealed class PetStore : INotifyPropertyChanged
    {
        public const string ScaleKey = "pet.scale";
        public const string LayerModeKey = "pet.windowLayerMode";
        public const string WanderingEnabledKey = "pet.wanderingEnabled";
        public const string WanderSpeedKey = "pet.wanderSpeed";

        public static readonly TimeSpan DismissDelay = TimeSpan.FromSeconds(6);
        public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(120);
        public static readonly TimeSpan WalkTimeout = TimeSpan.FromSeconds(20);
        public static readonly TimeSpan SipDuration = TimeSpan.FromSeconds(7);

        private readonly IPetSettings settings;
        private readonly IPetScheduler scheduler;

        private PetMood mood = PetMood.Sleepy;
        private PetPose pose = PetPose.Idle;
        private string message;
        private bool showsBubble = true;
        private WindowLayerMode layerMode;
        private double scale;
        private PetActivity activity = PetActivity.Idle;
        private int walkingDirection = -1;
        private bool wanderingEnabled;
        private double wanderSpeed;
        private int animationFrame;

        private IDisposable dismissWork;
        private IDisposable settleWork;
        private IDisposable activityWork;
        private int animationSequenceIndex;
        private double walkingDistance;

        public PetStore(IPetSettings settings, IPetScheduler scheduler)
        {
            this.settings = settings;
            this.scheduler = scheduler;

            message = PetDialogue.Lines(PetDialogueScene.Idle)[0];

            double savedScale = settings.GetDouble(ScaleKey) ?? 1;
            scale = Math.Min(Math.Max(savedScale, 0.6), 1.4);
            layerMode = WindowLayerModes.Parse(settings.GetString(LayerModeKey), WindowLayerMode.Front);
            wanderingEnabled = settings.GetBool(WanderingEnabledKey) ?? true;
            double savedSpeed = settings.GetDouble(WanderSpeedKey) ?? 42;
            wanderSpeed = Math.Min(Math.Max(savedSpeed, 18), 90);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public PetMood Mood
        {
            get { return mood; }
            private set { SetField(ref mood, value, "Mood"); }
        }

        public PetPose Pose
        {
            get { return pose; }
            private set { SetField(ref pose, value, "Pose"); }
        }

        public string Message
        {
            get { return message; }
            private set { SetField(ref message, value, "Message"); }
        }

        public bool ShowsBubble
        {
            get { return showsBubble; }
            private set { SetField(ref showsBubble, value, "ShowsBubble"); }
        }

        public WindowLayerMode LayerMode
        {
            get { return layerMode; }
            private set { SetField(ref layerMode, value, "LayerMode"); }
        }

        public double Scale
        {
            get { return scale; }
            private set { SetField(ref scale, value, "Scale"); }
        }

        public PetActivity Activity
        {
            get { return activity; }
            private set { SetField(ref activity, value, "Activity"); }
        }

        public int WalkingDirection
        {
            get { return walkingDirection; }
            private set { SetField(ref walkingDirection, value, "WalkingDirection"); }
        }

        public bool WanderingEnabled
        {
            get { return wanderingEnabled; }
            private set { SetField(ref wanderingEnabled, value, "WanderingEnabled"); }
        }

        public double WanderSpeed
        {
            get { return wanderSpeed; }
            private set { SetField(ref wanderSpeed, value, "WanderSpeed"); }
        }

        public int AnimationFrame
        {
            get { return animationFrame; }
            private set { SetField(ref animationFrame, value, "AnimationFrame"); }
        }

        public void ReactToTap()
        {
            ReactToTap(null);
        }

        public void ReactToTap(int? randomDialogueIndex)
        {
            InterruptActivity();
            Mood = PetMoods.AllCases[((int)mood + 1) % PetMoods.AllCases.Length];
            switch (mood)
            {
                case PetMood.Curious: Pose = PetPose.Curious; break;
                case PetMood.Pleased: Pose = PetPose.Happy; break;
                default: Pose = PetPose.Idle; break;
            }
            Message = PetDialogue.Line(SceneFor(pose), randomDialogueIndex);
            ShowsBubble = true;
            ScheduleDismiss();
        }

        public void IdleTick()
        {
            IdleTick(null, null);
        }

        public void IdleTick(int? randomIndex, bool? sleeping)
        {
            if (showsBubble) return;
            bool usesSleepingPose = sleeping ?? PetRandom.Next(0, 2) == 1;
            PetDialogueScene scene = usesSleepingPose ? PetDialogueScene.Sleeping : PetDialogueScene.Idle;
            Message = PetDialogue.Line(scene, randomIndex);
            Mood = PetMood.Sleepy;
            Pose = usesSleepingPose ? PetPose.Sleeping : PetPose.Idle;
            ShowsBubble = true;
            ScheduleDismiss();
        }

        public void BeginDrag()
        {
            BeginDrag(null);
        }

        public void BeginDrag(int? randomDialogueIndex)
        {
            if (pose == PetPose.Grabbed) return;
            InterruptActivity();
            Cancel(ref settleWork);
            Cancel(ref dismissWork);
            Pose = PetPose.Grabbed;
            Mood = PetMood.Curious;
            Message = PetDialogue.Line(PetDialogueScene.Grabbed, randomDialogueIndex);
            ShowsBubble = true;
        }

        public void EndDrag()
        {
            EndDrag(null);
        }

        public void EndDrag(int? randomDialogueIndex)
        {
            if (pose != PetPose.Grabbed) return;
            Pose = PetPose.Curious;
            Message = PetDialogue.Line(PetDialogueScene.Released, randomDialogueIndex);
            ScheduleDismiss();
            Cancel(ref settleWork);
            settleWork = scheduler.Schedule(SettleDelay, delegate
            {
                if (pose == PetPose.Grabbed) return;
                Pose = PetPose.Idle;
                Mood = PetMood.Sleepy;
            });
        }

        public void ToggleBubble()
        {
            ShowsBubble = showsBubble == false;
            if (showsBubble) ScheduleDismiss();
        }

        public void SetScale(double newScale)
        {
            Scale = Math.Min(Math.Max(newScale, 0.6), 1.4);
            settings.SetDouble(ScaleKey, scale);
        }

        public void SetLayerMode(WindowLayerMode mode)
        {
            LayerMode = mode;
            settings.SetString(LayerModeKey, WindowLayerModes.RawValue(mode));
        }

        public void SetWanderingEnabled(bool enabled)
        {
            WanderingEnabled = enabled;
            settings.SetBool(WanderingEnabledKey, enabled);
            if (enabled == false) InterruptActivity();
        }

        public void SetWanderSpeed(double speed)
        {
            WanderSpeed = Math.Min(Math.Max(speed, 18), 90);
            settings.SetDouble(WanderSpeedKey, wanderSpeed);
        }

        public void LifestyleTick()
        {
            LifestyleTick(null, null);
        }

        public void LifestyleTick(int? randomEvent, int? randomDialogueIndex)
        {
            if (wanderingEnabled == false) return;
            if (pose == PetPose.Grabbed) return;
            if (activity != PetActivity.Idle) return;

            switch (randomEvent ?? PetRandom.Next(0, 8))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    PerformLifestyle(PetActivity.Walking, randomDialogueIndex);
                    break;
                case 4:
                    PerformLifestyle(PetActivity.DrinkingTea, randomDialogueIndex);
                    break;
                case 5:
                    PerformLifestyle(PetActivity.EatingSnack, randomDialogueIndex);
                    break;
                case 6:
                    Pose = PetPose.Sleeping;
                    Mood = PetMood.Sleepy;
                    Message = PetDialogue.Line(PetDialogueScene.Sleeping, randomDialogueIndex);
                    ShowsBubble = true;
                    ScheduleDismiss();
                    break;
                default:
                    Pose = PetPose.Curious;
                    Mood = PetMood.Curious;
                    Message = PetDialogue.Line(PetDialogueScene.Curious, randomDialogueIndex);
                    ShowsBubble = true;
                    ScheduleDismiss();
                    break;
            }
        }

        public void PerformLifestyle(PetActivity requestedActivity)
        {
            PerformLifestyle(requestedActivity, null);
        }

        public void PerformLifestyle(PetActivity requestedActivity, int? randomDialogueIndex)
        {
            if (wanderingEnabled == false) return;
            if (pose == PetPose.Grabbed) return;
            InterruptActivity();

            switch (requestedActivity)
            {
                case PetActivity.Walking:
                    // Arrival normally ends walking; this timeout is only a safety net
                    // for a window that cannot move or disappears mid-activity.
                    BeginActivity(PetActivity.Walking, WalkTimeout);
                    Pose = PetPose.Curious;
                    Mood = PetMood.Curious;
                    Message = PetDialogue.Line(PetDialogueScene.Walking, randomDialogueIndex);
                    ShowsBubble = true;
                    ScheduleDismiss();
                    break;
                case PetActivity.DrinkingTea:
                    BeginActivity(PetActivity.DrinkingTea, SipDuration);
                    Pose = PetPose.Idle;
                    Mood = PetMood.Pleased;
                    Message = PetDialogue.Line(PetDialogueScene.DrinkingTea, randomDialogueIndex);
                    ShowsBubble = true;
                    ScheduleDismiss();
                    break;
                case PetActivity.EatingSnack:
                    BeginActivity(PetActivity.EatingSnack, SipDuration);
                    Pose = PetPose.Happy;
                    Mood = PetMood.Pleased;
                    Message = PetDialogue.Line(PetDialogueScene.EatingSnack, randomDialogueIndex);
                    ShowsBubble = true;
                    ScheduleDismiss();
                    break;
            }
        }

        public void UpdateWalkingDirection(int direction)
        {
            WalkingDirection = direction < 0 ? -1 : 1;
        }

        public void AdvanceActivityFrame()
        {
            if (activity == PetActivity.Idle) return;
            int[] sequence = PetActivities.FrameSequence(activity);
            animationSequenceIndex = (animationSequenceIndex + 1) % sequence.Length;
            AnimationFrame = sequence[animationSequenceIndex];
        }

        public void AdvanceWalkingFrame(double distance)
        {
            if (activity != PetActivity.Walking) return;
            if (distance <= 0) return;
            walkingDistance += distance;
            double strideLength = 44 * scale;
            double phase = walkingDistance % strideLength / strideLength;
            AnimationFrame = Math.Min((int)(phase * 4), 3);
        }

        public void FinishWalking()
        {
            if (activity != PetActivity.Walking) return;
            InterruptActivity();
            Pose = PetPose.Idle;
            Mood = PetMood.Sleepy;
            Message = PetDialogue.Line(PetDialogueScene.Idle);
            ShowsBubble = true;
            ScheduleDismiss();
        }

        public void SpeakForCurrentState()
        {
            SpeakForCurrentState(null);
        }

        public void SpeakForCurrentState(int? randomDialogueIndex)
        {
            Message = PetDialogue.Line(CurrentDialogueScene, randomDialogueIndex);
            ShowsBubble = true;
            ScheduleDismiss();
        }

        private PetDialogueScene CurrentDialogueScene
        {
            get
            {
                switch (activity)
                {
                    case PetActivity.Walking: return PetDialogueScene.Walking;
                    case PetActivity.DrinkingTea: return PetDialogueScene.DrinkingTea;
                    case PetActivity.EatingSnack: return PetDialogueScene.EatingSnack;
                    default: return SceneFor(pose);
                }
            }
        }

        private static PetDialogueScene SceneFor(PetPose pose)
        {
            switch (pose)
            {
                case PetPose.Curious: return PetDialogueScene.Curious;
                case PetPose.Happy: return PetDialogueScene.Happy;
                case PetPose.Sleeping: return PetDialogueScene.Sleeping;
                case PetPose.Grabbed: return PetDialogueScene.Grabbed;
                default: return PetDialogueScene.Idle;
            }
        }

        private void ScheduleDismiss()
        {
            Cancel(ref dismissWork);
            dismissWork = scheduler.Schedule(DismissDelay, delegate
            {
                ShowsBubble = false;
                Mood = PetMood.Sleepy;
                if (pose != PetPose.Grabbed) Pose = PetPose.Idle;
            });
        }

        private void BeginActivity(PetActivity newActivity, TimeSpan duration)
        {
            Cancel(ref activityWork);
            Activity = newActivity;
            animationSequenceIndex = 0;
            walkingDistance = 0;
            AnimationFrame = PetActivities.FrameSequence(newActivity)[0];
            activityWork = scheduler.Schedule(duration, delegate
            {
                if (pose == PetPose.Grabbed) return;
                Activity = PetActivity.Idle;
                Pose = PetPose.Idle;
                Mood = PetMood.Sleepy;
            });
        }

        private void InterruptActivity()
        {
            Cancel(ref activityWork);
            Activity = PetActivity.Idle;
            animationSequenceIndex = 0;
            walkingDistance = 0;
            AnimationFrame = 0;
        }

        private static void Cancel(ref IDisposable work)
        {
            if (work == null) return;
            work.Dispose();
            work = null;
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
