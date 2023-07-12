using FluentAssertions;

using StepWise.Prose.Model;


namespace StepWise.Prose.Test;

public static class TestUtils {
    public static void ist(bool @bool) =>
        @bool.Should().BeTrue();
    public static void ist<A, B>(A a, B b) =>
        a.Should().Be(b);
    public static void ist<A, B>(A a, B b, Func<A, B, bool> f) =>
        f(a, b).Should().BeTrue();

    public static bool eq(Node a, Node b) =>
        a.Eq(b);

    public static void ist<A>(A? a) =>
        a.Should().NotBeNull();

    public static void istThrows(Action f) =>
        f.Should().Throw<Exception>();
}