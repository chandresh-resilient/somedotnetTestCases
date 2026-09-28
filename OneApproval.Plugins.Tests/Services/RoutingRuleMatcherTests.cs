using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Moq;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Models;
using OneApproval.Plugins.Services;
using OneApproval.Plugins.Tests.TestHelpers;
using Xunit;

namespace OneApproval.Plugins.Tests.Services
{
    public class RoutingRuleMatcherTests
    {
        private readonly Mock<ITracingService> _tracingMock;
        private readonly RoutingRuleMatcher _matcher;

        public RoutingRuleMatcherTests()
        {
            _tracingMock = new Mock<ITracingService>();
            _matcher = new RoutingRuleMatcher(_tracingMock.Object);
        }

        #region Special Rule Skipping Tests

        [Fact]
        public void IsTemporarilyUnsupportedSpecialRule_WhenComplianceAdvicePopulated_ReturnsTrue()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ComplianceAdvice] = new OptionSetValue(123900000);

            Assert.True(_matcher.IsTemporarilyUnsupportedSpecialRule(rule));
        }

        [Theory]
        [InlineData(TriStateMatch.Yes, true)]
        [InlineData(999, true)] // Invalid Choice
        [InlineData(TriStateMatch.No, false)]
        [InlineData(TriStateMatch.Any, false)]
        public void IsTemporarilyUnsupportedSpecialRule_GCARC_EvaluatesCorrectly(int gcarcValue, bool expectedSkip)
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.GcarcMatch] = new OptionSetValue(gcarcValue);

            Assert.Equal(expectedSkip, _matcher.IsTemporarilyUnsupportedSpecialRule(rule));
        }

        [Theory]
        [InlineData(TriStateMatch.Yes, true)]
        [InlineData(999, true)] // Invalid Choice
        [InlineData(TriStateMatch.No, false)]
        [InlineData(TriStateMatch.Any, false)]
        public void IsTemporarilyUnsupportedSpecialRule_TM_EvaluatesCorrectly(int tmValue, bool expectedSkip)
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.TmMatch] = new OptionSetValue(tmValue);

            Assert.Equal(expectedSkip, _matcher.IsTemporarilyUnsupportedSpecialRule(rule));
        }

        [Theory]
        [InlineData(TriStateMatch.Yes, true)]
        [InlineData(999, true)] // Invalid Choice
        [InlineData(TriStateMatch.No, false)]
        [InlineData(TriStateMatch.Any, false)]
        public void IsTemporarilyUnsupportedSpecialRule_ComplianceOverride_EvaluatesCorrectly(int overrideValue, bool expectedSkip)
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ComplianceOverrideMatch] = new OptionSetValue(overrideValue);

            Assert.Equal(expectedSkip, _matcher.IsTemporarilyUnsupportedSpecialRule(rule));
        }

        [Fact]
        public void IsTemporarilyUnsupportedSpecialRule_WhenNormalRule_ReturnsFalse()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.GcarcMatch] = new OptionSetValue(TriStateMatch.No);
            rule[RoutingRuleEntity.TmMatch] = new OptionSetValue(TriStateMatch.No);
            rule[RoutingRuleEntity.ComplianceOverrideMatch] = new OptionSetValue(TriStateMatch.No);

            Assert.False(_matcher.IsTemporarilyUnsupportedSpecialRule(rule));
        }

        #endregion

        #region Catch-All Protection Tests

        [Fact]
        public void HasAnyConfirmedSpecificCondition_WhenAllConditionsBlank_ReturnsFalse()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.Name] = "Empty Catch-All Rule";

            Assert.False(_matcher.HasAnyConfirmedSpecificCondition(rule));
        }

        [Theory]
        [InlineData(RoutingRuleEntity.ClientUnitMatchText, "Wealth Management")]
        [InlineData(RoutingRuleEntity.ClientType, 1)]
        [InlineData(RoutingRuleEntity.RoutingScenario, 123900001)]
        [InlineData(RoutingRuleEntity.GroupComposition, 123900000)]
        [InlineData(RoutingRuleEntity.RoutingRisk, 123900000)]
        [InlineData(RoutingRuleEntity.OneKycStage, 745460009)]
        [InlineData(RoutingRuleEntity.OneKycCaseType, 123900000)]
        public void HasAnyConfirmedSpecificCondition_WhenOneConditionPopulated_ReturnsTrue(string attributeName, object value)
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            if (value is int intVal)
            {
                rule[attributeName] = new OptionSetValue(intVal);
            }
            else
            {
                rule[attributeName] = value;
            }

            Assert.True(_matcher.HasAnyConfirmedSpecificCondition(rule));
        }

        #endregion

        #region Client Unit Text Matching Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MatchClientUnitText_WhenRuleTextBlank_ReturnsTrue(string ruleText)
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ClientUnitMatchText] = ruleText;

            Assert.True(_matcher.MatchClientUnitText(rule, "Wealth Management Netherlands"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MatchClientUnitText_WhenActualNameBlankWithPopulatedRule_ReturnsFalse(string actualName)
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ClientUnitMatchText] = "Wealth Management";

            Assert.False(_matcher.MatchClientUnitText(rule, actualName));
        }

        [Fact]
        public void MatchClientUnitText_ExactMatchCaseInsensitive_ReturnsTrue()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ClientUnitMatchText] = "Corporate Banking";

            Assert.True(_matcher.MatchClientUnitText(rule, "corporate banking"));
            Assert.True(_matcher.MatchClientUnitText(rule, "Corporate Banking"));
            Assert.True(_matcher.MatchClientUnitText(rule, "  Corporate Banking  "));
        }

        [Fact]
        public void MatchClientUnitText_StartsWithPrefixWithSpaceBoundary_ReturnsTrue()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ClientUnitMatchText] = "Wealth Management";

            Assert.True(_matcher.MatchClientUnitText(rule, "Wealth Management Netherlands"));
            Assert.True(_matcher.MatchClientUnitText(rule, "Wealth Management France"));
        }

        [Fact]
        public void MatchClientUnitText_StartsWithWithoutSpaceBoundary_ReturnsFalse()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ClientUnitMatchText] = "Wealth";

            // "Wealth" without space must not match "WealthX"
            Assert.False(_matcher.MatchClientUnitText(rule, "WealthX Corporation"));
        }

        [Fact]
        public void MatchClientUnitText_NonPrefixSubstring_ReturnsFalse()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ClientUnitMatchText] = "Management";

            Assert.False(_matcher.MatchClientUnitText(rule, "Wealth Management"));
        }

        #endregion

        #region Choice Matching Tests

        [Fact]
        public void MatchChoice_WhenRuleAttributeNull_ReturnsTrue()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            Assert.True(_matcher.MatchChoice(rule, RoutingRuleEntity.ClientType, OneKycPartyType.BusinessClient));
        }

        [Fact]
        public void MatchChoice_WhenValuesEqual_ReturnsTrue()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ClientType] = new OptionSetValue(OneKycPartyType.BusinessClient);

            Assert.True(_matcher.MatchChoice(rule, RoutingRuleEntity.ClientType, OneKycPartyType.BusinessClient));
        }

        [Fact]
        public void MatchChoice_WhenValuesDiffer_ReturnsFalse()
        {
            Entity rule = new Entity(RoutingRuleEntity.EntityLogicalName, Guid.NewGuid());
            rule[RoutingRuleEntity.ClientType] = new OptionSetValue(OneKycPartyType.BusinessClient);

            Assert.False(_matcher.MatchChoice(rule, RoutingRuleEntity.ClientType, OneKycPartyType.NaturalPerson));
        }

        #endregion

        #region End-to-End Condition Matching Tests

        [Fact]
        public void RuleMatchesConfirmedConditions_WhenAllConditionsMatch_ReturnsTrue()
        {
            Entity rule = TestDataBuilder.CreateRoutingRule(
                Guid.NewGuid(),
                "Matching Rule",
                clientUnitMatchText: "Wealth Management",
                clientType: OneKycPartyType.BusinessClient,
                routingScenario: RoutingScenario.SingleBcNumber,
                groupComposition: GroupComposition.NotApplicable,
                routingRisk: RoutingRisk.Medium,
                oneKycStage: OneKycStage.MediumApproval,
                oneKycCaseType: 123900000);

            RoutingContextData context = new RoutingContextData
            {
                ClientUnitName = "Wealth Management Netherlands",
                ClientType = OneKycPartyType.BusinessClient,
                RoutingScenario = RoutingScenario.SingleBcNumber,
                GroupComposition = GroupComposition.NotApplicable,
                RoutingRisk = RoutingRisk.Medium,
                OneKycStage = OneKycStage.MediumApproval,
                OneKycCaseType = 123900000
            };

            Assert.True(_matcher.RuleMatchesConfirmedConditions(rule, context));
        }

        [Fact]
        public void RuleMatchesConfirmedConditions_WhenClientUnitFails_ReturnsFalse()
        {
            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), clientUnitMatchText: "Corporate Banking");
            RoutingContextData context = new RoutingContextData { ClientUnitName = "Wealth Management" };

            Assert.False(_matcher.RuleMatchesConfirmedConditions(rule, context));
        }

        [Fact]
        public void RuleMatchesConfirmedConditions_WhenClientTypeFails_ReturnsFalse()
        {
            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), clientType: OneKycPartyType.BusinessClient);
            RoutingContextData context = new RoutingContextData
            {
                ClientUnitName = "Wealth Management",
                ClientType = OneKycPartyType.NaturalPerson
            };

            Assert.False(_matcher.RuleMatchesConfirmedConditions(rule, context));
        }

        [Fact]
        public void RuleMatchesConfirmedConditions_WhenScenarioFails_ReturnsFalse()
        {
            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), routingScenario: RoutingScenario.LogicalGroup);
            RoutingContextData context = new RoutingContextData
            {
                ClientUnitName = "Wealth Management",
                ClientType = OneKycPartyType.BusinessClient,
                RoutingScenario = RoutingScenario.SingleBcNumber
            };

            Assert.False(_matcher.RuleMatchesConfirmedConditions(rule, context));
        }

        [Fact]
        public void RuleMatchesConfirmedConditions_WhenCompositionFails_ReturnsFalse()
        {
            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), groupComposition: GroupComposition.MixedBcAndNp);
            RoutingContextData context = new RoutingContextData
            {
                ClientUnitName = "Wealth Management",
                ClientType = OneKycPartyType.BusinessClient,
                RoutingScenario = RoutingScenario.SingleBcNumber,
                GroupComposition = GroupComposition.NotApplicable
            };

            Assert.False(_matcher.RuleMatchesConfirmedConditions(rule, context));
        }

        [Fact]
        public void RuleMatchesConfirmedConditions_WhenRiskFails_ReturnsFalse()
        {
            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), routingRisk: RoutingRisk.Increased);
            RoutingContextData context = new RoutingContextData
            {
                ClientUnitName = "Wealth Management",
                ClientType = OneKycPartyType.BusinessClient,
                RoutingScenario = RoutingScenario.SingleBcNumber,
                GroupComposition = GroupComposition.NotApplicable,
                RoutingRisk = RoutingRisk.Medium
            };

            Assert.False(_matcher.RuleMatchesConfirmedConditions(rule, context));
        }

        [Fact]
        public void RuleMatchesConfirmedConditions_WhenStageFails_ReturnsFalse()
        {
            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), oneKycStage: OneKycStage.IncreasedApproval);
            RoutingContextData context = new RoutingContextData
            {
                ClientUnitName = "Wealth Management",
                ClientType = OneKycPartyType.BusinessClient,
                RoutingScenario = RoutingScenario.SingleBcNumber,
                GroupComposition = GroupComposition.NotApplicable,
                RoutingRisk = RoutingRisk.Medium,
                OneKycStage = OneKycStage.MediumApproval
            };

            Assert.False(_matcher.RuleMatchesConfirmedConditions(rule, context));
        }

        [Fact]
        public void RuleMatchesConfirmedConditions_WhenCaseTypeFails_ReturnsFalse()
        {
            Entity rule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), oneKycCaseType: 123900001);
            RoutingContextData context = new RoutingContextData
            {
                ClientUnitName = "Wealth Management",
                ClientType = OneKycPartyType.BusinessClient,
                RoutingScenario = RoutingScenario.SingleBcNumber,
                GroupComposition = GroupComposition.NotApplicable,
                RoutingRisk = RoutingRisk.Medium,
                OneKycStage = OneKycStage.MediumApproval,
                OneKycCaseType = 123900000
            };

            Assert.False(_matcher.RuleMatchesConfirmedConditions(rule, context));
        }

        [Fact]
        public void FindMatchingRules_FiltersSpecialRulesAndReturnsOnlyMatchingRules()
        {
            Entity specialRule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Special GCARC Rule", gcarcMatch: TriStateMatch.Yes);
            Entity matchingRule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Valid Rule", clientUnitMatchText: "Wealth");
            Entity nonMatchingRule = TestDataBuilder.CreateRoutingRule(Guid.NewGuid(), "Wrong Rule", clientUnitMatchText: "Retail");

            List<Entity> activeRules = new List<Entity> { specialRule, matchingRule, nonMatchingRule };
            RoutingContextData context = new RoutingContextData
            {
                ClientUnitName = "Wealth Netherlands",
                ClientType = OneKycPartyType.BusinessClient,
                RoutingScenario = RoutingScenario.SingleBcNumber,
                GroupComposition = GroupComposition.NotApplicable,
                RoutingRisk = RoutingRisk.Medium,
                OneKycStage = OneKycStage.MediumApproval,
                OneKycCaseType = 123900000
            };

            List<Entity> matches = _matcher.FindMatchingRules(activeRules, context);

            Assert.Single(matches);
            Assert.Equal(matchingRule.Id, matches[0].Id);
        }

        #endregion
    }
}
