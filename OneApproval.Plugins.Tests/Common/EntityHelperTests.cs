using System;
using Microsoft.Xrm.Sdk;
using Moq;
using OneApproval.Plugins.Common;
using Xunit;

namespace OneApproval.Plugins.Tests.Common
{
    public class EntityHelperTests
    {
        [Fact]
        public void AreValuesEqual_BothNull_ReturnsTrue()
        {
            Assert.True(EntityHelper.AreValuesEqual(null, null));
        }

        [Fact]
        public void AreValuesEqual_OneNullOneNonNull_ReturnsFalse()
        {
            Assert.False(EntityHelper.AreValuesEqual(null, "someValue"));
            Assert.False(EntityHelper.AreValuesEqual("someValue", null));
            Assert.False(EntityHelper.AreValuesEqual(new OptionSetValue(1), null));
            Assert.False(EntityHelper.AreValuesEqual(null, new EntityReference("account", Guid.NewGuid())));
        }

        [Fact]
        public void AreValuesEqual_EntityReference_EvaluatesIdAndLogicalNameCaseInsensitively()
        {
            Guid id1 = Guid.NewGuid();
            Guid id2 = Guid.NewGuid();

            EntityReference ref1 = new EntityReference("aab_case", id1);
            EntityReference ref2 = new EntityReference("AAB_CASE", id1);
            EntityReference refDiffId = new EntityReference("aab_case", id2);
            EntityReference refDiffEntity = new EntityReference("aab_party", id1);

            Assert.True(EntityHelper.AreValuesEqual(ref1, ref2));
            Assert.False(EntityHelper.AreValuesEqual(ref1, refDiffId));
            Assert.False(EntityHelper.AreValuesEqual(ref1, refDiffEntity));
            Assert.False(EntityHelper.AreValuesEqual(ref1, "NotAnEntityReference"));
            Assert.False(EntityHelper.AreValuesEqual("NotAnEntityReference", ref1));
        }

        [Fact]
        public void AreValuesEqual_OptionSetValue_EvaluatesValue()
        {
            OptionSetValue opt1 = new OptionSetValue(100);
            OptionSetValue opt2 = new OptionSetValue(100);
            OptionSetValue optDiff = new OptionSetValue(200);

            Assert.True(EntityHelper.AreValuesEqual(opt1, opt2));
            Assert.False(EntityHelper.AreValuesEqual(opt1, optDiff));
            Assert.False(EntityHelper.AreValuesEqual(opt1, 100)); // int vs OptionSetValue
            Assert.False(EntityHelper.AreValuesEqual(100, opt1));
        }

        [Fact]
        public void AreValuesEqual_String_EvaluatesOrdinalEquality()
        {
            Assert.True(EntityHelper.AreValuesEqual("ExactMatch", "ExactMatch"));
            Assert.False(EntityHelper.AreValuesEqual("CaseDiff", "casediff"));
            Assert.False(EntityHelper.AreValuesEqual("First", "Second"));
        }

        [Fact]
        public void AreValuesEqual_DateTime_EvaluatesEquality()
        {
            DateTime dt1 = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
            DateTime dt2 = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
            DateTime dtDiff = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

            Assert.True(EntityHelper.AreValuesEqual(dt1, dt2));
            Assert.False(EntityHelper.AreValuesEqual(dt1, dtDiff));
            Assert.False(EntityHelper.AreValuesEqual(dt1, "2026-09-21"));
        }

        [Fact]
        public void AreValuesEqual_GenericObjects_EvaluatesObjectEquals()
        {
            Assert.True(EntityHelper.AreValuesEqual(12345, 12345));
            Assert.False(EntityHelper.AreValuesEqual(12345, 67890));
            Assert.True(EntityHelper.AreValuesEqual(true, true));
            Assert.False(EntityHelper.AreValuesEqual(true, false));
        }

        [Fact]
        public void AddIfDifferent_WhenValuesEqual_DoesNotAddAttribute()
        {
            Mock<ITracingService> tracingMock = new Mock<ITracingService>();
            Entity existing = new Entity("test");
            existing["field1"] = "ExistingValue";
            existing["choice1"] = new OptionSetValue(10);

            Entity updateTarget = new Entity("test");

            EntityHelper.AddIfDifferent(updateTarget, existing, "field1", "ExistingValue", tracingMock.Object, "TestRecord");
            EntityHelper.AddIfDifferent(updateTarget, existing, "choice1", new OptionSetValue(10), tracingMock.Object, "TestRecord");
            EntityHelper.AddIfDifferent(updateTarget, existing, "nonExistentField", null, tracingMock.Object, "TestRecord");

            Assert.Empty(updateTarget.Attributes);
        }

        [Fact]
        public void AddIfDifferent_WhenValuesDiffer_AddsAttributeToUpdateTarget()
        {
            Mock<ITracingService> tracingMock = new Mock<ITracingService>();
            Entity existing = new Entity("test");
            existing["field1"] = "OldValue";

            Entity updateTarget = new Entity("test");

            EntityHelper.AddIfDifferent(updateTarget, existing, "field1", "NewValue", tracingMock.Object, "TestRecord");

            Assert.True(updateTarget.Contains("field1"));
            Assert.Equal("NewValue", updateTarget["field1"]);
            tracingMock.Verify(t => t.Trace(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public void AddIfDifferent_WhenExistingPopulatedAndProposedNull_AddsNullToClearAttribute()
        {
            Mock<ITracingService> tracingMock = new Mock<ITracingService>();
            Entity existing = new Entity("test");
            existing["field1"] = "ValueToClear";

            Entity updateTarget = new Entity("test");

            EntityHelper.AddIfDifferent(updateTarget, existing, "field1", null, tracingMock.Object, "TestRecord");

            Assert.True(updateTarget.Contains("field1"));
            Assert.Null(updateTarget["field1"]);
        }

        [Fact]
        public void SetIfNotNull_WhenValueNotNull_SetsAttribute()
        {
            Entity entity = new Entity("test");
            EntityHelper.SetIfNotNull(entity, "attr1", "MyValue");

            Assert.True(entity.Contains("attr1"));
            Assert.Equal("MyValue", entity["attr1"]);
        }

        [Fact]
        public void SetIfNotNull_WhenValueIsNull_DoesNotSetAttribute()
        {
            Entity entity = new Entity("test");
            EntityHelper.SetIfNotNull(entity, "attr1", null);

            Assert.False(entity.Contains("attr1"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SetStringIfNotBlank_WhenNullOrWhitespace_DoesNotSetAttribute(string value)
        {
            Entity entity = new Entity("test");
            EntityHelper.SetStringIfNotBlank(entity, "strField", value);

            Assert.False(entity.Contains("strField"));
        }

        [Fact]
        public void SetStringIfNotBlank_WhenPopulated_SetsTrimmedValue()
        {
            Entity entity = new Entity("test");
            EntityHelper.SetStringIfNotBlank(entity, "strField", "  hello world  ");

            Assert.True(entity.Contains("strField"));
            Assert.Equal("hello world", entity["strField"]);
        }

        [Fact]
        public void BuildRecordName_WithCaseNumber_BuildsStandardName()
        {
            string name = EntityHelper.BuildRecordName("KYC Approval", "CASE-9999", "SMA");
            Assert.Equal("KYC Approval - CASE-9999 - SMA", name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BuildRecordName_WithoutCaseNumber_BuildsFallbackNameWithTimestamp(string caseNumber)
        {
            string name = EntityHelper.BuildRecordName("Approval Task", caseNumber, "CARC");
            Assert.StartsWith("Approval Task - CARC - ", name);
        }
    }
}
