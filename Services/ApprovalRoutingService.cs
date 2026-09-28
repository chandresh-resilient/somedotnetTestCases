using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using OneApproval.Plugins.Common;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Models;
using OneApproval.Plugins.Repositories;

namespace OneApproval.Plugins.Services
{
    /// <summary>
    /// Core orchestrator for the OneApproval routing process.
    /// Evaluates incoming OneKYC Approval &amp; Advice records and creates or updates
    /// the corresponding Approval Task and KYC Approval Case records.
    /// </summary>
    public class ApprovalRoutingService
    {
        private readonly IOneKycRepository _oneKycRepository;
        private readonly IRoutingRuleRepository _routingRuleRepository;
        private readonly IOneApprovalRepository _oneApprovalRepository;
        private readonly GroupCompositionCalculator _groupCompositionCalculator;
        private readonly RoutingRuleMatcher _routingRuleMatcher;
        private readonly ITracingService _tracing;

        /// <summary>
        /// Initializes a new instance of <see cref="ApprovalRoutingService"/>.
        /// </summary>
        public ApprovalRoutingService(
            IOneKycRepository oneKycRepository,
            IRoutingRuleRepository routingRuleRepository,
            IOneApprovalRepository oneApprovalRepository,
            GroupCompositionCalculator groupCompositionCalculator,
            RoutingRuleMatcher routingRuleMatcher,
            ITracingService tracing)
        {
            _oneKycRepository = oneKycRepository ?? throw new ArgumentNullException(nameof(oneKycRepository));
            _routingRuleRepository = routingRuleRepository ?? throw new ArgumentNullException(nameof(routingRuleRepository));
            _oneApprovalRepository = oneApprovalRepository ?? throw new ArgumentNullException(nameof(oneApprovalRepository));
            _groupCompositionCalculator = groupCompositionCalculator ?? throw new ArgumentNullException(nameof(groupCompositionCalculator));
            _routingRuleMatcher = routingRuleMatcher ?? throw new ArgumentNullException(nameof(routingRuleMatcher));
            _tracing = tracing;
        }

