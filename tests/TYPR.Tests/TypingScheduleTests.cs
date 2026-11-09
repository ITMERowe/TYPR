using Xunit;

namespace TYPR.Tests;

public sealed class TypingScheduleTests
{
    [Fact]
    public void OnTime_WaitsUntilTheDeadline()
    {
        double due = 50;
        Assert.Equal(50, TypingSchedule.NextWait(ref due, nowMs: 0, slotMs: 50));
        Assert.Equal(50, due);
    }

    [Fact]
    public void SlightlyLate_PaysTheOvershootBackOnTheNextKey()
    {
        // The last wake-up came 10 ms late: the next wait is 10 ms shorter, not the full slot.
        double due = 100;
        Assert.Equal(40, TypingSchedule.NextWait(ref due, nowMs: 60, slotMs: 50));
    }

    [Fact]
    public void FarBehind_RepaysAtMostOneSlotAndNeverBursts()
    {
        // A 2 s stall must not turn into dozens of back-to-back keys.
        double due = 100;
        Assert.Equal(1, TypingSchedule.NextWait(ref due, nowMs: 2100, slotMs: 50));
        Assert.Equal(2050, due);

        // The next key is then on the normal pace again.
        due += 50;
        Assert.Equal(1, TypingSchedule.NextWait(ref due, nowMs: 2101, slotMs: 50));
        due += 50;
        Assert.Equal(48, TypingSchedule.NextWait(ref due, nowMs: 2102, slotMs: 50));
    }

    [Fact]
    public void AlwaysWaitsAtLeastOneMillisecond()
    {
        double due = 10;
        Assert.Equal(1, TypingSchedule.NextWait(ref due, nowMs: 10.2, slotMs: 3));
    }
}
