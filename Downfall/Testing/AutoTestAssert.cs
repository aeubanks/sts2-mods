namespace Downfall.Testing;

internal static class AutoTestAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"Assertion failed: {message}");
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Assertion failed ({message}): expected '{expected}', got '{actual}'");
    }

    public static T NotNull<T>(T? value, string message) where T : class
    {
        if (value is null)
            throw new Exception($"Assertion failed (not null): {message}");
        return value;
    }
}
