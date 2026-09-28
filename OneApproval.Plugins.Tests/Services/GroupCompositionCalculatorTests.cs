using System;
using Microsoft.Xrm.Sdk;
using Moq;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Repositories;
using OneApproval.Plugins.Services;
using OneApproval.Plugins.Tests.TestHelpers;
using Xunit;

namespace OneApproval.Plugins.Tests.Services
{
    public class GroupCompositionCalculatorTests
    {
        private readonly Mock<IOneKycRepository> _repoMock;
        private readonly Mock<ITracingService> _tracingMock;
        private readonly GroupCompositionCalculator _calculator;

        public GroupCompositionCalculatorTests()
        {
            _repoMock = new Mock<IOneKycRepository>();
            _tracingMock = new Mock<ITracingService>();
            _calculator = new GroupCompositionCalculator(_repoMock.Object, _tracingMock.Object);
        }

        [Fact]
        public void Calculate_WhenGroupReferenceNull_ReturnsNotApplicable()
        {
            int result = _calculator.Calculate(null, OneKycPartyType.BusinessClient);
            Assert.Equal(GroupComposition.NotApplicable, result);
        }

        [Theory]
        [InlineData(OneKycPartyType.NaturalPerson, GroupComposition.NpOnly)]
        [InlineData(OneKycPartyType.BusinessClient, GroupComposition.BcOnly)]
        [InlineData(999, GroupComposition.Unknown)]
        public void Calculate_WhenNoGroupPartiesFound_FallsBackToCurrentPartyType(int currentPartyType, int expected)
        {
            Guid groupId = Guid.NewGuid();
            EntityReference groupRef = new EntityReference(GroupEntity.EntityLogicalName, groupId);

            _repoMock.Setup(r => r.GetPartiesByGroupId(groupId))
                .Returns(new EntityCollection());

            int result = _calculator.Calculate(groupRef, currentPartyType);
            Assert.Equal(expected, result);
            _tracingMock.Verify(t => t.Trace(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public void Calculate_WhenGroupHasNaturalPersonsOnly_ReturnsNpOnly()
        {
            Guid groupId = Guid.NewGuid();
            EntityReference groupRef = new EntityReference(GroupEntity.EntityLogicalName, groupId);

            EntityCollection collection = new EntityCollection();
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "NP 1", OneKycPartyType.NaturalPerson));
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "NP 2", OneKycPartyType.NaturalPerson));

            _repoMock.Setup(r => r.GetPartiesByGroupId(groupId)).Returns(collection);

            int result = _calculator.Calculate(groupRef, OneKycPartyType.BusinessClient);
            Assert.Equal(GroupComposition.NpOnly, result);
        }

        [Fact]
        public void Calculate_WhenGroupHasBusinessClientsOnly_ReturnsBcOnly()
        {
            Guid groupId = Guid.NewGuid();
            EntityReference groupRef = new EntityReference(GroupEntity.EntityLogicalName, groupId);

            EntityCollection collection = new EntityCollection();
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "BC 1", OneKycPartyType.BusinessClient));
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "BC 2", OneKycPartyType.BusinessClient));

            _repoMock.Setup(r => r.GetPartiesByGroupId(groupId)).Returns(collection);

            int result = _calculator.Calculate(groupRef, OneKycPartyType.NaturalPerson);
            Assert.Equal(GroupComposition.BcOnly, result);
        }

        [Fact]
        public void Calculate_WhenGroupHasBothBcAndNp_ReturnsMixedBcAndNp()
        {
            Guid groupId = Guid.NewGuid();
            EntityReference groupRef = new EntityReference(GroupEntity.EntityLogicalName, groupId);

            EntityCollection collection = new EntityCollection();
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "BC 1", OneKycPartyType.BusinessClient));
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "NP 1", OneKycPartyType.NaturalPerson));

            _repoMock.Setup(r => r.GetPartiesByGroupId(groupId)).Returns(collection);

            int result = _calculator.Calculate(groupRef, OneKycPartyType.BusinessClient);
            Assert.Equal(GroupComposition.MixedBcAndNp, result);
        }

        [Fact]
        public void Calculate_WhenMemberPartyHasNullPartyType_ReturnsUnknown()
        {
            Guid groupId = Guid.NewGuid();
            EntityReference groupRef = new EntityReference(GroupEntity.EntityLogicalName, groupId);

            EntityCollection collection = new EntityCollection();
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "NP 1", OneKycPartyType.NaturalPerson));
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "Null Party Type", null));

            _repoMock.Setup(r => r.GetPartiesByGroupId(groupId)).Returns(collection);

            int result = _calculator.Calculate(groupRef, OneKycPartyType.NaturalPerson);
            Assert.Equal(GroupComposition.Unknown, result);
        }

        [Fact]
        public void Calculate_WhenMemberPartyHasUnsupportedPartyType_ReturnsUnknown()
        {
            Guid groupId = Guid.NewGuid();
            EntityReference groupRef = new EntityReference(GroupEntity.EntityLogicalName, groupId);

            EntityCollection collection = new EntityCollection();
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "BC 1", OneKycPartyType.BusinessClient));
            collection.Entities.Add(TestDataBuilder.CreateParty(Guid.NewGuid(), "Unsupported Type", 3)); // 3 is not BC or NP

            _repoMock.Setup(r => r.GetPartiesByGroupId(groupId)).Returns(collection);

            int result = _calculator.Calculate(groupRef, OneKycPartyType.BusinessClient);
            Assert.Equal(GroupComposition.Unknown, result);
        }
    }
}