        /// <summary>
        /// Executes the end-to-end routing workflow for a created Approval and Advice record.
        /// </summary>
        /// <remarks>
        /// Execution Sequence:
        /// 1. Load source Approval &amp; Advice record and validate trigger stage.
        /// 2. Load related OneKYC Case, Party, Client Unit, and Group.
        /// 3. Derive Scenario, Group Composition, and Risk Scores into <see cref="RoutingContextData"/>.
        /// 4. Check whether an active KYC Approval Case already exists for this OneKYC Case.
        /// 5. Query active Routing Rules and filter/match confirmed conditions.
        /// 6. Enforce single-match rule: 0 matches = exit cleanly; &gt;1 matches = log configuration error and exit.
        /// 7. Load optional OneKYC Case Analyst (Role Group = 958630000) and Group display info.
        /// 8. Create or update the linked Approval Task (reusing existing task if present, updating ka_flow).
        /// 9. Create or update the KYC Approval Case (synchronizing fields, clearing null values).
        /// </remarks>
        /// <param name="approvalAndAdviceId">GUID of the newly created aab_approvalandadvice record.</param>
        public void RouteApprovalAndAdvice(Guid approvalAndAdviceId)
        {
            _tracing?.Trace("=== OneApproval routing started ===");

            // 1. Retrieve the triggering Approval & Advice record
            Entity approvalAndAdvice = _oneKycRepository.GetApprovalAndAdvice(approvalAndAdviceId);
            OptionSetValue stageValue = approvalAndAdvice.GetAttributeValue<OptionSetValue>(ApprovalAndAdviceEntity.Stage);

            if (stageValue == null)
            {
                _tracing?.Trace("Exit: aab_stage is blank on Approval And Advice {0}.", approvalAndAdviceId);
                return;
            }

            _tracing?.Trace("Approval And Advice Stage value = {0}.", stageValue.Value);

            // 2. Validate trigger gate stage
            if (!StageHelper.IsRoutingTriggerStage(stageValue.Value))
            {
                _tracing?.Trace("Exit: Stage {0} is not a routing trigger Stage.", stageValue.Value);
                return;
            }

            EntityReference oneKycCaseReference = approvalAndAdvice.GetAttributeValue<EntityReference>(ApprovalAndAdviceEntity.CaseId);
            if (oneKycCaseReference == null)
            {
                _tracing?.Trace("Exit: Approval And Advice {0} has no related OneKYC Case.", approvalAndAdviceId);
                return;
            }

            // 3. Retrieve source OneKYC Case
            Entity oneKycCase = _oneKycRepository.GetOneKycCase(oneKycCaseReference.Id);

            // 4. Check for an existing active KYC Approval Case (to decide between Create vs Update)
            Entity existingActiveCase = _oneApprovalRepository.FindActiveApprovalCaseByOneKycCaseId(oneKycCase.Id);
            if (existingActiveCase != null)
            {
                _tracing?.Trace(
                    "Existing active OneApproval KYC Approval Case {0} found for OneKYC Case {1}. " +
                    "The existing Case will be updated if exactly one Routing Rule matches.",
                    existingActiveCase.Id,
                    oneKycCase.Id);
            }
            else
            {
                _tracing?.Trace(
                    "No active OneApproval KYC Approval Case exists for OneKYC Case {0}. " +
                    "A new Case will be created if exactly one Routing Rule matches.",
                    oneKycCase.Id);
            }

            // 5. Build routing context with all confirmed attributes
            RoutingContextData routingContext = BuildRoutingContext(oneKycCase, stageValue.Value);
            if (routingContext == null)
            {
                return;
            }

            TraceRoutingContext(routingContext);

            // 6. Query and evaluate active Routing Rules
            List<Entity> activeRules = _routingRuleRepository.GetActiveRoutingRules();
            _tracing?.Trace("Active Routing Rule count = {0}.", activeRules.Count);

            List<Entity> matchingRules = _routingRuleMatcher.FindMatchingRules(activeRules, routingContext);

            // 7. Verify match cardinality
            if (matchingRules.Count == 0)
            {
                _tracing?.Trace("No supported Routing Rule matched. Existing OneApproval records will not be changed and no new records will be created.");
                return;
            }

            if (matchingRules.Count > 1)
            {
                _tracing?.Trace(
                    "CONFIGURATION ERROR: {0} supported Routing Rules matched. Existing OneApproval records will not be changed and no new records will be created.",
                    matchingRules.Count);

                foreach (Entity matchedRule in matchingRules)
                {
                    string matchedRuleName = matchedRule.GetAttributeValue<string>(RoutingRuleEntity.Name) ?? "(blank)";
                    _tracing?.Trace("Matched Rule: ID={0}, Name={1}", matchedRule.Id, matchedRuleName);
                }

                return;
            }

            // Exactly one rule matched
            Entity winningRule = matchingRules[0];
            OptionSetValue targetFlowValue = winningRule.GetAttributeValue<OptionSetValue>(RoutingRuleEntity.TargetOneApprovalFlow);

            if (targetFlowValue == null)
            {
                _tracing?.Trace("Configuration error: matched Routing Rule {0} has no Target OneApproval Flow.", winningRule.Id);
                return;
            }

            if (!FlowHelper.IsSupportedTargetFlow(targetFlowValue.Value))
            {
                _tracing?.Trace(
                    "Configuration error: matched Routing Rule {0} has unsupported Flow value {1}. Only CARC, CoE and SMA are accepted.",
                    winningRule.Id,
                    targetFlowValue.Value);
                return;
            }

            string flowLabel = FlowHelper.GetFlowLabel(targetFlowValue.Value);

            _tracing?.Trace(
                "Exactly one supported Routing Rule matched. Rule={0}, Flow={1} ({2}).",
                winningRule.Id,
                flowLabel,
                targetFlowValue.Value);

            // 8. Load optional Case Analyst and Group display fields
            Entity caseAnalyst = _oneKycRepository.FindCaseAnalyst(oneKycCase.Id);
            Entity group = routingContext.Group != null ? _oneKycRepository.GetGroup(routingContext.Group.Id) : null;

            // 9. Create or update Approval Task
            Guid approvalTaskId = CreateOrUpdateApprovalTask(
                routingContext,
                existingActiveCase,
                targetFlowValue.Value,
                flowLabel);

            // 10. Create or update KYC Approval Case
            Guid approvalCaseId = CreateOrUpdateApprovalCase(
                existingActiveCase,
                routingContext,
                caseAnalyst,
                group,
                approvalTaskId,
                targetFlowValue.Value,
                flowLabel);

            _tracing?.Trace(
                "OneApproval routing completed. Approval Task={0}; KYC Approval Case={1}; Operation={2}.",
                approvalTaskId,
                approvalCaseId,
                existingActiveCase == null ? "Create" : "Update");

            _tracing?.Trace("=== OneApproval routing finished ===");
        }

