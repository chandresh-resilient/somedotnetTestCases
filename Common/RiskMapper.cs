using System;
using OneApproval.Plugins.Constants;

namespace OneApproval.Plugins.Common
{
    /// <summary>
    /// Mappers for converting OneKYC Risk Score text into target OptionSet choice values.
    /// </summary>
    public static class RiskMapper
    {
        /// <summary>
        /// Converts OneKYC text risk into the local Routing Rule Routing Risk Choice value.
        /// Returns null if blank or unsupported.
        /// </summary>
        public static int? MapRiskTextToRoutingRisk(string riskText)
        {
            if (string.IsNullOrWhiteSpace(riskText))
            {
                return null;
            }

            string normalized = riskText.Trim();

            if (string.Equals(normalized, "Medium", StringComparison.OrdinalIgnoreCase))
            {
                return RoutingRisk.Medium;
            }

            if (string.Equals(normalized, "Increased", StringComparison.OrdinalIgnoreCase))
            {
                return RoutingRisk.Increased;
            }

            if (string.Equals(normalized, "Unacceptable", StringComparison.OrdinalIgnoreCase))
            {
                return RoutingRisk.Unacceptable;
            }

            return null;
        }

        /// <summary>
        /// Converts OneKYC text risk into the target KYC Approval Case Risk Score Choice value.
        /// Returns null if blank or unsupported.
        /// </summary>
        public static int? MapRiskTextToApprovalCaseRiskScore(string riskText)
        {
            if (string.IsNullOrWhiteSpace(riskText))
            {
                return null;
            }

            string normalized = riskText.Trim();

            if (string.Equals(normalized, "Neutral", StringComparison.OrdinalIgnoreCase))
            {
                return ApprovalCaseRiskScore.Neutral;
            }

            if (string.Equals(normalized, "Medium", StringComparison.OrdinalIgnoreCase))
            {
                return ApprovalCaseRiskScore.Medium;
            }

            if (string.Equals(normalized, "Increased", StringComparison.OrdinalIgnoreCase))
            {
                return ApprovalCaseRiskScore.Increased;
            }

            if (string.Equals(normalized, "Unacceptable", StringComparison.OrdinalIgnoreCase))
            {
                return ApprovalCaseRiskScore.Unacceptable;
            }

            return null;
        }
    }
}
