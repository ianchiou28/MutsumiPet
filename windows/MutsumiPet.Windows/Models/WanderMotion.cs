using System;

namespace MutsumiPet.Models
{
    public enum WanderMotionStepKind
    {
        Paused,
        Move,
        Arrived
    }

    public struct WanderMotionStep : IEquatable<WanderMotionStep>
    {
        private const double Epsilon = 1e-9;

        public readonly WanderMotionStepKind Kind;
        public readonly double X;

        private WanderMotionStep(WanderMotionStepKind kind, double x)
        {
            Kind = kind;
            X = x;
        }

        public static WanderMotionStep Paused()
        {
            return new WanderMotionStep(WanderMotionStepKind.Paused, 0);
        }

        public static WanderMotionStep Move(double toX)
        {
            return new WanderMotionStep(WanderMotionStepKind.Move, toX);
        }

        public static WanderMotionStep Arrived(double x)
        {
            return new WanderMotionStep(WanderMotionStepKind.Arrived, x);
        }

        public bool Equals(WanderMotionStep other)
        {
            if (Kind != other.Kind) return false;
            if (Kind == WanderMotionStepKind.Paused) return true;
            return Math.Abs(X - other.X) < Epsilon;
        }

        public override bool Equals(object obj)
        {
            return obj is WanderMotionStep && Equals((WanderMotionStep)obj);
        }

        public override int GetHashCode()
        {
            return (int)Kind;
        }

        public override string ToString()
        {
            if (Kind == WanderMotionStepKind.Paused) return "paused";
            return Kind.ToString().ToLowerInvariant() + "(" + X.ToString("0.###") + ")";
        }
    }

    /// Picks a random horizontal target inside the usable screen band and walks
    /// toward it one step at a time. Kept free of any window/UI type so the walk
    /// cycle can be unit tested without a desktop.
    public sealed class WanderMotion
    {
        private double? targetX;
        private int lastAppliedDirection = -1;

        public double? TargetX
        {
            get { return targetX; }
        }

        public int LastAppliedDirection
        {
            get { return lastAppliedDirection; }
        }

        public void ResetTarget()
        {
            targetX = null;
        }

        public void EnsureTarget(double currentX, double minX, double maxX)
        {
            EnsureTarget(currentX, minX, maxX, null, null);
        }

        public void EnsureTarget(
            double currentX,
            double minX,
            double maxX,
            int? preferredDirection,
            double? requestedDistance)
        {
            if (targetX != null) return;
            if (maxX <= minX) return;

            double current = Math.Min(Math.Max(currentX, minX), maxX);
            double edgeThreshold = Math.Min(140, maxX - minX);

            int direction;
            if (preferredDirection != null)
            {
                direction = preferredDirection.Value < 0 ? -1 : 1;
            }
            else if (current - minX < edgeThreshold)
            {
                direction = 1;
            }
            else if (maxX - current < edgeThreshold)
            {
                direction = -1;
            }
            else
            {
                direction = -lastAppliedDirection;
            }

            if (AvailableDistance(direction, current, minX, maxX) < 2)
            {
                direction *= -1;
            }

            double available = AvailableDistance(direction, current, minX, maxX);
            if (available <= 0) return;

            double maximum = Math.Min(320, available);
            double minimum = Math.Min(140, maximum);
            double requested = requestedDistance != null
                ? requestedDistance.Value
                : PetRandom.NextDouble(minimum, maximum);
            double distance = Math.Min(Math.Max(requested, 0), maximum);

            targetX = Math.Min(Math.Max(current + direction * distance, minX), maxX);
        }

        public WanderMotionStep NextStep(double currentX, double maximumDistance)
        {
            if (targetX == null) return WanderMotionStep.Paused();

            double target = targetX.Value;
            double difference = target - currentX;
            if (Math.Abs(difference) < 2)
            {
                targetX = null;
                return WanderMotionStep.Arrived(target);
            }

            double direction = difference < 0 ? -1 : 1;
            double distance = Math.Min(Math.Abs(difference), Math.Max(0.5, maximumDistance));
            return WanderMotionStep.Move(currentX + direction * distance);
        }

        public void RecordApplied(double deltaX)
        {
            if (Math.Abs(deltaX) <= 0.01) return;
            lastAppliedDirection = deltaX < 0 ? -1 : 1;
        }

        private static double AvailableDistance(int direction, double current, double minX, double maxX)
        {
            return direction < 0 ? current - minX : maxX - current;
        }
    }
}