        /// <summary>
        /// Loads related Party and derives all confirmed routing inputs.
        /// </summary>
        private RoutingContextData BuildRoutingContext(Entity oneKycCase, int oneKycStage)
        {
            EntityReference partyReference = oneKycCase.GetAttributeValue<EntityReference>(OneKycCaseEntity.PartyId);
            if (partyReference == null)
            {
                _tracing?.Trace("Exit: OneKYC Case {0} has no Party lookup.", oneKycCase.Id);
                return null;
            }

            Entity party = _oneKycRepository.GetParty(partyReference.Id);

            OptionSetValue partyTypeValue = party.GetAttributeValue<OptionSetValue>(PartyEntity.PartyType);
            if (partyTypeValue == null ||
                (partyTypeValue.Value != OneKycPartyType.BusinessClient &&
                 partyTypeValue.Value != OneKycPartyType.NaturalPerson))
            {
                _tracing?.Trace(
                    "Exit: Party {0} has no supported Party Type. Value={1}.",
                    party.Id,
                    partyTypeValue == null ? "(blank)" : partyTypeValue.Value.ToString());
                return null;
            }

            OptionSetValue caseTypeValue = oneKycCase.GetAttributeValue<OptionSetValue>(OneKycCaseEntity.CaseType);
            if (caseTypeValue == null)
            {
                _tracing?.Trace("Exit: OneKYC Case {0} has no Case Type.", oneKycCase.Id);
                return null;
            }

            EntityReference clientUnitReference = oneKycCase.GetAttributeValue<EntityReference>(OneKycCaseEntity.ClientUnit);
            if (clientUnitReference == null)
            {
                _tracing?.Trace("Exit: OneKYC Case {0} has no Client Unit.", oneKycCase.Id);
                return null;
            }

            Entity clientUnit = _oneKycRepository.GetClientUnit(clientUnitReference.Id);
            string clientUnitName = clientUnit.GetAttributeValue<string>(ClientUnitEntity.Name);

            if (string.IsNullOrWhiteSpace(clientUnitName))
            {
                _tracing?.Trace(
                    "Exit: OneKYC Client Unit {0} has no aab_name, so text-based Client Unit routing cannot continue.",
                    clientUnitReference.Id);
                return null;
            }

            clientUnitName = clientUnitName.Trim();
            _tracing?.Trace("Actual OneKYC Client Unit Name = '{0}'.", clientUnitName);

            EntityReference groupFromCase = oneKycCase.GetAttributeValue<EntityReference>(OneKycCaseEntity.GroupId);
            EntityReference groupFromParty = party.GetAttributeValue<EntityReference>(PartyEntity.GroupId);

            if (groupFromCase != null &&
                groupFromParty != null &&
                groupFromCase.Id != groupFromParty.Id)
            {
                _tracing?.Trace(
                    "WARNING: OneKYC Case Group {0} differs from Party Group {1}. " +
                    "The Case Group is used because Case Detils.xlsx identifies aab_case.aab_groupid as the source.",
                    groupFromCase.Id,
                    groupFromParty.Id);
            }

            string clientNumber = party.GetAttributeValue<string>(PartyEntity.ClientNumber);
            if (string.IsNullOrWhiteSpace(clientNumber))
            {
                _tracing?.Trace("Exit: Party {0} has no Client Number / BC Number.", party.Id);
                return null;
            }

            int routingScenario = groupFromCase == null
                ? RoutingScenario.SingleBcNumber
                : RoutingScenario.LogicalGroup;

            int groupComposition = _groupCompositionCalculator.Calculate(groupFromCase, partyTypeValue.Value);

            string riskText = oneKycCase.GetAttributeValue<string>(OneKycCaseEntity.RiskScore);
            int? routingRisk = RiskMapper.MapRiskTextToRoutingRisk(riskText);

            if (!routingRisk.HasValue)
            {
                _tracing?.Trace(
                    "Exit: OneKYC Case {0} has a blank or unsupported Risk Score '{1}'.",
                    oneKycCase.Id,
                    riskText ?? "(blank)");
                return null;
            }

            int? approvalCaseRiskScore = RiskMapper.MapRiskTextToApprovalCaseRiskScore(riskText);
            if (!approvalCaseRiskScore.HasValue)
            {
                _tracing?.Trace(
                    "Exit: Risk Score '{0}' cannot be mapped to ka_kycapprovalcase.ka_riskscore.",
                    riskText);
                return null;
            }

            OptionSetValue sourceSystemValue = party.GetAttributeValue<OptionSetValue>(PartyEntity.Source);

            return new RoutingContextData
            {
                OneKycCase = oneKycCase,
                Party = party,
                ClientUnit = clientUnitReference,
                ClientUnitName = clientUnitName,
                Group = groupFromCase,
                OneKycCaseNumber = oneKycCase.GetAttributeValue<string>(OneKycCaseEntity.CaseNumber),
                ClientType = partyTypeValue.Value,
                OneKycCaseType = caseTypeValue.Value,
                OneKycStage = oneKycStage,
                CurrentStageName = oneKycCase.GetAttributeValue<string>(OneKycCaseEntity.CurrentStageName),
                RiskText = riskText,
                RoutingRisk = routingRisk.Value,
                ApprovalCaseRiskScore = approvalCaseRiskScore.Value,
                RoutingScenario = routingScenario,
                GroupComposition = groupComposition,
                PartyName = party.GetAttributeValue<string>(PartyEntity.Name),
                ClientNumber = clientNumber,
                SourceSystem = sourceSystemValue?.Value,
                NextReviewDate = oneKycCase.Contains(OneKycCaseEntity.NextReviewDate)
                    ? (DateTime?)oneKycCase.GetAttributeValue<DateTime>(OneKycCaseEntity.NextReviewDate)
                    : null
            };
        }

