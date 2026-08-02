using System;

namespace MutsumiPet.Models
{
    /// A single seeded source shared by dialogue picking and wander targets, so the
    /// pet never repeats the same sequence just because two `Random` instances were
    /// created within the same clock tick.
    public static class PetRandom
    {
        private static readonly object Gate = new object();
        private static readonly Random Source = new Random();

        public static int Next(int minValue, int maxValueExclusive)
        {
            lock (Gate)
            {
                return Source.Next(minValue, maxValueExclusive);
            }
        }

        /// Inclusive on both ends, matching Swift's `Double.random(in: a...b)`.
        public static double NextDouble(double minValue, double maxValue)
        {
            if (maxValue <= minValue) return minValue;
            lock (Gate)
            {
                return minValue + Source.NextDouble() * (maxValue - minValue);
            }
        }
    }
}
