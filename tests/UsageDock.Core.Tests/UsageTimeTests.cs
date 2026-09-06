using System.Globalization;
using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class UsageTimeTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-09-06T10:00:00Z");
    [Theory]
    [InlineData(8820,"za 6 dni 3 godz.")]
    [InlineData(8641,"za 6 dni 1 min")]
    [InlineData(8640,"za 6 dni")]
    [InlineData(1440,"za 1 dzień")]
    [InlineData(134,"za 2 godz. 14 min")]
    [InlineData(45,"za 45 min")]
    [InlineData(1,"za 1 min")]
    public void RemainingTimeDoesNotRoundUpDays(int minutes,string expected)=>Assert.Equal(expected,UsageTime.Relative(Now.AddMinutes(minutes),Now));
    [Fact] public void PartialMinuteIsExplicit()=>Assert.Equal("za <1 min",UsageTime.Relative(Now.AddSeconds(59),Now));
    [Fact] public void SecondsDoNotRoundIntoExtraMinute()=>Assert.Equal("za 1 min",UsageTime.Relative(Now.AddSeconds(119),Now));
    [Fact] public void MissingTargetStaysUnknown()=>Assert.Equal("Brak terminu",UsageTime.Relative(null,Now));
    [Theory][InlineData(0)][InlineData(-1)]
    public void ExpiredTargetRequestsFreshRead(int seconds)=>Assert.Equal("Termin minął — odśwież odczyt",UsageTime.Relative(Now.AddSeconds(seconds),Now));
    [Fact] public void AbsoluteDateUsesRequestedZoneAndPolishMonth()
    {
        var zone=TimeZoneInfo.CreateCustomTimeZone("Fixture UTC+2",TimeSpan.FromHours(2),"Fixture UTC+2","Fixture UTC+2");
        Assert.Equal("12 wrz 2026, 14:30 (UTC+02:00)",UsageTime.Absolute(DateTimeOffset.Parse("2026-09-12T12:30:00Z"),zone));
    }
    [Fact] public void AbsoluteDateUsesTargetDateDstOffset()
    {
        var start=TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1,1,1,2,0,0),3,29);
        var end=TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1,1,1,3,0,0),10,25);
        var rule=TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026,1,1),new DateTime(2026,12,31),TimeSpan.FromHours(1),start,end);
        var zone=TimeZoneInfo.CreateCustomTimeZone("Fixture DST",TimeSpan.FromHours(1),"Fixture","Standard","Summer",new[]{rule});
        Assert.Equal("29 mar 2026, 01:30 (UTC+01:00)",UsageTime.Absolute(DateTimeOffset.Parse("2026-03-29T00:30:00Z"),zone));
        Assert.Equal("29 mar 2026, 03:30 (UTC+02:00)",UsageTime.Absolute(DateTimeOffset.Parse("2026-03-29T01:30:00Z"),zone));
    }
    [Fact] public void FullCombinesExactDateAndCountdown()=>Assert.Equal("6 wrz 2026, 10:45 (UTC+00:00) · za 45 min",UsageTime.Full(Now.AddMinutes(45),Now,TimeZoneInfo.Utc));
    [Fact] public void MissingFullDateDoesNotInventExpiry()=>Assert.Equal("Brak terminu",UsageTime.Full(null,Now,TimeZoneInfo.Utc));
    [Fact] public void PresentationDoesNotDependOnCurrentCulture()
    {
        var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("en-US");Assert.Contains("wrz",UsageTime.Absolute(Now,TimeZoneInfo.Utc));}finally{CultureInfo.CurrentCulture=previous;}
    }
}
