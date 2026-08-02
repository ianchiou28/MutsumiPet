using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace MutsumiPet.Tests
{
    public sealed class TestFailure : Exception
    {
        public TestFailure(string message) : base(message)
        {
        }
    }

    /// A deliberately tiny xUnit stand-in. The macOS build gets XCTest for free via
    /// `swift test`; on Windows a self-contained runner keeps `build_windows.ps1
    /// -Verify` dependency-free.
    public static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (condition == false) throw new TestFailure(message);
        }

        public static void False(bool condition, string message)
        {
            True(condition == false, message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual)) return;
            throw new TestFailure(string.Format(
                "{0}\n      expected: {1}\n      actual:   {2}",
                message, Describe(expected), Describe(actual)));
        }

        public static void Close(double expected, double actual, double tolerance, string message)
        {
            if (Math.Abs(expected - actual) <= tolerance) return;
            throw new TestFailure(string.Format(
                "{0}\n      expected: {1} (+/- {2})\n      actual:   {3}",
                message, expected, tolerance, actual));
        }

        public static void GreaterThan(double actual, double threshold, string message)
        {
            if (actual > threshold) return;
            throw new TestFailure(string.Format(
                "{0}\n      expected: greater than {1}\n      actual:   {2}",
                message, threshold, actual));
        }

        public static void InRange(int actual, int minimum, int maximum, string message)
        {
            if (actual >= minimum && actual <= maximum) return;
            throw new TestFailure(string.Format(
                "{0}\n      expected: {1}..{2}\n      actual:   {3}",
                message, minimum, maximum, actual));
        }

        private static string Describe(object value)
        {
            if (value == null) return "<null>";
            if (value is string) return "\"" + value + "\"";
            return value.ToString();
        }
    }

    public static class TestRunner
    {
        [STAThread]
        public static int Main()
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch (IOException)
            {
                // A redirected console can reject the encoding change; the test
                // names are ASCII either way.
            }

            List<MethodInfo> tests = Discover();
            int failed = 0;

            foreach (MethodInfo test in tests)
            {
                string name = test.DeclaringType.Name + "." + test.Name;
                try
                {
                    test.Invoke(null, null);
                    Console.WriteLine("  ok    " + name);
                }
                catch (TargetInvocationException exception)
                {
                    failed++;
                    Exception inner = exception.InnerException ?? exception;
                    Console.WriteLine("  FAIL  " + name);
                    Console.WriteLine("        " + inner.Message.Replace("\n", "\n        "));
                    if ((inner is TestFailure) == false)
                    {
                        Console.WriteLine("        " + inner.GetType().Name);
                        Console.WriteLine(inner.StackTrace);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine(string.Format(
                "{0} tests, {1} passed, {2} failed", tests.Count, tests.Count - failed, failed));
            return failed == 0 ? 0 : 1;
        }

        private static List<MethodInfo> Discover()
        {
            var found = new List<MethodInfo>();
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.Name.EndsWith("Tests") == false) continue;
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name.StartsWith("Test") == false) continue;
                    if (method.GetParameters().Length != 0) continue;
                    found.Add(method);
                }
            }

            found.Sort(delegate(MethodInfo left, MethodInfo right)
            {
                int byType = string.CompareOrdinal(left.DeclaringType.Name, right.DeclaringType.Name);
                return byType != 0 ? byType : string.CompareOrdinal(left.Name, right.Name);
            });
            return found;
        }
    }
}