        /// <summary>
        /// Creates a new Approval Task or updates the existing linked Task with the latest Flow and name.
        /// </summary>
        private Guid CreateOrUpdateApprovalTask(
            RoutingContextData context,
            Entity existingApprovalCase,
            int targetFlow,
            string flowLabel)
        {
            EntityReference existingApprovalTaskReference = existingApprovalCase == null
                ? null
                : existingApprovalCase.GetAttributeValue<EntityReference>(ApprovalCaseEntity.ApprovalTask);

            string desiredTaskName = EntityHelper.BuildRecordName("Approval Task", context.OneKycCaseNumber, flowLabel);
            OptionSetValue desiredTaskFlow = new OptionSetValue(targetFlow);

            // Reuse existing Task if present on the existing KYC Approval Case
            if (existingApprovalTaskReference != null)
            {
                Entity existingTask = _oneApprovalRepository.GetApprovalTask(existingApprovalTaskReference.Id);
                Entity taskUpdate = new Entity(ApprovalTaskEntity.EntityLogicalName, existingApprovalTaskReference.Id);

                EntityHelper.AddIfDifferent(taskUpdate, existingTask, ApprovalTaskEntity.Name, desiredTaskName, _tracing, "Approval Task");
                EntityHelper.AddIfDifferent(taskUpdate, existingTask, ApprovalTaskEntity.Flow, desiredTaskFlow, _tracing, "Approval Task");

                if (taskUpdate.Attributes.Count > 0)
                {
                    _oneApprovalRepository.UpdateApprovalTask(taskUpdate);
                    _tracing?.Trace(
                        "Updated existing Approval Task {0}. Changed field count={1}; Flow={2} ({3}).",
                        existingApprovalTaskReference.Id,
                        taskUpdate.Attributes.Count,
                        flowLabel,
                        targetFlow);
                }
                else
                {
                    _tracing?.Trace(
                        "Existing Approval Task {0} already has the required name and Flow. No Task update was required.",
                        existingApprovalTaskReference.Id);
                }

                return existingApprovalTaskReference.Id;
            }

            // Create new Task
            Entity taskToCreate = new Entity(ApprovalTaskEntity.EntityLogicalName);
            taskToCreate[ApprovalTaskEntity.Name] = desiredTaskName;
            taskToCreate[ApprovalTaskEntity.Flow] = desiredTaskFlow;

            Guid newTaskId = _oneApprovalRepository.CreateApprovalTask(taskToCreate);

            _tracing?.Trace("Created Approval Task {0}; ka_flow={1} ({2}).", newTaskId, flowLabel, targetFlow);
            return newTaskId;
        }

