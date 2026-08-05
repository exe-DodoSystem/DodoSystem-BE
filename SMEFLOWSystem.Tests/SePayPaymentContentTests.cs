using SMEFLOWSystem.Application.Helpers;

namespace SMEFLOWSystem.Tests;

public sealed class SePayPaymentContentTests
{
    [Fact]
    public void BuildTransferContent_RemovesBankUnsafePunctuation()
    {
        var content = SePayPaymentContent.BuildTransferContent("DODO", "SUB-50908211");

        Assert.Equal("DODO SUB50908211", content);
    }

    [Theory]
    [InlineData("DODO SUB-50908211", "SUB-50908211")]
    [InlineData("DODO SUB50908211", "SUB-50908211")]
    [InlineData("DODO SUB 50908211", "SUB-50908211")]
    [InlineData("DODOSUB50908211", "SUB-50908211")]
    [InlineData("FT123 DODO.SUB_50908211 payment", "SUB-50908211")]
    public void TryExtractBillingOrderNumber_AcceptsBankNormalizedContent(
        string transferContent,
        string expectedOrderNumber)
    {
        var found = SePayPaymentContent.TryExtractBillingOrderNumber(
            transferContent,
            out var orderNumber);

        Assert.True(found);
        Assert.Equal(expectedOrderNumber, orderNumber);
    }
}
