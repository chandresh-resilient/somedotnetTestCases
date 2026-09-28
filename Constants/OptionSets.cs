namespace OneApproval.Plugins.Constants
{
    /// <summary>
    /// Values from the OneKYC global Choice aab_stagename.
    /// </summary>
    public static class OneKycStage
    {
        public const int MediumApproval = 745460009;
        public const int ComplianceAdvice = 745460010;
        public const int IncreasedApproval = 745460011;
        public const int CarcApproval = 745460013;
    }

    /// <summary>
    /// Values from the OneKYC global Choice aab_partytype.
    /// </summary>
    public static class OneKycPartyType
    {
        public const int BusinessClient = 1;
        public const int NaturalPerson = 2;
    }

    /// <summary>
    /// Role group values used on aab_caseowner.aab_rolegroup.
    /// </summary>
    public static class OneKycRoleGroup
    {
        public const int CaseAnalyst = 958630000;
    }

    /// <summary>
    /// Values from the Routing Scenario Choice on ka_routingrule.
    /// </summary>
    public static class RoutingScenario
    {
        public const int SingleBcNumber = 123900001;
        public const int LogicalGroup = 123900002;
    }

    /// <summary>
    /// Values from the Group Composition Choice on ka_routingrule.
    /// </summary>
    public static class GroupComposition
    {
        public const int NotApplicable = 123900000;
        public const int NpOnly = 123900001;
        public const int BcOnly = 123900002;
        public const int MixedBcAndNp = 123900003;
        public const int Unknown = 123900004;
    }

    /// <summary>
    /// Values from the Routing Risk Choice on ka_routingrule.
    /// </summary>
    public static class RoutingRisk
    {
        public const int Medium = 123900000;
        public const int Increased = 123900001;
        public const int Unacceptable = 123900002;
    }

    /// <summary>
    /// Values from the Risk Score Choice on ka_kycapprovalcase.
    /// </summary>
    public static class ApprovalCaseRiskScore
    {
        public const int Neutral = 123900000;
        public const int Medium = 123900001;
        public const int Increased = 123900002;
        public const int Unacceptable = 123900003;
    }

    /// <summary>
    /// Values from the KYC Tri-State Match Choice used by GCARC Match, TM Match,
    /// and Compliance Override Match on ka_routingrule.
    /// </summary>
    public static class TriStateMatch
    {
        public const int Any = 123900000;
        public const int Yes = 123900001;
        public const int No = 123900002;
    }

    /// <summary>
    /// Values used by OneApproval Flow Choices (Routing Rule target, Approval Task flow, Approval Case flow).
    /// </summary>
    public static class OneApprovalFlow
    {
        public const int Carc = 123900000;
        public const int Coe = 123900001;
        public const int Sma = 123900002;
        public const int Unrouted = 123900003;
    }
}
