using System;
using Microsoft.Xrm.Sdk;
using Moq;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Plugin;
using OneApproval.Plugins.Tests.TestHelpers;
using Xunit;

namespace OneApproval.Plugins.Tests.Plugin
{
    public class RouteOneKycApprovalAndAdvicePluginTests
    {
        private readonly RouteOneKycApprovalAndAdvicePlugin _plugin;

        public RouteOneKycApprovalAndAdvicePluginTests()
        {
            _plugin = new RouteOneKycApprovalAndAdvicePlugin();
        }

        [Theory]
        [InlineData("Update")]
        [InlineData("Delete")]
        [InlineData("Retrieve")]
        public void Execute_WhenMessageNotCreate_ExitsWithoutAction(string message)
        {
            MockPluginContextProvider contextProvider = new MockPluginContextProvider()
                .WithMessage(message)
                .WithEntityName(ApprovalAndAdviceEntity.EntityLogicalName)
                .WithStage(PluginStage.PostOperation)
                .WithDepth(1)
                .WithPrimaryEntityId(Guid.NewGuid());

            _plugin.Execute(contextProvider.ServiceProviderMock.Object);

            contextProvider.OrganizationServiceMock.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()), Times.Never);
        }

        [Theory]
        [InlineData("account")]
        [InlineData("contact")]
        [InlineData("aab_case")]
        public void Execute_WhenEntityNotApprovalAndAdvice_ExitsWithoutAction(string entityName)
        {
            MockPluginContextProvider contextProvider = new MockPluginContextProvider()
                .WithMessage(PluginMessage.Create)
                .WithEntityName(entityName)
                .WithStage(PluginStage.PostOperation)
                .WithDepth(1)
                .WithPrimaryEntityId(Guid.NewGuid());

            _plugin.Execute(contextProvider.ServiceProviderMock.Object);

            contextProvider.OrganizationServiceMock.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()), Times.Never);
        }

        [Theory]
        [InlineData(PluginStage.PreValidation)]
        [InlineData(PluginStage.PreOperation)]
        public void Execute_WhenStageNotPostOperation_ExitsWithoutAction(int stage)
        {
            MockPluginContextProvider contextProvider = new MockPluginContextProvider()
                .WithMessage(PluginMessage.Create)
                .WithEntityName(ApprovalAndAdviceEntity.EntityLogicalName)
                .WithStage(stage)
                .WithDepth(1)
                .WithPrimaryEntityId(Guid.NewGuid());

            _plugin.Execute(contextProvider.ServiceProviderMock.Object);

            contextProvider.OrganizationServiceMock.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()), Times.Never);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public void Execute_WhenDepthGreaterThanOne_ExitsDueToRecursionGuard(int depth)
        {
            MockPluginContextProvider contextProvider = new MockPluginContextProvider()
                .WithMessage(PluginMessage.Create)
                .WithEntityName(ApprovalAndAdviceEntity.EntityLogicalName)
                .WithStage(PluginStage.PostOperation)
                .WithDepth(depth)
                .WithPrimaryEntityId(Guid.NewGuid());

            _plugin.Execute(contextProvider.ServiceProviderMock.Object);

            contextProvider.OrganizationServiceMock.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()), Times.Never);
        }

        [Fact]
        public void Execute_WhenPrimaryEntityIdEmptyAndNoTarget_ExitsWithoutAction()
        {
            MockPluginContextProvider contextProvider = new MockPluginContextProvider()
                .WithMessage(PluginMessage.Create)
                .WithEntityName(ApprovalAndAdviceEntity.EntityLogicalName)
                .WithStage(PluginStage.PostOperation)
                .WithDepth(1)
                .WithPrimaryEntityId(Guid.Empty);

            contextProvider.PluginExecutionContextMock.Setup(c => c.InputParameters).Returns(new ParameterCollection());

            _plugin.Execute(contextProvider.ServiceProviderMock.Object);

            contextProvider.OrganizationServiceMock.Verify(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()), Times.Never);
        }

        [Fact]
        public void Execute_WhenPrimaryEntityIdEmpty_FallsBackToTargetInputParameter()
        {
            Guid targetId = Guid.NewGuid();
            Entity targetEntity = new Entity(ApprovalAndAdviceEntity.EntityLogicalName, targetId);

            MockPluginContextProvider contextProvider = new MockPluginContextProvider()
                .WithMessage(PluginMessage.Create)
                .WithEntityName(ApprovalAndAdviceEntity.EntityLogicalName)
                .WithStage(PluginStage.PostOperation)
                .WithDepth(1)
                .WithPrimaryEntityId(Guid.Empty)
                .WithInputParameter(PluginParameter.Target, targetEntity);

            // Setup retrieve of approval and advice with stage = null to trigger early exit after ID resolved
            contextProvider.OrganizationServiceMock
                .Setup(s => s.Retrieve(ApprovalAndAdviceEntity.EntityLogicalName, targetId, It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()))
                .Returns(new Entity(ApprovalAndAdviceEntity.EntityLogicalName, targetId));

            _plugin.Execute(contextProvider.ServiceProviderMock.Object);

            contextProvider.OrganizationServiceMock.Verify(s => s.Retrieve(ApprovalAndAdviceEntity.EntityLogicalName, targetId, It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()), Times.Once);
        }

        [Fact]
        public void Execute_WhenInvalidPluginExecutionExceptionThrown_RethrowsDirectly()
        {
            Guid id = Guid.NewGuid();
            MockPluginContextProvider contextProvider = new MockPluginContextProvider()
                .WithMessage(PluginMessage.Create)
                .WithEntityName(ApprovalAndAdviceEntity.EntityLogicalName)
                .WithStage(PluginStage.PostOperation)
                .WithDepth(1)
                .WithPrimaryEntityId(id);

            contextProvider.OrganizationServiceMock
                .Setup(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()))
                .Throws(new InvalidPluginExecutionException("Deliberate business failure"));

            InvalidPluginExecutionException ex = Assert.Throws<InvalidPluginExecutionException>(() =>
                _plugin.Execute(contextProvider.ServiceProviderMock.Object));

            Assert.Equal("Deliberate business failure", ex.Message);
        }

        [Fact]
        public void Execute_WhenUnexpectedExceptionThrown_WrapsInInvalidPluginExecutionException()
        {
            Guid id = Guid.NewGuid();
            MockPluginContextProvider contextProvider = new MockPluginContextProvider()
                .WithMessage(PluginMessage.Create)
                .WithEntityName(ApprovalAndAdviceEntity.EntityLogicalName)
                .WithStage(PluginStage.PostOperation)
                .WithDepth(1)
                .WithPrimaryEntityId(id);

            contextProvider.OrganizationServiceMock
                .Setup(s => s.Retrieve(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Microsoft.Xrm.Sdk.Query.ColumnSet>()))
                .Throws(new TimeoutException("Database connection timeout"));

            InvalidPluginExecutionException ex = Assert.Throws<InvalidPluginExecutionException>(() =>
                _plugin.Execute(contextProvider.ServiceProviderMock.Object));

            Assert.Contains("OneApproval routing failed because of a technical error", ex.Message);
            Assert.IsType<TimeoutException>(ex.InnerException);
            contextProvider.TracingServiceMock.Verify(t => t.Trace(It.Is<string>(s => s.Contains("unexpected technical error")), It.IsAny<object[]>()), Times.Once);
        }
    }
}
