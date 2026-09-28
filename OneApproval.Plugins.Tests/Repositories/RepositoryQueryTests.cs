using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Repositories;
using OneApproval.Plugins.Tests.TestHelpers;
using Xunit;

namespace OneApproval.Plugins.Tests.Repositories
{
    public class RepositoryQueryTests
    {
        private readonly Mock<IOrganizationService> _serviceMock;
        private readonly Mock<ITracingService> _tracingMock;

        public RepositoryQueryTests()
        {
            _serviceMock = new Mock<IOrganizationService>();
            _tracingMock = new Mock<ITracingService>();
        }

        #region OneKycRepository Tests

        [Fact]
        public void OneKycRepository_GetApprovalAndAdvice_RetrievesCorrectColumns()
        {
            Guid id = Guid.NewGuid();
            OneKycRepository repo = new OneKycRepository(_serviceMock.Object, _tracingMock.Object);

            repo.GetApprovalAndAdvice(id);

            _serviceMock.Verify(s => s.Retrieve(
                ApprovalAndAdviceEntity.EntityLogicalName,
                id,
                It.Is<ColumnSet>(c => c.Columns.Contains(ApprovalAndAdviceEntity.Stage) && c.Columns.Contains(ApprovalAndAdviceEntity.CaseId))), Times.Once);
        }

        [Fact]
        public void OneKycRepository_GetOneKycCase_RetrievesCorrectColumns()
        {
            Guid id = Guid.NewGuid();
            OneKycRepository repo = new OneKycRepository(_serviceMock.Object, _tracingMock.Object);

            repo.GetOneKycCase(id);

            _serviceMock.Verify(s => s.Retrieve(
                OneKycCaseEntity.EntityLogicalName,
                id,
                It.Is<ColumnSet>(c =>
                    c.Columns.Contains(OneKycCaseEntity.CaseNumber) &&
                    c.Columns.Contains(OneKycCaseEntity.CaseType) &&
                    c.Columns.Contains(OneKycCaseEntity.PartyId) &&
                    c.Columns.Contains(OneKycCaseEntity.ClientUnit) &&
                    c.Columns.Contains(OneKycCaseEntity.RiskScore))), Times.Once);
        }

        [Fact]
        public void OneKycRepository_GetPartiesByGroupId_BuildsCorrectQuery()
        {
            Guid groupId = Guid.NewGuid();
            OneKycRepository repo = new OneKycRepository(_serviceMock.Object, _tracingMock.Object);

            repo.GetPartiesByGroupId(groupId);

            _serviceMock.Verify(s => s.RetrieveMultiple(It.Is<QueryExpression>(q =>
                q.EntityName == PartyEntity.EntityLogicalName &&
                q.Criteria.Conditions.Count == 1 &&
                q.Criteria.Conditions[0].AttributeName == PartyEntity.GroupId &&
                (Guid)q.Criteria.Conditions[0].Values[0] == groupId)), Times.Once);
        }

        [Fact]
        public void OneKycRepository_FindCaseAnalyst_WhenZeroFound_ReturnsNull()
        {
            Guid caseId = Guid.NewGuid();
            OneKycRepository repo = new OneKycRepository(_serviceMock.Object, _tracingMock.Object);

            _serviceMock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryExpression>()))
                .Returns(new EntityCollection());

            Entity result = repo.FindCaseAnalyst(caseId);

            Assert.Null(result);
            _tracingMock.Verify(t => t.Trace(It.Is<string>(str => str.Contains("No active OneKYC Case Analyst")), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public void OneKycRepository_FindCaseAnalyst_WhenAmbiguous_ReturnsNull()
        {
            Guid caseId = Guid.NewGuid();
            OneKycRepository repo = new OneKycRepository(_serviceMock.Object, _tracingMock.Object);

            EntityCollection collection = new EntityCollection();
            collection.Entities.Add(TestDataBuilder.CreateCaseOwner(Guid.NewGuid(), caseId));
            collection.Entities.Add(TestDataBuilder.CreateCaseOwner(Guid.NewGuid(), caseId));

            _serviceMock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryExpression>()))
                .Returns(collection);

            Entity result = repo.FindCaseAnalyst(caseId);

            Assert.Null(result);
            _tracingMock.Verify(t => t.Trace(It.Is<string>(str => str.Contains("More than one active OneKYC Case Analyst")), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public void OneKycRepository_FindCaseAnalyst_WhenExactlyOne_ReturnsEntity()
        {
            Guid caseId = Guid.NewGuid();
            Guid analystId = Guid.NewGuid();
            OneKycRepository repo = new OneKycRepository(_serviceMock.Object, _tracingMock.Object);

            EntityCollection collection = new EntityCollection();
            collection.Entities.Add(TestDataBuilder.CreateCaseOwner(analystId, caseId));

            _serviceMock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryExpression>()))
                .Returns(collection);

            Entity result = repo.FindCaseAnalyst(caseId);

            Assert.NotNull(result);
            Assert.Equal(analystId, result.Id);
        }

        [Fact]
        public void OneKycRepository_GetGroup_WhenExceptionThrown_LogsAndReturnsNull()
        {
            Guid groupId = Guid.NewGuid();
            OneKycRepository repo = new OneKycRepository(_serviceMock.Object, _tracingMock.Object);

            _serviceMock.Setup(s => s.Retrieve(GroupEntity.EntityLogicalName, groupId, It.IsAny<ColumnSet>()))
                .Throws(new InvalidOperationException("CRM server unreachable"));

            Entity result = repo.GetGroup(groupId);

            Assert.Null(result);
            _tracingMock.Verify(t => t.Trace(It.Is<string>(str => str.Contains("Could not load optional Group display fields")), It.IsAny<object[]>()), Times.Once);
        }

        #endregion

        #region RoutingRuleRepository Tests

        [Fact]
        public void RoutingRuleRepository_GetActiveRoutingRules_QueriesActiveRulesOrderedByName()
        {
            RoutingRuleRepository repo = new RoutingRuleRepository(_serviceMock.Object);

            _serviceMock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryExpression>()))
                .Returns(new EntityCollection());

