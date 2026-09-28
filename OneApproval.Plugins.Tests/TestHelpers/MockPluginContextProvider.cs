using System;
using Microsoft.Xrm.Sdk;
using Moq;

namespace OneApproval.Plugins.Tests.TestHelpers
{
    /// <summary>
    /// Builder class for creating mocked Dataverse plugin execution context and services with Moq.
    /// </summary>
    public class MockPluginContextProvider
    {
        public Mock<IServiceProvider> ServiceProviderMock { get; } = new Mock<IServiceProvider>();
        public Mock<IPluginExecutionContext> PluginExecutionContextMock { get; } = new Mock<IPluginExecutionContext>();
        public Mock<IOrganizationService> OrganizationServiceMock { get; } = new Mock<IOrganizationService>();
        public Mock<IOrganizationServiceFactory> OrganizationServiceFactoryMock { get; } = new Mock<IOrganizationServiceFactory>();
        public Mock<ITracingService> TracingServiceMock { get; } = new Mock<ITracingService>();

        public MockPluginContextProvider()
        {
            ServiceProviderMock.Setup(sp => sp.GetService(typeof(IPluginExecutionContext)))
                .Returns(PluginExecutionContextMock.Object);

            ServiceProviderMock.Setup(sp => sp.GetService(typeof(ITracingService)))
                .Returns(TracingServiceMock.Object);

            ServiceProviderMock.Setup(sp => sp.GetService(typeof(IOrganizationServiceFactory)))
                .Returns(OrganizationServiceFactoryMock.Object);

            OrganizationServiceFactoryMock.Setup(f => f.CreateOrganizationService(It.IsAny<Guid?>()))
                .Returns(OrganizationServiceMock.Object);
        }

        public MockPluginContextProvider WithMessage(string messageName)
        {
            PluginExecutionContextMock.Setup(c => c.MessageName).Returns(messageName);
            return this;
        }

        public MockPluginContextProvider WithEntityName(string entityName)
        {
            PluginExecutionContextMock.Setup(c => c.PrimaryEntityName).Returns(entityName);
            return this;
        }

        public MockPluginContextProvider WithStage(int stage)
        {
            PluginExecutionContextMock.Setup(c => c.Stage).Returns(stage);
            return this;
        }

        public MockPluginContextProvider WithDepth(int depth)
        {
            PluginExecutionContextMock.Setup(c => c.Depth).Returns(depth);
            return this;
        }

        public MockPluginContextProvider WithPrimaryEntityId(Guid id)
        {
            PluginExecutionContextMock.Setup(c => c.PrimaryEntityId).Returns(id);
            return this;
        }

        public MockPluginContextProvider WithInputParameter(string key, object value)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters[key] = value;
            PluginExecutionContextMock.Setup(c => c.InputParameters).Returns(parameters);
            return this;
        }
    }
}
