using OneApproval.Plugins.Constants;

namespace OneApproval.Plugins.Common
{
    /// <summary>
    /// Helpers for OneKYC stage validations.
    /// </summary>
    public static class StageHelper
    {
        /// <summary>
        /// Returns true only for OneKYC stages that should trigger routing evaluation.
        /// </summary>
        public static bool IsRoutingTriggerStage(int stage)
        {
            return stage == OneKycStage.MediumApproval ||
                   stage == OneKycStage.IncreasedApproval ||
                   stage == OneKycStage.ComplianceAdvice ||
                   stage == OneKycStage.CarcApproval;
        }
    }
}
