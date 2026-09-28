using System;
using Microsoft.Xrm.Sdk;
using OneApproval.Plugins.Constants;

namespace OneApproval.Plugins.Tests.TestHelpers
{
    /// <summary>
    /// Builder class for generating populated, strongly-typed Dataverse entities for unit testing.
    /// </summary>
    public static class TestDataBuilder
    {
        public static Entity CreateApprovalAndAdvice(Guid id, int? stage = OneKycStage.MediumApproval, Guid? caseId = null)
        {
            Entity entity = new Entity(ApprovalAndAdviceEntity.EntityLogicalName, id);
            if (stage.HasValue)
            {
                entity[ApprovalAndAdviceEntity.Stage] = new OptionSetValue(stage.Value);
            }
            if (caseId.HasValue)
            {
                entity[ApprovalAndAdviceEntity.CaseId] = new EntityReference(OneKycCaseEntity.EntityLogicalName, caseId.Value);
            }
            return entity;
        }

        public static Entity CreateOneKycCase(
            Guid id,
            string caseNumber = "CASE-1001",
            int? caseType = 123900000,
            string currentStageName = "Medium Approval",
            Guid? partyId = null,
            Guid? clientUnitId = null,
            string riskScore = "Medium",
            DateTime? nextReviewDate = null,
            Guid? groupId = null)
        {
            Entity entity = new Entity(OneKycCaseEntity.EntityLogicalName, id);
            entity[OneKycCaseEntity.CaseNumber] = caseNumber;

            if (caseType.HasValue)
            {
                entity[OneKycCaseEntity.CaseType] = new OptionSetValue(caseType.Value);
            }

            entity[OneKycCaseEntity.CurrentStageName] = currentStageName;

            if (partyId.HasValue)
            {
                entity[OneKycCaseEntity.PartyId] = new EntityReference(PartyEntity.EntityLogicalName, partyId.Value);
            }

            if (clientUnitId.HasValue)
            {
                entity[OneKycCaseEntity.ClientUnit] = new EntityReference(ClientUnitEntity.EntityLogicalName, clientUnitId.Value);
            }

            entity[OneKycCaseEntity.RiskScore] = riskScore;

            if (nextReviewDate.HasValue)
            {
                entity[OneKycCaseEntity.NextReviewDate] = nextReviewDate.Value;
            }

            if (groupId.HasValue)
            {
                entity[OneKycCaseEntity.GroupId] = new EntityReference(GroupEntity.EntityLogicalName, groupId.Value);
            }

            return entity;
        }

        public static Entity CreateParty(
            Guid id,
            string name = "Acme Corp",
            int? partyType = OneKycPartyType.BusinessClient,
            string clientNumber = "BC-98765",
            int? source = 100000000,
            Guid? groupId = null)
        {
            Entity entity = new Entity(PartyEntity.EntityLogicalName, id);
            entity[PartyEntity.Name] = name;

            if (partyType.HasValue)
            {
                entity[PartyEntity.PartyType] = new OptionSetValue(partyType.Value);
            }

            entity[PartyEntity.ClientNumber] = clientNumber;

            if (source.HasValue)
            {
                entity[PartyEntity.Source] = new OptionSetValue(source.Value);
            }

            if (groupId.HasValue)
            {
                entity[PartyEntity.GroupId] = new EntityReference(GroupEntity.EntityLogicalName, groupId.Value);
            }

            return entity;
        }

        public static Entity CreateClientUnit(Guid id, string name = "Wealth Management Netherlands")
        {
            Entity entity = new Entity(ClientUnitEntity.EntityLogicalName, id);
            entity[ClientUnitEntity.Name] = name;
            return entity;
        }

        public static Entity CreateCaseOwner(
            Guid id,
            Guid caseId,
            int roleGroup = OneKycRoleGroup.CaseAnalyst,
            string name = "John Doe",
            string username = "jdoe@example.com")
        {
            Entity entity = new Entity(CaseOwnerEntity.EntityLogicalName, id);
            entity[CaseOwnerEntity.CaseId] = new EntityReference(OneKycCaseEntity.EntityLogicalName, caseId);
            entity[CaseOwnerEntity.RoleGroup] = new OptionSetValue(roleGroup);
            entity[CaseOwnerEntity.Name] = name;
            entity[CaseOwnerEntity.Username] = username;
            entity[CaseOwnerEntity.StateCode] = new OptionSetValue(EntityState.Active);
            return entity;
        }

        public static Entity CreateGroup(Guid id, string groupNumber = "GRP-001", string groupName = "Acme Holdings Group")
        {
            Entity entity = new Entity(GroupEntity.EntityLogicalName, id);
            entity[GroupEntity.IdNumber] = groupNumber;
            entity[GroupEntity.Name] = groupName;
            return entity;
        }

