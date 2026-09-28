using OneApproval.Plugins.Common;
using OneApproval.Plugins.Constants;
using Xunit;

namespace OneApproval.Plugins.Tests.Common
{
    public class FlowHelperTests
    {
        [Theory]
        [InlineData(OneApprovalFlow.Carc, true)]
        [InlineData(OneApprovalFlow.Coe, true)]
        [InlineData(OneApprovalFlow.Sma, true)]
        [InlineData(OneApprovalFlow.Unrouted, false)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(999999, false)]
        public void IsSupportedTargetFlow_ValidatesCorrectly(int flow, bool expected)
        {
            bool actual = FlowHelper.IsSupportedTargetFlow(flow);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(OneApprovalFlow.Carc, "CARC")]
        [InlineData(OneApprovalFlow.Coe, "CoE")]
        [InlineData(OneApprovalFlow.Sma, "SMA")]
        [InlineData(OneApprovalFlow.Unrouted, "Unrouted")]
        [InlineData(99999, "Unknown Flow")]
        [InlineData(0, "Unknown Flow")]
        [InlineData(-1, "Unknown Flow")]
        public void GetFlowLabel_ReturnsExpectedLabel(int flow, string expectedLabel)
        {
            string actual = FlowHelper.GetFlowLabel(flow);
            Assert.Equal(expectedLabel, actual);
        }
    }
}
