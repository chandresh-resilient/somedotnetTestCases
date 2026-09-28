namespace OneApproval.Plugins.Constants
{
    /// <summary>
    /// Schema definition for the OneKYC Approval and Advice entity (aab_approvalandadvice).
    /// </summary>
    public static class ApprovalAndAdviceEntity
    {
        public const string EntityLogicalName = "aab_approvalandadvice";

        public const string Stage = "aab_stage";
        public const string CaseId = "aab_caseid";
    }

    /// <summary>
    /// Schema definition for the OneKYC Case entity (aab_case).
    /// </summary>
    public static class OneKycCaseEntity
    {
        public const string EntityLogicalName = "aab_case";

        public const string CaseNumber = "aab_casenumber";
        public const string CaseType = "aab_abncasetype";
        public const string CurrentStageName = "aab_currentstagename";
        public const string PartyId = "aab_partyid";
        public const string ClientUnit = "aab_clientunit";
        public const string RiskScore = "aab_riskscore";
        public const string NextReviewDate = "aab_nextreviewdate";
        public const string GroupId = "aab_groupid";
    }

    /// <summary>
    /// Schema definition for the OneKYC Party entity (aab_party).
    /// </summary>
    public static class PartyEntity
    {
        public const string EntityLogicalName = "aab_party";

        public const string Name = "aab_name";
        public const string PartyType = "aab_partytype";
        public const string ClientNumber = "aab_clientnumber";
        public const string Source = "aab_source";
        public const string GroupId = "aab_groupid";
    }

    /// <summary>
    /// Schema definition for the OneKYC Client Unit entity (aab_clientunit).
    /// </summary>
    public static class ClientUnitEntity
    {
        public const string EntityLogicalName = "aab_clientunit";

        public const string Name = "aab_name";
    }

    /// <summary>
    /// Schema definition for the OneApproval Routing Rule entity (ka_routingrule).
    /// </summary>
    public static class RoutingRuleEntity
    {
        public const string EntityLogicalName = "ka_routingrule";

        public const string Name = "ka_routingrulename";
        public const string ClientUnitMatchText = "ka_clientunitmatchtext";
        public const string ClientType = "ka_clienttype";
        public const string RoutingScenario = "ka_routingscenario";
        public const string GroupComposition = "ka_groupcomposition";
        public const string RoutingRisk = "ka_routingrisk";
        public const string OneKycStage = "ka_onekycstage";
        public const string ComplianceAdvice = "ka_complianceadvice";
        public const string ComplianceOverrideMatch = "ka_compliancecverridematch";
        public const string OneKycCaseType = "ka_onekyccasetype";
        public const string GcarcMatch = "ka_gcarcmatch";
        public const string TmMatch = "ka_tmmatch";
        public const string TargetOneApprovalFlow = "ka_targetoneapprovalflow";
        public const string StateCode = "statecode";
    }

    /// <summary>
    /// Schema definition for the OneApproval Approval Task entity (ka_approvaltask).
    /// </summary>
    public static class ApprovalTaskEntity
    {
        public const string EntityLogicalName = "ka_approvaltask";

        public const string Name = "ka_newcolumn";
        public const string Flow = "ka_flow";
    }

    /// <summary>
    /// Schema definition for the OneApproval KYC Approval Case entity (ka_kycapprovalcase).
    /// </summary>
    public static class ApprovalCaseEntity
    {
        public const string EntityLogicalName = "ka_kycapprovalcase";

        public const string Id = "ka_kycapprovalcaseid";
        public const string Name = "ka_newcolumn";
        public const string OneApprovalFlow = "ka_oneapprovalflow";
        public const string ApprovalTask = "ka_approvaltask";
        public const string OneKycCaseNumber = "ka_nnekyccasenumber";
        public const string CaseType = "ka_casetype";
        public const string CurrentStageOneKyc = "ka_currentstageonekyc";
        public const string PartyName = "ka_partyname";
        public const string PartyType = "ka_partytype";
        public const string ClientUnit = "ka_clientunit";
        public const string RiskScore = "ka_riskscore";
        public const string NextReviewDate = "ka_nextreviewdate";
        public const string ClientNumber = "ka_clientnumber";
        public const string SourceSystem = "ka_sourcesystem";
        public const string GroupId = "ka_groupid";
        public const string CaseAnalyst = "ka_caseanalyst";
        public const string ClientName = "ka_clientname";
        public const string ComplexName = "ka_complexname";
        public const string StateCode = "statecode";
    }

    /// <summary>
    /// Schema definition for the OneKYC Case Owner entity (aab_caseowner).
    /// </summary>
    public static class CaseOwnerEntity
    {
        public const string EntityLogicalName = "aab_caseowner";

        public const string Id = "aab_caseownerid";
        public const string Name = "aab_name";
        public const string Username = "aab_username";
        public const string RoleGroup = "aab_rolegroup";
        public const string CaseId = "aab_caseid";
        public const string StateCode = "statecode";
    }

    /// <summary>
    /// Schema definition for the OneKYC Group entity (aab_group).
    /// </summary>
    public static class GroupEntity
    {
        public const string EntityLogicalName = "aab_group";

        public const string IdNumber = "aab_groupidnumber";
        public const string Name = "aab_groupname";
    }
}
