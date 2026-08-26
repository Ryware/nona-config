using Nona.Benchmarks;

namespace Nona.Benchmarks.Tests;

public class ReplicationMetricsTests
{
    [Test]
    public async Task Calculate_UsesTheFullSampleCount()
    {
        var onePercentStale = ReplicationMetrics.Calculate(100, 99);
        var halfStale = ReplicationMetrics.Calculate(100, 50);
        var fullyStale = ReplicationMetrics.Calculate(100, 0);

        await Assert.That(onePercentStale.StaleReads).IsEqualTo(1);
        await Assert.That(onePercentStale.StaleReadRatePercent).IsEqualTo(1d);
        await Assert.That(halfStale.StaleReads).IsEqualTo(50);
        await Assert.That(halfStale.StaleReadRatePercent).IsEqualTo(50d);
        await Assert.That(fullyStale.StaleReads).IsEqualTo(100);
        await Assert.That(fullyStale.StaleReadRatePercent).IsEqualTo(100d);
    }

    [Test]
    public async Task Calculate_ClampsObservedReadsToTheSampleRange()
    {
        var overObserved = ReplicationMetrics.Calculate(100, 101);
        var negativeObserved = ReplicationMetrics.Calculate(100, -1);

        await Assert.That(overObserved.StaleReads).IsEqualTo(0);
        await Assert.That(overObserved.StaleReadRatePercent).IsEqualTo(0d);
        await Assert.That(negativeObserved.StaleReads).IsEqualTo(100);
        await Assert.That(negativeObserved.StaleReadRatePercent).IsEqualTo(100d);
    }
}
