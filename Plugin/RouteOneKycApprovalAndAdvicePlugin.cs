using System;
using Microsoft.Xrm.Sdk;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Plugin;
using OneApproval.Plugins.Repositories;
using OneApproval.Plugins.Services;

namespace OneApproval.Plugins
{
    /// <summary>
    /// Entry point Dataverse Plugin registered on the 'Create' message of 'aab_approvalandadvice' in the PostOperation stage (40).
    /// Responsible for orchestrating the routing of OneKYC Approval and Advice events to OneApproval Cases and Tasks.
    /// </summary>
    /// <remarks>
    /// Fully matches original registration:
    /// - Full Type Name: OneApproval.Plugins.RouteOneKycApprovalAndAdvicePlugin
    /// - Message: Create
    /// - Primary Entity: aab_approvalandadvice
    /// - Stage: PostOperation (40)
    /// - Mode: Synchronous
    /// </remarks>
    public sealed class RouteOneKycApprovalAndAdvicePlugin : IPlugin
    {
        /// <summary>
        /// Standard entry point called by the Dataverse execution pipeline.
        /// Wraps execution in error handling to guarantee synchronous transaction rollback on failure.
        /// </summary>
        /// <param name="serviceProvider">Container supplying execution context, organization service, and tracing.</param>
        public void Execute(IServiceProvider serviceProvider)
        {
            LocalPluginContext localContext = new LocalPluginContext(serviceProvider);

            try
            {
                ExecuteInternal(localContext);
            }
            catch (InvalidPluginExecutionException)
            {
                // Preserve deliberately thrown Dataverse business exceptions
                throw;
            }
            catch (Exception ex)
            {
                // Trace unexpected technical exceptions to Plugin Trace Log
                localContext.Trace("RouteOneKycApprovalAndAdvicePlugin unexpected technical error: {0}", ex);

                // Convert to InvalidPluginExecutionException so Dataverse rolls back the transaction safely
                throw new InvalidPluginExecutionException(
                    "OneApproval routing failed because of a technical error. See Plugin Trace Log for details.",
                    ex);
            }
        }

        /// <summary>
        /// Validates pipeline execution prerequisites and delegates to <see cref="ApprovalRoutingService"/>.
        /// </summary>
        /// <param name="localContext">Context wrapper containing SDK services.</param>
        private static void ExecuteInternal(LocalPluginContext localContext)
        {
            IPluginExecutionContext context = localContext.PluginExecutionContext;

            // 1. Message check
            if (!string.Equals(context.MessageName, PluginMessage.Create, StringComparison.OrdinalIgnoreCase))
            {
                localContext.Trace("Exit: this plug-in only handles Create. Actual message = {0}.", context.MessageName);
                return;
            }

            // 2. Primary entity check
            if (!string.Equals(context.PrimaryEntityName, ApprovalAndAdviceEntity.EntityLogicalName, StringComparison.OrdinalIgnoreCase))
            {
                localContext.Trace("Exit: this plug-in only handles {0}. Actual entity = {1}.", ApprovalAndAdviceEntity.EntityLogicalName, context.PrimaryEntityName);
                return;
            }

            // 3. Stage check (PostOperation = 40)
            if (context.Stage != PluginStage.PostOperation)
            {
                localContext.Trace("Exit: expected PostOperation stage {0}. Actual stage = {1}.", PluginStage.PostOperation, context.Stage);
                return;
            }

            // 4. Execution depth check (recursion guard)
            if (context.Depth > ExecutionLimits.MaxDepth)
            {
                localContext.Trace("Exit: execution depth is {0}. Recursive execution is ignored.", context.Depth);
                return;
            }

            // 5. Retrieve created row ID with Target fallback
            Guid approvalAndAdviceId = context.PrimaryEntityId;

            if (approvalAndAdviceId == Guid.Empty && context.InputParameters.Contains(PluginParameter.Target))
            {
                if (context.InputParameters[PluginParameter.Target] is Entity target)
                {
                    approvalAndAdviceId = target.Id;
                }
            }

            if (approvalAndAdviceId == Guid.Empty)
            {
                localContext.Trace("Exit: Approval And Advice ID is empty.");
                return;
            }

            // 6. Initialize dependencies
            IOrganizationService service = localContext.OrganizationService;
            ITracingService tracing = localContext.TracingService;

            IOneKycRepository oneKycRepo = new OneKycRepository(service, tracing);
            IRoutingRuleRepository routingRuleRepo = new RoutingRuleRepository(service);
            IOneApprovalRepository oneApprovalRepo = new OneApprovalRepository(service);

            GroupCompositionCalculator groupCompositionCalculator = new GroupCompositionCalculator(oneKycRepo, tracing);
            RoutingRuleMatcher ruleMatcher = new RoutingRuleMatcher(tracing);

            ApprovalRoutingService routingService = new ApprovalRoutingService(
                oneKycRepo,
                routingRuleRepo,
                oneApprovalRepo,
                groupCompositionCalculator,
                ruleMatcher,
                tracing);

            // 7. Execute routing workflow
            routingService.RouteApprovalAndAdvice(approvalAndAdviceId);
        }
    }
}
