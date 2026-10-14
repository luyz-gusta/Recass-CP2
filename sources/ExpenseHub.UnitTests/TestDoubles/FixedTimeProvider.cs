using System;

namespace ExpenseHub.UnitTests.TestDoubles;

/// <summary>
/// Relógio controlado pelos testes. Por padrão marca 01/10/2026 12:00 UTC.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FixedTimeProvider()
        : this(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public DateTime UtcNow => _utcNow.UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(_utcNow.UtcDateTime);

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public void Advance(TimeSpan delta)
    {
        _utcNow = _utcNow.Add(delta);
    }
}
