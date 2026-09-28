using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OneApproval.Plugins.Constants;

namespace OneApproval.Plugins.Repositories
{
    /// <summary>
    /// Concrete Dataverse data access repository for managing OneApproval Case and Task records.
    /// </summary>
    public class OneApprovalRepository : IOneApprovalRepository
    {
        private readonly IOrganizationService _service;

        /// <summary>
        /// Initializes a new instance of <see cref="OneApprovalRepository"/>.
        /// </summary>
        /// <param name="service">Dataverse organization service.</param>
        public OneApprovalRepository(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <inheritdoc/>
        public Entity FindActiveApprovalCaseByOneKycCaseId(Guid oneKycCaseId)
        {
            QueryExpression query = new QueryExpression(ApprovalCaseEntity.EntityLogicalName)
            {
                ColumnSet = new ColumnSet(
                    ApprovalCaseEntity.Id,
                    ApprovalCaseEntity.Name,
                    ApprovalCaseEntity.OneApprovalFlow,
                    ApprovalCaseEntity.ApprovalTask,
                    ApprovalCaseEntity.OneKycCaseNumber,
                    ApprovalCaseEntity.CaseType,
                    ApprovalCaseEntity.CurrentStageOneKyc,
                    ApprovalCaseEntity.PartyName,
                    ApprovalCaseEntity.PartyType,
                    ApprovalCaseEntity.ClientUnit,
                    ApprovalCaseEntity.RiskScore,
                    ApprovalCaseEntity.NextReviewDate,
                    ApprovalCaseEntity.ClientNumber,
                    ApprovalCaseEntity.SourceSystem,
                    ApprovalCaseEntity.GroupId,
                    ApprovalCaseEntity.CaseAnalyst,
                    ApprovalCaseEntity.ClientName,
                    ApprovalCaseEntity.ComplexName),
                TopCount = 1
            };

            query.Criteria.AddCondition(
                ApprovalCaseEntity.OneKycCaseNumber,
                ConditionOperator.Equal,
                oneKycCaseId);

            query.Criteria.AddCondition(
                ApprovalCaseEntity.StateCode,
                ConditionOperator.Equal,
                EntityState.Active);

            EntityCollection result = _service.RetrieveMultiple(query);
            return result.Entities.Count == 0 ? null : result.Entities[0];
        }

        /// <inheritdoc/>
        public Entity GetApprovalTask(Guid taskId)
        {
            return _service.Retrieve(
                ApprovalTaskEntity.EntityLogicalName,
                taskId,
                new ColumnSet(
                    ApprovalTaskEntity.Name,
                    ApprovalTaskEntity.Flow));
        }

        /// <inheritdoc/>
        public Guid CreateApprovalTask(Entity task)
        {
            return _service.Create(task);
        }

        /// <inheritdoc/>
        public void UpdateApprovalTask(Entity task)
        {
            _service.Update(task);
        }

        /// <inheritdoc/>
        public Guid CreateApprovalCase(Entity approvalCase)
        {
            return _service.Create(approvalCase);
        }

        /// <inheritdoc/>
        public void UpdateApprovalCase(Entity approvalCase)
        {
            _service.Update(approvalCase);
        }
    }
}