            List<Entity> result = repo.GetActiveRoutingRules();

            _serviceMock.Verify(s => s.RetrieveMultiple(It.Is<QueryExpression>(q =>
                q.EntityName == RoutingRuleEntity.EntityLogicalName &&
                q.Criteria.Conditions[0].AttributeName == RoutingRuleEntity.StateCode &&
                (int)q.Criteria.Conditions[0].Values[0] == EntityState.Active &&
                q.Orders[0].AttributeName == RoutingRuleEntity.Name &&
                q.Orders[0].OrderType == OrderType.Ascending)), Times.Once);
        }

        #endregion

        #region OneApprovalRepository Tests

        [Fact]
        public void OneApprovalRepository_FindActiveApprovalCaseByOneKycCaseId_BuildsCorrectQuery()
        {
            Guid caseId = Guid.NewGuid();
            OneApprovalRepository repo = new OneApprovalRepository(_serviceMock.Object);

            _serviceMock.Setup(s => s.RetrieveMultiple(It.IsAny<QueryExpression>()))
                .Returns(new EntityCollection());

            Entity result = repo.FindActiveApprovalCaseByOneKycCaseId(caseId);

            Assert.Null(result);
            _serviceMock.Verify(s => s.RetrieveMultiple(It.Is<QueryExpression>(q =>
                q.EntityName == ApprovalCaseEntity.EntityLogicalName &&
                q.TopCount == 1 &&
                q.Criteria.Conditions.Count == 2 &&
                q.Criteria.Conditions[0].AttributeName == ApprovalCaseEntity.OneKycCaseNumber &&
                (Guid)q.Criteria.Conditions[0].Values[0] == caseId &&
                q.Criteria.Conditions[1].AttributeName == ApprovalCaseEntity.StateCode &&
                (int)q.Criteria.Conditions[1].Values[0] == EntityState.Active)), Times.Once);
        }

        [Fact]
        public void OneApprovalRepository_GetApprovalTask_RetrievesCorrectColumns()
        {
            Guid taskId = Guid.NewGuid();
            OneApprovalRepository repo = new OneApprovalRepository(_serviceMock.Object);

            repo.GetApprovalTask(taskId);

            _serviceMock.Verify(s => s.Retrieve(
                ApprovalTaskEntity.EntityLogicalName,
                taskId,
                It.Is<ColumnSet>(c => c.Columns.Contains(ApprovalTaskEntity.Name) && c.Columns.Contains(ApprovalTaskEntity.Flow))), Times.Once);
        }

        [Fact]
        public void OneApprovalRepository_CreateAndUpdateMethods_CallOrganizationService()
        {
            OneApprovalRepository repo = new OneApprovalRepository(_serviceMock.Object);
            Entity task = new Entity(ApprovalTaskEntity.EntityLogicalName);
            Entity approvalCase = new Entity(ApprovalCaseEntity.EntityLogicalName);

            repo.CreateApprovalTask(task);
            repo.UpdateApprovalTask(task);
            repo.CreateApprovalCase(approvalCase);
            repo.UpdateApprovalCase(approvalCase);

            _serviceMock.Verify(s => s.Create(task), Times.Once);
            _serviceMock.Verify(s => s.Update(task), Times.Once);
            _serviceMock.Verify(s => s.Create(approvalCase), Times.Once);
            _serviceMock.Verify(s => s.Update(approvalCase), Times.Once);
        }

        #endregion
    }
}
