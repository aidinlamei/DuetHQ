using Microsoft.Extensions.Time.Testing;

namespace DuetHQ.TestSupport;

public static class FakeTime
{
    /// <summary>A <see cref="FakeTimeProvider"/> standing at <paramref name="instant"/>. It never moves on its own.</summary>
    // Normalised to UTC on purpose: FakeTimeProvider keeps the offset of its start value, and TimeProvider.GetLocalNow derives the
    // local time from GetUtcNow().Ticks, so a start with a non-zero offset makes GetLocalNow() wrong by exactly that offset.
    public static FakeTimeProvider At(DateTimeOffset instant) => new(instant.ToUniversalTime());

    /// <summary>
    /// A <see cref="FakeTimeProvider"/> whose local zone is <paramref name="timeZoneId"/> and whose clock reads
    /// <paramref name="local"/> there. A local time that does not exist (DST gap) or occurs twice (DST overlap) is rejected
    /// instead of guessed: state such instants explicitly with <see cref="At"/>.
    /// </summary>
    public static FakeTimeProvider AtLocal(DateTime local, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(unspecified))
        {
            throw new ArgumentException($"{unspecified:O} does not exist in {timeZoneId} (DST gap). Use FakeTime.At with an explicit instant.", nameof(local));
        }

        if (zone.IsAmbiguousTime(unspecified))
        {
            throw new ArgumentException($"{unspecified:O} occurs twice in {timeZoneId} (DST overlap). Use FakeTime.At with an explicit instant.", nameof(local));
        }

        var provider = At(new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified)));
        provider.SetLocalTimeZone(zone);
        return provider;
    }
}
