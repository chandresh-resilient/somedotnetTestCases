using OneApproval.Plugins.Common;
using OneApproval.Plugins.Constants;
using Xunit;

namespace OneApproval.Plugins.Tests.Common
{
    public class RiskMapperTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void MapRiskTextToRoutingRisk_WhenNullOrWhitespace_ReturnsNull(string input)
        {
            int? result = RiskMapper.MapRiskTextToRoutingRisk(input);
            Assert.Null(result);
        }

        [Theory]
        [InlineData("Medium", RoutingRisk.Medium)]
        [InlineData("medium", RoutingRisk.Medium)]
        [InlineData("  MEDIUM  ", RoutingRisk.Medium)]
        [InlineData("Increased", RoutingRisk.Increased)]
        [InlineData("increased", RoutingRisk.Increased)]
        [InlineData("  INCREASED  ", RoutingRisk.Increased)]
        [InlineData("Unacceptable", RoutingRisk.Unacceptable)]
        [InlineData("unacceptable", RoutingRisk.Unacceptable)]
        [InlineData("  UNACCEPTABLE  ", RoutingRisk.Unacceptable)]
        public void MapRiskTextToRoutingRisk_WhenValidRisk_ReturnsExpectedOptionSetValue(string input, int expected)
        {
            int? result = RiskMapper.MapRiskTextToRoutingRisk(input);
            Assert.NotNull(result);
            Assert.Equal(expected, result.Value);
        }

        [Theory]
        [InlineData("Neutral")]
        [InlineData("High")]
        [InlineData("Low")]
        [InlineData("Critical")]
        [InlineData("UnknownRisk")]
        public void MapRiskTextToRoutingRisk_WhenNeutralOrUnknown_ReturnsNull(string input)
        {
            int? result = RiskMapper.MapRiskTextToRoutingRisk(input);
            Assert.Null(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MapRiskTextToApprovalCaseRiskScore_WhenNullOrWhitespace_ReturnsNull(string input)
        {
            int? result = RiskMapper.MapRiskTextToApprovalCaseRiskScore(input);
            Assert.Null(result);
        }

        [Theory]
        [InlineData("Neutral", ApprovalCaseRiskScore.Neutral)]
        [InlineData("neutral", ApprovalCaseRiskScore.Neutral)]
        [InlineData("  NEUTRAL  ", ApprovalCaseRiskScore.Neutral)]
        [InlineData("Medium", ApprovalCaseRiskScore.Medium)]
        [InlineData("medium", ApprovalCaseRiskScore.Medium)]
        [InlineData("  MEDIUM  ", ApprovalCaseRiskScore.Medium)]
        [InlineData("Increased", ApprovalCaseRiskScore.Increased)]
        [InlineData("increased", ApprovalCaseRiskScore.Increased)]
        [InlineData("  INCREASED  ", ApprovalCaseRiskScore.Increased)]
        [InlineData("Unacceptable", ApprovalCaseRiskScore.Unacceptable)]
        [InlineData("unacceptable", ApprovalCaseRiskScore.Unacceptable)]
        [InlineData("  UNACCEPTABLE  ", ApprovalCaseRiskScore.Unacceptable)]
        public void MapRiskTextToApprovalCaseRiskScore_WhenValidRisk_ReturnsExpectedOptionSetValue(string input, int expected)
        {
            int? result = RiskMapper.MapRiskTextToApprovalCaseRiskScore(input);
            Assert.NotNull(result);
            Assert.Equal(expected, result.Value);
        }

        [Theory]
        [InlineData("High")]
        [InlineData("Low")]
        [InlineData("Extreme")]
        [InlineData("123900000")]
        public void MapRiskTextToApprovalCaseRiskScore_WhenUnknown_ReturnsNull(string input)
        {
            int? result = RiskMapper.MapRiskTextToApprovalCaseRiskScore(input);
            Assert.Null(result);
        }
    }
}
