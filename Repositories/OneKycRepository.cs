using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OneApproval.Plugins.Constants;

namespace OneApproval.Plugins.Repositories
{
    /// <summary>
    /// Concrete Dataverse data access repository for retrieving OneKYC records.
    /// Uses targeted ColumnSets to optimize query performance and reduce bandwidth.
    /// </summary>
    public class OneKycRepository : IOneKycRepository
    {
        private readonly IOrganizationService _service;
        private readonly ITracingService _tracing;

        /// <summary>
        /// Initializes a new instance of <see cref="OneKycRepository"/>.
        /// </summary>
        /// <param name="service">Dataverse organization service connected under plugin execution user context.</param>
        /// <param name="tracing">Tracing service for diagnostic logging.</param>
        public OneKycRepository(IOrganizationService service, ITracingService tracing)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracing = tracing;
        }

        /// <inheritdoc/>
        public Entity GetApprovalAndAdvice(Guid id)
        {
            return _service.Retrieve(
                ApprovalAndAdviceEntity.EntityLogicalName,
                id,
                new ColumnSet(
                    ApprovalAndAdviceEntity.Stage,
                    ApprovalAndAdviceEntity.CaseId));
        }

        /// <inheritdoc/>
        public Entity GetOneKycCase(Guid id)
        {
            return _service.Retrieve(
                OneKycCaseEntity.EntityLogicalName,
                id,
                new ColumnSet(
                    OneKycCaseEntity.CaseNumber,
                    OneKycCaseEntity.CaseType,
                    OneKycCaseEntity.CurrentStageName,
                    OneKycCaseEntity.PartyId,
                    OneKycCaseEntity.ClientUnit,
                    OneKycCaseEntity.RiskScore,
                    OneKycCaseEntity.NextReviewDate,
                    OneKycCaseEntity.GroupId));
        }

        /// <inheritdoc/>
        public Entity GetParty(Guid id)
        {
            return _service.Retrieve(
                PartyEntity.EntityLogicalName,
                id,
                new ColumnSet(
                    PartyEntity.Name,
                    PartyEntity.PartyType,
                    PartyEntity.ClientNumber,
                    PartyEntity.Source,
                    PartyEntity.GroupId));
        }

        /// <inheritdoc/>
        public Entity GetClientUnit(Guid id)
        {
            return _service.Retrieve(
                ClientUnitEntity.EntityLogicalName,
                id,
                new ColumnSet(ClientUnitEntity.Name));
        }

        /// <inheritdoc/>
        public EntityCollection GetPartiesByGroupId(Guid groupId)
        {
            QueryExpression query = new QueryExpression(PartyEntity.EntityLogicalName)
            {
                ColumnSet = new ColumnSet(PartyEntity.PartyType)
            };

            query.Criteria.AddCondition(
                PartyEntity.GroupId,
                ConditionOperator.Equal,
                groupId);

            return _service.RetrieveMultiple(query);
        }

        /// <inheritdoc/>
        public Entity FindCaseAnalyst(Guid oneKycCaseId)
        {
            QueryExpression query = new QueryExpression(CaseOwnerEntity.EntityLogicalName)
            {
                ColumnSet = new ColumnSet(
                    CaseOwnerEntity.Id,
                    CaseOwnerEntity.Name,
                    CaseOwnerEntity.Username,
                    CaseOwnerEntity.RoleGroup),
                TopCount = 2 // Read at most 2 records to test for uniqueness vs ambiguity
            };

            query.Criteria.AddCondition(
                CaseOwnerEntity.CaseId,
                ConditionOperator.Equal,
                oneKycCaseId);

            query.Criteria.AddCondition(
                CaseOwnerEntity.RoleGroup,
                ConditionOperator.Equal,
                OneKycRoleGroup.CaseAnalyst);

            query.Criteria.AddCondition(
                CaseOwnerEntity.StateCode,
                ConditionOperator.Equal,
                EntityState.Active);

            EntityCollection result = _service.RetrieveMultiple(query);

            if (result.Entities.Count == 0)
            {
                _tracing?.Trace(
                    "No active OneKYC Case Analyst Case Owner was found for Case {0}. ka_caseanalyst will remain blank.",
                    oneKycCaseId);
                return null;
            }

            if (result.Entities.Count > 1)
            {
                _tracing?.Trace(
                    "More than one active OneKYC Case Analyst Case Owner was found for Case {0}. ka_caseanalyst will remain blank rather than choosing arbitrarily.",
                    oneKycCaseId);
                return null;
            }

            return result.Entities[0];
        }

        /// <inheritdoc/>
        public Entity GetGroup(Guid groupId)
        {
            try
            {
                return _service.Retrieve(
                    GroupEntity.EntityLogicalName,
                    groupId,
                    new ColumnSet(
                        GroupEntity.IdNumber,
                        GroupEntity.Name));
            }
            catch (Exception ex)
            {
                _tracing?.Trace(
                    "Could not load optional Group display fields for Group {0}: {1}",
                    groupId,
                    ex.Message);
                return null;
            }
        }
    }
}
