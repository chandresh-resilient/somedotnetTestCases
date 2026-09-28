using System;
using Microsoft.Xrm.Sdk;

namespace OneApproval.Plugins.Models
{
    /// <summary>
    /// In-memory data transfer object containing all confirmed source and derived routing values.
    /// Loaded once before rule matching to ensure high performance and consistency.
    /// </summary>
    public sealed class RoutingContextData
    {
        public Entity OneKycCase { get; set; }
        public Entity Party { get; set; }
        public EntityReference ClientUnit { get; set; }
        public string ClientUnitName { get; set; }
        public EntityReference Group { get; set; }

        public string OneKycCaseNumber { get; set; }
        public int ClientType { get; set; }
        public int OneKycCaseType { get; set; }
        public int OneKycStage { get; set; }
        public string CurrentStageName { get; set; }

        public string RiskText { get; set; }
        public int RoutingRisk { get; set; }
        public int ApprovalCaseRiskScore { get; set; }

        public int RoutingScenario { get; set; }
        public int GroupComposition { get; set; }

        public string PartyName { get; set; }
        public string ClientNumber { get; set; }
        public int? SourceSystem { get; set; }
        public DateTime? NextReviewDate { get; set; }
    }
}
