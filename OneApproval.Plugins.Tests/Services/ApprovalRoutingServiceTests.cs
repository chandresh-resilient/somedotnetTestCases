using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Moq;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Repositories;
using OneApproval.Plugins.Services;
using OneApproval.Plugins.Tests.TestHelpers;
using Xunit;

namespace OneApproval.Plugins.Tests.Services
{
    public class ApprovalRoutingServiceTests
    {
        private readonly Mock<IOneKycRepository> _oneKycRepoMock;
        private readonly Mock<IRoutingRuleRepository> _routingRuleRepoMock;
        private readonly Mock<IOneApprovalRepository> _oneApprovalRepoMock;
        private readonly Mock<ITracingService> _tracingMock;
        private readonly GroupCompositionCalculator _calculator;
        private readonly RoutingRuleMatcher _ruleMatcher;
        private readonly ApprovalRoutingService _service;

        public ApprovalRoutingServiceTests()
        {
            _oneKycRepoMock = new Mock<IOneKycRepository>();
            _routingRuleRepoMock = new Mock<IRoutingRuleRepository>();
            _oneApprovalRepoMock = new Mock<IOneApprovalRepository>();
            _tracingMock = new Mock<ITracingService>();

            _calculator = new GroupCompositionCalculator(_oneKycRepoMock.Object, _tracingMock.Object);
            _ruleMatcher = new RoutingRuleMatcher(_tracingMock.Object);

            _service = new ApprovalRoutingService(
                _oneKycRepoMock.Object,
                _routingRuleRepoMock.Object,
                _oneApprovalRepoMock.Object,
                _calculator,
                _ruleMatcher,
                _tracingMock.Object);
        }

        #region Gate 1-3: Stage and Case Gate Tests

