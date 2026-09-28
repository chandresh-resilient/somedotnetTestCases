using OneApproval.Plugins.Common;
using OneApproval.Plugins.Constants;
using Xunit;

namespace OneApproval.Plugins.Tests.Common
{
    public class StageHelperTests
    {
        [Theory]
        [InlineData(OneKycStage.MediumApproval, true)]
        [InlineData(OneKycStage.IncreasedApproval, true)]
        [InlineData(OneKycStage.ComplianceAdvice, true)]
        [InlineData(OneKycStage.CarcApproval, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(745460000, false)]
        [InlineData(745460008, false)]
        [InlineData(745460012, false)]
        [InlineData(745460014, false)]
        public void IsRoutingTriggerStage_EvaluatesCorrectly(int stage, bool expected)
        {
            bool actual = StageHelper.IsRoutingTriggerStage(stage);
            Assert.Equal(expected, actual);
        }
    }
}
