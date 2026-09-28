using OneApproval.Plugins.Constants;

namespace OneApproval.Plugins.Common
{
    /// <summary>
    /// Helpers for OneApproval flow choices and display labels.
    /// </summary>
    public static class FlowHelper
    {
        /// <summary>
        /// Validates that the flow is one of the supported target flows (CARC, CoE, SMA).
        /// </summary>
        public static bool IsSupportedTargetFlow(int flow)
        {
            return flow == OneApprovalFlow.Carc ||
                   flow == OneApprovalFlow.Coe ||
                   flow == OneApprovalFlow.Sma;
        }

        /// <summary>
        /// Returns the standard display label for a OneApproval flow Choice value.
        /// </summary>
        public static string GetFlowLabel(int flow)
        {
            switch (flow)
            {
                case OneApprovalFlow.Carc:
                    return "CARC";
                case OneApprovalFlow.Coe:
                    return "CoE";
                case OneApprovalFlow.Sma:
                    return "SMA";
                case OneApprovalFlow.Unrouted:
                    return "Unrouted";
                default:
                    return "Unknown Flow";
            }
        }
    }
}