        [Fact]
        public void RouteApprovalAndAdvice_WhenStageNull_ExitsEarly()
        {
            Guid adviceId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, stage: null);

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneKycRepoMock.Verify(r => r.GetOneKycCase(It.IsAny<Guid>()), Times.Never);
            _oneApprovalRepoMock.Verify(r => r.CreateApprovalCase(It.IsAny<Entity>()), Times.Never);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenStageNotTriggerStage_ExitsEarly()
        {
            Guid adviceId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, stage: 999999);

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneKycRepoMock.Verify(r => r.GetOneKycCase(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenCaseLookupNull_ExitsEarly()
        {
            Guid adviceId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, stage: OneKycStage.MediumApproval, caseId: null);

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneKycRepoMock.Verify(r => r.GetOneKycCase(It.IsAny<Guid>()), Times.Never);
        }

        #endregion

        #region Gate 4-11: Context Building Validation Tests

        [Fact]
        public void RouteApprovalAndAdvice_WhenPartyLookupNull_ExitsEarly()
        {
            Guid adviceId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, partyId: null);

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneKycRepoMock.Verify(r => r.GetParty(It.IsAny<Guid>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(3)] // Not BC or NP
        public void RouteApprovalAndAdvice_WhenPartyTypeMissingOrUnsupported_ExitsEarly(int? partyType)
        {
            Guid adviceId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            Guid partyId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, partyId: partyId);
            Entity party = TestDataBuilder.CreateParty(partyId, partyType: partyType);

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);
            _oneKycRepoMock.Setup(r => r.GetParty(partyId)).Returns(party);

            _service.RouteApprovalAndAdvice(adviceId);

            _routingRuleRepoMock.Verify(r => r.GetActiveRoutingRules(), Times.Never);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenCaseTypeMissing_ExitsEarly()
        {
            Guid adviceId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            Guid partyId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, caseType: null, partyId: partyId);
            Entity party = TestDataBuilder.CreateParty(partyId, partyType: OneKycPartyType.BusinessClient);

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);
            _oneKycRepoMock.Setup(r => r.GetParty(partyId)).Returns(party);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneKycRepoMock.Verify(r => r.GetClientUnit(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenClientUnitLookupMissing_ExitsEarly()
        {
            Guid adviceId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            Guid partyId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, partyId: partyId, clientUnitId: null);
            Entity party = TestDataBuilder.CreateParty(partyId, partyType: OneKycPartyType.BusinessClient);

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);
            _oneKycRepoMock.Setup(r => r.GetParty(partyId)).Returns(party);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneKycRepoMock.Verify(r => r.GetClientUnit(It.IsAny<Guid>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RouteApprovalAndAdvice_WhenClientUnitNameBlank_ExitsEarly(string clientUnitName)
        {
            Guid adviceId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            Guid partyId = Guid.NewGuid();
            Guid clientUnitId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, partyId: partyId, clientUnitId: clientUnitId);
            Entity party = TestDataBuilder.CreateParty(partyId, partyType: OneKycPartyType.BusinessClient);
            Entity clientUnit = TestDataBuilder.CreateClientUnit(clientUnitId, clientUnitName);

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);
            _oneKycRepoMock.Setup(r => r.GetParty(partyId)).Returns(party);
            _oneKycRepoMock.Setup(r => r.GetClientUnit(clientUnitId)).Returns(clientUnit);

            _service.RouteApprovalAndAdvice(adviceId);

            _routingRuleRepoMock.Verify(r => r.GetActiveRoutingRules(), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RouteApprovalAndAdvice_WhenClientNumberBlank_ExitsEarly(string clientNumber)
        {
            Guid adviceId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            Guid partyId = Guid.NewGuid();
            Guid clientUnitId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, partyId: partyId, clientUnitId: clientUnitId);
            Entity party = TestDataBuilder.CreateParty(partyId, partyType: OneKycPartyType.BusinessClient, clientNumber: clientNumber);
            Entity clientUnit = TestDataBuilder.CreateClientUnit(clientUnitId, "Wealth Management");

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);
            _oneKycRepoMock.Setup(r => r.GetParty(partyId)).Returns(party);
            _oneKycRepoMock.Setup(r => r.GetClientUnit(clientUnitId)).Returns(clientUnit);

            _service.RouteApprovalAndAdvice(adviceId);

            _routingRuleRepoMock.Verify(r => r.GetActiveRoutingRules(), Times.Never);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenRoutingRiskUnmapped_ExitsEarly()
        {
            Guid adviceId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            Guid partyId = Guid.NewGuid();
            Guid clientUnitId = Guid.NewGuid();
            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, partyId: partyId, clientUnitId: clientUnitId, riskScore: "UnmappedRisk");
            Entity party = TestDataBuilder.CreateParty(partyId, partyType: OneKycPartyType.BusinessClient, clientNumber: "BC-1234");
            Entity clientUnit = TestDataBuilder.CreateClientUnit(clientUnitId, "Wealth Management");

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);
            _oneKycRepoMock.Setup(r => r.GetParty(partyId)).Returns(party);
            _oneKycRepoMock.Setup(r => r.GetClientUnit(clientUnitId)).Returns(clientUnit);

            _service.RouteApprovalAndAdvice(adviceId);

            _routingRuleRepoMock.Verify(r => r.GetActiveRoutingRules(), Times.Never);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenCaseGroupDiffersFromPartyGroup_UsesCaseGroupAndLogsWarning()
        {
            Guid adviceId = Guid.NewGuid();
            Guid caseId = Guid.NewGuid();
            Guid partyId = Guid.NewGuid();
            Guid clientUnitId = Guid.NewGuid();
            Guid caseGroupId = Guid.NewGuid();
            Guid partyGroupId = Guid.NewGuid();

            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, partyId: partyId, clientUnitId: clientUnitId, riskScore: "Medium", groupId: caseGroupId);
            Entity party = TestDataBuilder.CreateParty(partyId, partyType: OneKycPartyType.BusinessClient, clientNumber: "BC-1234", groupId: partyGroupId);
            Entity clientUnit = TestDataBuilder.CreateClientUnit(clientUnitId, "Wealth Management");

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);
            _oneKycRepoMock.Setup(r => r.GetParty(partyId)).Returns(party);
            _oneKycRepoMock.Setup(r => r.GetClientUnit(clientUnitId)).Returns(clientUnit);
            _oneKycRepoMock.Setup(r => r.GetPartiesByGroupId(caseGroupId)).Returns(new EntityCollection());

            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity>());

            _service.RouteApprovalAndAdvice(adviceId);

