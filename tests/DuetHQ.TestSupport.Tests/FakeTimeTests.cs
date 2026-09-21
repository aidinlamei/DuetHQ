namespace DuetHQ.TestSupport.Tests;

// Time zones are IANA ids: they resolve on Windows through ICU and natively on Linux CI.
// Tehran has had no DST since 2022, so it proves id resolution and offset only. Europe/Berlin exercises DST offsets and the rejected gap/overlap times.
public sealed class FakeTimeTests
{
    [Fact]
    public void At_Instant_StandsAtThatInstant()
    {
        var instant = new DateTimeOffset(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);

        var provider = FakeTime.At(instant);

        provider.GetUtcNow().ShouldBe(instant);
    }

    [Fact]
    public void At_InstantWithNonUtcOffset_IsNormalisedToUtc()
    {
        // Regression: FakeTimeProvider keeps the start value's offset, which made GetLocalNow() wrong by that offset.
        var provider = FakeTime.At(new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.FromMinutes(210)));

        provider.GetUtcNow().Offset.ShouldBe(TimeSpan.Zero);
        provider.GetUtcNow().ShouldBe(new DateTimeOffset(2026, 9, 21, 8, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void At_Instant_NeverMovesOnItsOwn()
    {
        var provider = FakeTime.At(new DateTimeOffset(2026, 9, 21, 8, 30, 0, TimeSpan.Zero));

        var first = provider.GetUtcNow();
        var second = provider.GetUtcNow();

        second.ShouldBe(first);
    }

    [Fact]
    public void AtLocal_TehranNoon_IsThreeAndAHalfHoursBeforeInUtc()
    {
        var provider = FakeTime.AtLocal(new DateTime(2026, 9, 21, 12, 0, 0), "Asia/Tehran");

        provider.GetUtcNow().ShouldBe(new DateTimeOffset(2026, 9, 21, 8, 30, 0, TimeSpan.Zero));
        provider.GetLocalNow().DateTime.ShouldBe(new DateTime(2026, 9, 21, 12, 0, 0));
        provider.LocalTimeZone.Id.ShouldBe("Asia/Tehran");
    }

    [Theory]
    [InlineData(2026, 7, 1, 10)] // CEST, UTC+2
    [InlineData(2026, 1, 15, 11)] // CET, UTC+1
    public void AtLocal_BerlinNoon_UsesTheOffsetInForceOnThatDate(int year, int month, int day, int expectedUtcHour)
    {
        var provider = FakeTime.AtLocal(new DateTime(year, month, day, 12, 0, 0), "Europe/Berlin");

        provider.GetUtcNow().ShouldBe(new DateTimeOffset(year, month, day, expectedUtcHour, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AtLocal_LocalTimeInDstGap_Throws()
    {
        // Europe/Berlin springs forward on 2026-03-29: 02:00 -> 03:00, so 02:30 does not exist.
        Should.Throw<ArgumentException>(() => FakeTime.AtLocal(new DateTime(2026, 3, 29, 2, 30, 0), "Europe/Berlin"));
    }

    [Fact]
    public void AtLocal_LocalTimeInDstOverlap_Throws()
    {
        // Europe/Berlin falls back on 2026-10-25: 03:00 -> 02:00, so 02:30 occurs twice.
        Should.Throw<ArgumentException>(() => FakeTime.AtLocal(new DateTime(2026, 10, 25, 2, 30, 0), "Europe/Berlin"));
    }

    [Fact]
    public void AtLocal_UnknownZone_Throws()
    {
        Should.Throw<TimeZoneNotFoundException>(() => FakeTime.AtLocal(new DateTime(2026, 1, 1), "Mars/Olympus_Mons"));
    }
}
