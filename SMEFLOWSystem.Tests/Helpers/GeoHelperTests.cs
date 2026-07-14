using SMEFLOWSystem.Application.Helpers;

namespace SMEFLOWSystem.Tests.Helpers;

public class GeoHelperTests
{
    [Fact]
    public void DistanceInMeters_ReturnsZero_WhenCoordinatesAreIdentical()
    {
        var distance = GeoHelper.DistanceInMeters(10.762622, 106.660172, 10.762622, 106.660172);

        Assert.Equal(0, distance, precision: 6);
    }

    [Fact]
    public void DistanceInMeters_ReturnsExpectedDistance_ForKnownCoordinates()
    {
        var distance = GeoHelper.DistanceInMeters(10.762622, 106.660172, 10.776889, 106.700806);

        Assert.InRange(distance, 4_700, 4_750);
    }
}