        /// <summary>
        /// Creates a new KYC Approval Case or synchronizes an existing active Case with latest source and flow values.
        /// </summary>
        private Guid CreateOrUpdateApprovalCase(
            Entity existingApprovalCase,
            RoutingContextData context,
            Entity caseAnalyst,
            Entity group,
            Guid approvalTaskId,
            int targetFlow,
            string flowLabel)
        {
            bool isUpdate = existingApprovalCase != null;

            string desiredCaseName = EntityHelper.BuildRecordName("KYC Approval", context.OneKycCaseNumber, flowLabel);
            EntityReference desiredApprovalTask = new EntityReference(ApprovalTaskEntity.EntityLogicalName, approvalTaskId);
            EntityReference desiredOneKycCase = context.OneKycCase.ToEntityReference();
            OptionSetValue desiredCaseType = new OptionSetValue(context.OneKycCaseType);
            string desiredCurrentStage = string.IsNullOrWhiteSpace(context.CurrentStageName) ? null : context.CurrentStageName.Trim();
            EntityReference desiredParty = context.Party.ToEntityReference();
            OptionSetValue desiredPartyType = new OptionSetValue(context.ClientType);
            EntityReference desiredClientUnit = context.ClientUnit;
            OptionSetValue desiredRiskScore = new OptionSetValue(context.ApprovalCaseRiskScore);
            object desiredNextReviewDate = context.NextReviewDate.HasValue ? (object)context.NextReviewDate.Value : null;
            string desiredClientNumber = string.IsNullOrWhiteSpace(context.ClientNumber) ? null : context.ClientNumber.Trim();
            object desiredSourceSystem = context.SourceSystem.HasValue ? (object)new OptionSetValue(context.SourceSystem.Value) : null;
            EntityReference desiredGroup = context.Group;
            EntityReference desiredCaseAnalyst = caseAnalyst?.ToEntityReference();
            string desiredClientName = string.IsNullOrWhiteSpace(context.PartyName) ? null : context.PartyName.Trim();

            string groupName = group?.GetAttributeValue<string>(GroupEntity.Name);
            string desiredComplexName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
            OptionSetValue desiredFlow = new OptionSetValue(targetFlow);

            // Update mode: perform sparse update and clear fields where source is now null
            if (isUpdate)
            {
                Entity caseUpdate = new Entity(ApprovalCaseEntity.EntityLogicalName, existingApprovalCase.Id);

                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.Name, desiredCaseName, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.ApprovalTask, desiredApprovalTask, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.OneKycCaseNumber, desiredOneKycCase, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.CaseType, desiredCaseType, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.PartyName, desiredParty, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.PartyType, desiredPartyType, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.ClientUnit, desiredClientUnit, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.RiskScore, desiredRiskScore, _tracing, "KYC Approval Case");

                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.CurrentStageOneKyc, desiredCurrentStage, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.NextReviewDate, desiredNextReviewDate, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.ClientNumber, desiredClientNumber, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.SourceSystem, desiredSourceSystem, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.GroupId, desiredGroup, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.CaseAnalyst, desiredCaseAnalyst, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.ClientName, desiredClientName, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.ComplexName, desiredComplexName, _tracing, "KYC Approval Case");
                EntityHelper.AddIfDifferent(caseUpdate, existingApprovalCase, ApprovalCaseEntity.OneApprovalFlow, desiredFlow, _tracing, "KYC Approval Case");

                if (caseUpdate.Attributes.Count > 0)
                {
                    _oneApprovalRepository.UpdateApprovalCase(caseUpdate);
                    _tracing?.Trace(
                        "Updated existing KYC Approval Case {0}. Changed field count={1}; Flow={2} ({3}); Approval Task={4}.",
                        existingApprovalCase.Id,
                        caseUpdate.Attributes.Count,
                        flowLabel,
                        targetFlow,
                        approvalTaskId);
                }
                else
                {
                    _tracing?.Trace(
                        "Existing KYC Approval Case {0} already matches the current source values and Flow. No Case update was required.",
                        existingApprovalCase.Id);
                }