            // Verifies warning logged for group inconsistency
            _tracingMock.Verify(t => t.Trace(It.Is<string>(s => s.Contains("WARNING: OneKYC Case Group")), It.IsAny<object[]>()), Times.Once);
        }

        #endregion

        #region Rule Cardinality Tests (0, >1, Invalid Flow)

        [Fact]
        public void RouteApprovalAndAdvice_WhenZeroRulesMatch_ExitsCleanly()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId);

            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity>());

            _service.RouteApprovalAndAdvice(adviceId);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalTask(It.IsAny<Entity>()), Times.Never);
            _oneApprovalRepoMock.Verify(r => r.CreateApprovalCase(It.IsAny<Entity>()), Times.Never);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenMultipleRulesMatch_LogsConfigErrorAndExitsCleanly()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId);

            Entity rule1 = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 1", clientUnitMatchText: "Wealth");
            Entity rule2 = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 2", clientUnitMatchText: "Wealth");

            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity> { rule1, rule2 });

            _service.RouteApprovalAndAdvice(adviceId);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalTask(It.IsAny<Entity>()), Times.Never);
            _tracingMock.Verify(t => t.Trace(It.Is<string>(s => s.Contains("CONFIGURATION ERROR")), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenWinningRuleTargetFlowNull_LogsErrorAndExits()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId);

            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 1", targetFlow: null);

            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity> { rule });

            _service.RouteApprovalAndAdvice(adviceId);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalTask(It.IsAny<Entity>()), Times.Never);
            _tracingMock.Verify(t => t.Trace(It.Is<string>(s => s.Contains("has no Target OneApproval Flow")), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public void RouteApprovalAndAdvice_WhenWinningRuleTargetFlowUnsupported_LogsErrorAndExits()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId);

            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 1", targetFlow: OneApprovalFlow.Unrouted);

            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity> { rule });

            _service.RouteApprovalAndAdvice(adviceId);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalTask(It.IsAny<Entity>()), Times.Never);
            _tracingMock.Verify(t => t.Trace(It.Is<string>(s => s.Contains("has unsupported Flow value")), It.IsAny<object[]>()), Times.Once);
        }

        #endregion

        #region CREATE & UPDATE Workflow Tests

        [Fact]
        public void RouteApprovalAndAdvice_CreateMode_CreatesApprovalTaskAndCaseSuccessfully()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId);

            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 1", targetFlow: OneApprovalFlow.Sma);
            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity> { rule });

            // No active case exists
            _oneApprovalRepoMock.Setup(r => r.FindActiveApprovalCaseByOneKycCaseId(caseId)).Returns((Entity)null);

            Guid createdTaskId = Guid.NewGuid();
            Guid createdCaseId = Guid.NewGuid();

            _oneApprovalRepoMock.Setup(r => r.CreateApprovalTask(It.IsAny<Entity>())).Returns(createdTaskId);
            _oneApprovalRepoMock.Setup(r => r.CreateApprovalCase(It.IsAny<Entity>())).Returns(createdCaseId);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalTask(It.Is<Entity>(e =>
                e.GetAttributeValue<OptionSetValue>(ApprovalTaskEntity.Flow).Value == OneApprovalFlow.Sma)), Times.Once);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalCase(It.Is<Entity>(e =>
                e.GetAttributeValue<EntityReference>(ApprovalCaseEntity.ApprovalTask).Id == createdTaskId &&
                e.GetAttributeValue<OptionSetValue>(ApprovalCaseEntity.OneApprovalFlow).Value == OneApprovalFlow.Sma)), Times.Once);
        }

        [Fact]
        public void RouteApprovalAndAdvice_UpdateMode_ReusesExistingTaskAndUpdatesSparseFields()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId);

            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 1", targetFlow: OneApprovalFlow.Carc);
            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity> { rule });

            Guid existingTaskId = Guid.NewGuid();
            Guid existingCaseId = Guid.NewGuid();

            Entity existingTask = TestDataBuilder.CreateApprovalTask(existingTaskId, "Old Task Name", OneApprovalFlow.Sma);
            Entity existingCase = TestDataBuilder.CreateApprovalCase(existingCaseId, "Old Case Name", existingTaskId, caseId, flow: OneApprovalFlow.Sma);

            _oneApprovalRepoMock.Setup(r => r.FindActiveApprovalCaseByOneKycCaseId(caseId)).Returns(existingCase);
            _oneApprovalRepoMock.Setup(r => r.GetApprovalTask(existingTaskId)).Returns(existingTask);

            _service.RouteApprovalAndAdvice(adviceId);

            // Reuses task, updates changed flow and name
            _oneApprovalRepoMock.Verify(r => r.UpdateApprovalTask(It.Is<Entity>(e =>
                e.Id == existingTaskId &&
                e.GetAttributeValue<OptionSetValue>(ApprovalTaskEntity.Flow).Value == OneApprovalFlow.Carc)), Times.Once);

            // Updates case with new flow
            _oneApprovalRepoMock.Verify(r => r.UpdateApprovalCase(It.Is<Entity>(e =>
                e.Id == existingCaseId &&
                e.GetAttributeValue<OptionSetValue>(ApprovalCaseEntity.OneApprovalFlow).Value == OneApprovalFlow.Carc)), Times.Once);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalTask(It.IsAny<Entity>()), Times.Never);
            _oneApprovalRepoMock.Verify(r => r.CreateApprovalCase(It.IsAny<Entity>()), Times.Never);
        }

        [Fact]
        public void RouteApprovalAndAdvice_UpdateMode_WhenExistingCaseHasNoTask_CreatesNewTask()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId);

            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 1", targetFlow: OneApprovalFlow.Sma);
            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity> { rule });

            Guid existingCaseId = Guid.NewGuid();
            // Existing case has null task lookup
            Entity existingCase = TestDataBuilder.CreateApprovalCase(existingCaseId, "Old Case Name", taskId: null, oneKycCaseId: caseId);

            _oneApprovalRepoMock.Setup(r => r.FindActiveApprovalCaseByOneKycCaseId(caseId)).Returns(existingCase);

            Guid newTaskId = Guid.NewGuid();
            _oneApprovalRepoMock.Setup(r => r.CreateApprovalTask(It.IsAny<Entity>())).Returns(newTaskId);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalTask(It.IsAny<Entity>()), Times.Once);
            _oneApprovalRepoMock.Verify(r => r.UpdateApprovalCase(It.Is<Entity>(e =>
                e.Id == existingCaseId &&
                e.GetAttributeValue<EntityReference>(ApprovalCaseEntity.ApprovalTask).Id == newTaskId)), Times.Once);
        }

        [Fact]
        public void RouteApprovalAndAdvice_CaseAnalyst_WhenSingleAnalystFound_LinksAnalyst()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId);

            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 1", targetFlow: OneApprovalFlow.Sma);
            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity> { rule });

            Guid analystId = Guid.NewGuid();
            Entity analyst = TestDataBuilder.CreateCaseOwner(analystId, caseId);
            _oneKycRepoMock.Setup(r => r.FindCaseAnalyst(caseId)).Returns(analyst);

            _service.RouteApprovalAndAdvice(adviceId);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalCase(It.Is<Entity>(e =>
                e.GetAttributeValue<EntityReference>(ApprovalCaseEntity.CaseAnalyst).Id == analystId)), Times.Once);
        }

        [Fact]
        public void RouteApprovalAndAdvice_Group_WhenGroupRetrieveThrows_GracefullySetsComplexNameNull()
        {
            SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId, out Guid groupId);

            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Rule 1", targetFlow: OneApprovalFlow.Sma, routingScenario: RoutingScenario.LogicalGroup);
            _routingRuleRepoMock.Setup(r => r.GetActiveRoutingRules()).Returns(new List<Entity> { rule });

            _oneKycRepoMock.Setup(r => r.GetGroup(groupId)).Throws(new Exception("Database connection failure"));

            _service.RouteApprovalAndAdvice(adviceId);

            _oneApprovalRepoMock.Verify(r => r.CreateApprovalCase(It.Is<Entity>(e =>
                !e.Contains(ApprovalCaseEntity.ComplexName))), Times.Once);

            _tracingMock.Verify(t => t.Trace(It.Is<string>(s => s.Contains("Could not load optional Group display fields")), It.IsAny<object[]>()), Times.Never); // Logged in repo if handled
        }

        #endregion

        #region Helper Setup

        private void SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId)
        {
            Guid dummyGroupId;
            SetupValidRoutingPrerequisites(out adviceId, out caseId, out dummyGroupId, withGroup: false);
        }

        private void SetupValidRoutingPrerequisites(out Guid adviceId, out Guid caseId, out Guid groupId, bool withGroup = true)
        {
            adviceId = Guid.NewGuid();
            caseId = Guid.NewGuid();
            Guid partyId = Guid.NewGuid();
            Guid clientUnitId = Guid.NewGuid();
            groupId = withGroup ? Guid.NewGuid() : Guid.Empty;

            Entity advice = TestDataBuilder.CreateApprovalAndAdvice(adviceId, OneKycStage.MediumApproval, caseId);
            Entity kycCase = TestDataBuilder.CreateOneKycCase(caseId, partyId: partyId, clientUnitId: clientUnitId, riskScore: "Medium", groupId: withGroup ? (Guid?)groupId : null);
            Entity party = TestDataBuilder.CreateParty(partyId, partyType: OneKycPartyType.BusinessClient, clientNumber: "BC-1234");
            Entity clientUnit = TestDataBuilder.CreateClientUnit(clientUnitId, "Wealth Management");

            _oneKycRepoMock.Setup(r => r.GetApprovalAndAdvice(adviceId)).Returns(advice);
            _oneKycRepoMock.Setup(r => r.GetOneKycCase(caseId)).Returns(kycCase);
            _oneKycRepoMock.Setup(r => r.GetParty(partyId)).Returns(party);
            _oneKycRepoMock.Setup(r => r.GetClientUnit(clientUnitId)).Returns(clientUnit);

            if (withGroup)
            {
                _oneKycRepoMock.Setup(r => r.GetPartiesByGroupId(groupId)).Returns(new EntityCollection());
                _oneKycRepoMock.Setup(r => r.GetGroup(groupId)).Returns(TestDataBuilder.CreateGroup(groupId));
            }
        }

        #endregion
    }
}
