using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OneApproval.Plugins.Constants;

namespace OneApproval.Plugins.Repositories
{
    /// <summary>
    /// Data access repository for querying active Routing Rules from the ka_routingrule table.
    /// </summary>
    public class RoutingRuleRepository : IRoutingRuleRepository
    {
        private readonly IOrganizationService _service;

        /// <summary>
        /// Initializes a new instance of <see cref="RoutingRuleRepository"/>.
        /// </summary>
        /// <param name="service">Dataverse organization service.</param>
        public RoutingRuleRepository(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <inheritdoc/>
        public List<Entity> GetActiveRoutingRules()
        {
            QueryExpression query = new QueryExpression(RoutingRuleEntity.EntityLogicalName)
            {
                ColumnSet = new ColumnSet(
                    RoutingRuleEntity.Name,
                    RoutingRuleEntity.ClientUnitMatchText,
                    RoutingRuleEntity.ClientType,
                    RoutingRuleEntity.RoutingScenario,
                    RoutingRuleEntity.GroupComposition,
                    RoutingRuleEntity.RoutingRisk,
                    RoutingRuleEntity.OneKycStage,
                    RoutingRuleEntity.ComplianceAdvice,
                    RoutingRuleEntity.ComplianceOverrideMatch,
                    RoutingRuleEntity.OneKycCaseType,
                    RoutingRuleEntity.GcarcMatch,
                    RoutingRuleEntity.TmMatch,
                    RoutingRuleEntity.TargetOneApprovalFlow)
            };

            // Only active rules (statecode = 0) participate in routing
            query.Criteria.AddCondition(
                RoutingRuleEntity.StateCode,
                ConditionOperator.Equal,
                EntityState.Active);

            // Deterministic ordering by rule name (not priority-based)
            query.AddOrder(
                RoutingRuleEntity.Name,
                OrderType.Ascending);

            return _service.RetrieveMultiple(query).Entities.ToList();
        }
    }
}