        public static Entity CreateRoutingRule(
            Guid id,
            string name = "Rule 1",
            string clientUnitMatchText = "Wealth Management",
            int? clientType = OneKycPartyType.BusinessClient,
            int? routingScenario = RoutingScenario.SingleBcNumber,
            int? groupComposition = GroupComposition.NotApplicable,
            int? routingRisk = RoutingRisk.Medium,
            int? oneKycStage = OneKycStage.MediumApproval,
            int? oneKycCaseType = 123900000,
            int? targetFlow = OneApprovalFlow.Sma,
            int? complianceAdvice = null,
            int? gcarcMatch = TriStateMatch.No,
            int? tmMatch = TriStateMatch.No,
            int? complianceOverrideMatch = TriStateMatch.No)
        {
            Entity entity = new Entity(RoutingRuleEntity.EntityLogicalName, id);
            entity[RoutingRuleEntity.Name] = name;
            entity[RoutingRuleEntity.ClientUnitMatchText] = clientUnitMatchText;

            if (clientType.HasValue)
                entity[RoutingRuleEntity.ClientType] = new OptionSetValue(clientType.Value);

            if (routingScenario.HasValue)
                entity[RoutingRuleEntity.RoutingScenario] = new OptionSetValue(routingScenario.Value);

            if (groupComposition.HasValue)
                entity[RoutingRuleEntity.GroupComposition] = new OptionSetValue(groupComposition.Value);

            if (routingRisk.HasValue)
                entity[RoutingRuleEntity.RoutingRisk] = new OptionSetValue(routingRisk.Value);

            if (oneKycStage.HasValue)
                entity[RoutingRuleEntity.OneKycStage] = new OptionSetValue(oneKycStage.Value);

            if (oneKycCaseType.HasValue)
                entity[RoutingRuleEntity.OneKycCaseType] = new OptionSetValue(oneKycCaseType.Value);

            if (targetFlow.HasValue)
                entity[RoutingRuleEntity.TargetOneApprovalFlow] = new OptionSetValue(targetFlow.Value);

            if (complianceAdvice.HasValue)
                entity[RoutingRuleEntity.ComplianceAdvice] = new OptionSetValue(complianceAdvice.Value);

            if (gcarcMatch.HasValue)
                entity[RoutingRuleEntity.GcarcMatch] = new OptionSetValue(gcarcMatch.Value);

            if (tmMatch.HasValue)
                entity[RoutingRuleEntity.TmMatch] = new OptionSetValue(tmMatch.Value);

            if (complianceOverrideMatch.HasValue)
                entity[RoutingRuleEntity.ComplianceOverrideMatch] = new OptionSetValue(complianceOverrideMatch.Value);

            entity[RoutingRuleEntity.StateCode] = new OptionSetValue(EntityState.Active);
            return entity;
        }

        public static Entity CreateApprovalTask(Guid id, string name = "Approval Task - CASE-1001 - SMA", int flow = OneApprovalFlow.Sma)
        {
            Entity entity = new Entity(ApprovalTaskEntity.EntityLogicalName, id);
            entity[ApprovalTaskEntity.Name] = name;
            entity[ApprovalTaskEntity.Flow] = new OptionSetValue(flow);
            return entity;
        }

        public static Entity CreateApprovalCase(
            Guid id,
            string name = "KYC Approval - CASE-1001 - SMA",
            Guid? taskId = null,
            Guid? oneKycCaseId = null,
            int? caseType = 123900000,
            Guid? partyId = null,
            int? partyType = OneKycPartyType.BusinessClient,
            Guid? clientUnitId = null,
            int? riskScore = ApprovalCaseRiskScore.Medium,
            int? flow = OneApprovalFlow.Sma)
        {
            Entity entity = new Entity(ApprovalCaseEntity.EntityLogicalName, id);
            entity[ApprovalCaseEntity.Name] = name;

            if (taskId.HasValue)
                entity[ApprovalCaseEntity.ApprovalTask] = new EntityReference(ApprovalTaskEntity.EntityLogicalName, taskId.Value);

            if (oneKycCaseId.HasValue)
                entity[ApprovalCaseEntity.OneKycCaseNumber] = new EntityReference(OneKycCaseEntity.EntityLogicalName, oneKycCaseId.Value);

            if (caseType.HasValue)
                entity[ApprovalCaseEntity.CaseType] = new OptionSetValue(caseType.Value);

            if (partyId.HasValue)
                entity[ApprovalCaseEntity.PartyName] = new EntityReference(PartyEntity.EntityLogicalName, partyId.Value);

            if (partyType.HasValue)
                entity[ApprovalCaseEntity.PartyType] = new OptionSetValue(partyType.Value);

            if (clientUnitId.HasValue)
                entity[ApprovalCaseEntity.ClientUnit] = new EntityReference(ClientUnitEntity.EntityLogicalName, clientUnitId.Value);

            if (riskScore.HasValue)
                entity[ApprovalCaseEntity.RiskScore] = new OptionSetValue(riskScore.Value);

            if (flow.HasValue)
                entity[ApprovalCaseEntity.OneApprovalFlow] = new OptionSetValue(flow.Value);

            entity[ApprovalCaseEntity.StateCode] = new OptionSetValue(EntityState.Active);
            return entity;
        }
    }
}