                return existingApprovalCase.Id;
            }

            // Create mode: set required and optional non-null fields
            Entity caseToCreate = new Entity(ApprovalCaseEntity.EntityLogicalName);
            caseToCreate[ApprovalCaseEntity.Name] = desiredCaseName;
            caseToCreate[ApprovalCaseEntity.ApprovalTask] = desiredApprovalTask;
            caseToCreate[ApprovalCaseEntity.OneKycCaseNumber] = desiredOneKycCase;
            caseToCreate[ApprovalCaseEntity.CaseType] = desiredCaseType;
            caseToCreate[ApprovalCaseEntity.PartyName] = desiredParty;
            caseToCreate[ApprovalCaseEntity.PartyType] = desiredPartyType;
            caseToCreate[ApprovalCaseEntity.ClientUnit] = desiredClientUnit;
            caseToCreate[ApprovalCaseEntity.RiskScore] = desiredRiskScore;
            caseToCreate[ApprovalCaseEntity.OneApprovalFlow] = desiredFlow;

            EntityHelper.SetIfNotNull(caseToCreate, ApprovalCaseEntity.CurrentStageOneKyc, desiredCurrentStage);
            EntityHelper.SetIfNotNull(caseToCreate, ApprovalCaseEntity.NextReviewDate, desiredNextReviewDate);
            EntityHelper.SetIfNotNull(caseToCreate, ApprovalCaseEntity.ClientNumber, desiredClientNumber);
            EntityHelper.SetIfNotNull(caseToCreate, ApprovalCaseEntity.SourceSystem, desiredSourceSystem);
            EntityHelper.SetIfNotNull(caseToCreate, ApprovalCaseEntity.GroupId, desiredGroup);
            EntityHelper.SetIfNotNull(caseToCreate, ApprovalCaseEntity.CaseAnalyst, desiredCaseAnalyst);
            EntityHelper.SetIfNotNull(caseToCreate, ApprovalCaseEntity.ClientName, desiredClientName);
            EntityHelper.SetIfNotNull(caseToCreate, ApprovalCaseEntity.ComplexName, desiredComplexName);

            Guid newCaseId = _oneApprovalRepository.CreateApprovalCase(caseToCreate);

            _tracing?.Trace(
                "Created KYC Approval Case {0}; Flow={1} ({2}); Approval Task={3}.",
                newCaseId,
                flowLabel,
                targetFlow,
                approvalTaskId);

            return newCaseId;
        }

        /// <summary>
        /// Outputs the confirmed routing context values to Plugin Trace Log for debugging.
        /// </summary>
        private void TraceRoutingContext(RoutingContextData context)
        {
            if (_tracing == null) return;

            _tracing.Trace("--- Confirmed Routing Context ---");
            _tracing.Trace("OneKYC Case ID={0}; Case Number={1}", context.OneKycCase.Id, context.OneKycCaseNumber ?? "(blank)");
            _tracing.Trace("Party ID={0}; Client Type={1}; Client Number={2}", context.Party.Id, context.ClientType, context.ClientNumber ?? "(blank)");
            _tracing.Trace("Client Unit ID={0}; Client Unit Name={1}; Case Type={2}", context.ClientUnit == null ? "(blank)" : context.ClientUnit.Id.ToString(), context.ClientUnitName ?? "(blank)", context.OneKycCaseType);
            _tracing.Trace("OneKYC Stage={0}; Risk Text={1}; Routing Risk={2}", context.OneKycStage, context.RiskText ?? "(blank)", context.RoutingRisk);
            _tracing.Trace("Routing Scenario={0}; Group Composition={1}; Group={2}", context.RoutingScenario, context.GroupComposition, context.Group == null ? "(none)" : context.Group.Id.ToString());
            _tracing.Trace("TEMPORARY MODE: GCARC, TM, Compliance Advice and Compliance Override source matching are disabled. Special rules are skipped.");
            _tracing.Trace("--- End Routing Context ---");
        }
    }
}
