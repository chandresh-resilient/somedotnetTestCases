using System;
using Microsoft.Xrm.Sdk;

namespace OneApproval.Plugins.Plugin
{
    /// <summary>
    /// Encapsulates standard Dataverse plugin execution services from IServiceProvider.
    /// Follows the standard Microsoft Power Platform plugin architecture pattern.
    /// </summary>
    public class LocalPluginContext
    {
        public IServiceProvider ServiceProvider { get; }
        public IPluginExecutionContext PluginExecutionContext { get; }
        public IOrganizationService OrganizationService { get; }
        public ITracingService TracingService { get; }
        public IOrganizationServiceFactory OrganizationServiceFactory { get; }

        public LocalPluginContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            TracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            OrganizationServiceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            if (PluginExecutionContext != null && OrganizationServiceFactory != null)
            {
                OrganizationService = OrganizationServiceFactory.CreateOrganizationService(PluginExecutionContext.UserId);
            }
        }

        public void Trace(string message, params object[] args)
        {
            TracingService?.Trace(message, args);
        }
    }
}
