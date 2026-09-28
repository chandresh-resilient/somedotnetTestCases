using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Models;

namespace OneApproval.Plugins.Services
{
    /// <summary>
    /// Service responsible for filtering active Routing Rules and matching them against the current Routing Context.
    /// Handles temporary rule exclusions (GCARC, TM, Compliance) and evaluates confirmed routing conditions.
    /// </summary>
    public class RoutingRuleMatcher
    {
        private readonly ITracingService _tracing;

        /// <summary>
        /// Initializes a new instance of <see cref="RoutingRuleMatcher"/>.
        /// </summary>
        /// <param name="tracing">Tracing service for diagnostic logging.</param>
        public RoutingRuleMatcher(ITracingService tracing)
        {
            _tracing = tracing;
        }

        /// <summary>
        /// Evaluates active rules, skips temporarily unsupported special rules, and returns all matching rules.
        /// </summary>
        /// <param name="activeRules">List of active ka_routingrule entities.</param>
        /// <param name="context">In-memory context containing source and derived case values.</param>
        /// <returns>List of matching Routing Rule entities.</returns>
        public List<Entity> FindMatchingRules(IEnumerable<Entity> activeRules, RoutingContextData context)
        {
            List<Entity> matchingRules = new List<Entity>();

            foreach (Entity rule in activeRules)
            {
                // Filter out rules requiring unconfirmed source attributes (GCARC, TM, Compliance Advice/Override).
                if (IsTemporarilyUnsupportedSpecialRule(rule))
                {
                    continue;
                }

                // Check all confirmed conditions (Client Unit, Client Type, Scenario, Composition, Risk, Stage, Case Type).
                if (RuleMatchesConfirmedConditions(rule, context))
                {
                    matchingRules.Add(rule);
                }
            }

            return matchingRules;
        }

        /// <summary>
        /// Determines if a rule depends on temporarily unsupported special inputs.
        /// </summary>
        /// <remarks>
        /// Business Exclusion Rules:
        /// 1. Compliance Advice populated -> Rule is advice-specific -> SKIP.
        /// 2. GCARC Match = Yes (123900001) -> Rule requires special GCARC logic -> SKIP.
        /// 3. GCARC Match has invalid Choice value -> Configuration invalid -> SKIP.
        /// 4. TM Match = Yes (123900001) -> Rule requires special TM logic -> SKIP.
        /// 5. TM Match has invalid Choice value -> Configuration invalid -> SKIP.
        /// 6. Compliance Override Match = Yes (123900001) -> Rule requires override logic -> SKIP.
        /// 7. Compliance Override Match has invalid Choice value -> Configuration invalid -> SKIP.
        ///
        /// Rules having GCARC/TM/Override = 'No' or 'Any' are allowed to participate as normal rules.
        /// </remarks>
        /// <param name="rule">The routing rule to inspect.</param>
        /// <returns>True if the rule should be temporarily skipped; otherwise false.</returns>
        public bool IsTemporarilyUnsupportedSpecialRule(Entity rule)
        {
            string ruleName = rule.GetAttributeValue<string>(RoutingRuleEntity.Name) ?? "(blank)";

            // Check Compliance Advice condition
            OptionSetValue complianceAdvice = rule.GetAttributeValue<OptionSetValue>(RoutingRuleEntity.ComplianceAdvice);
            if (complianceAdvice != null)
            {
                _tracing?.Trace(
                    "TEMPORARY SKIP: Rule {0} ({1}) contains Compliance Advice value {2}. " +
                    "Compliance Advice source mapping is not yet confirmed.",
                    rule.Id,
                    ruleName,
                    complianceAdvice.Value);
                return true;
            }

            // Check GCARC Match condition
            OptionSetValue gcarcMatch = rule.GetAttributeValue<OptionSetValue>(RoutingRuleEntity.GcarcMatch);
            if (gcarcMatch != null && gcarcMatch.Value == TriStateMatch.Yes)
            {
                _tracing?.Trace(
                    "TEMPORARY SKIP: Rule {0} ({1}) has GCARC Match = Yes. " +
                    "GCARC source handling is not yet confirmed.",
                    rule.Id,
                    ruleName);
                return true;
            }

            if (gcarcMatch != null &&
                gcarcMatch.Value != TriStateMatch.Any &&
                gcarcMatch.Value != TriStateMatch.Yes &&
                gcarcMatch.Value != TriStateMatch.No)
            {
                _tracing?.Trace(
                    "TEMPORARY SKIP: Rule {0} ({1}) has invalid GCARC Match value {2}.",
                    rule.Id,
                    ruleName,
                    gcarcMatch.Value);
                return true;
            }

            // Check TM Match condition
            OptionSetValue tmMatch = rule.GetAttributeValue<OptionSetValue>(RoutingRuleEntity.TmMatch);
            if (tmMatch != null && tmMatch.Value == TriStateMatch.Yes)
            {
                _tracing?.Trace(
                    "TEMPORARY SKIP: Rule {0} ({1}) has TM Match = Yes. " +
                    "The authoritative OneKYC TM source is not yet confirmed.",
                    rule.Id,
                    ruleName);
                return true;
            }

            if (tmMatch != null &&
                tmMatch.Value != TriStateMatch.Any &&
                tmMatch.Value != TriStateMatch.Yes &&
                tmMatch.Value != TriStateMatch.No)
            {
                _tracing?.Trace(
                    "TEMPORARY SKIP: Rule {0} ({1}) has invalid TM Match value {2}.",
                    rule.Id,
                    ruleName,
                    tmMatch.Value);
                return true;
            }

            // Check Compliance Override Match condition
            OptionSetValue complianceOverrideMatch = rule.GetAttributeValue<OptionSetValue>(RoutingRuleEntity.ComplianceOverrideMatch);
            if (complianceOverrideMatch != null && complianceOverrideMatch.Value == TriStateMatch.Yes)
            {
                _tracing?.Trace(
                    "TEMPORARY SKIP: Rule {0} ({1}) has Compliance Override Match = Yes. " +
                    "The OneKYC manual-override business meaning is not yet confirmed.",
                    rule.Id,
                    ruleName);
                return true;
            }

            if (complianceOverrideMatch != null &&
                complianceOverrideMatch.Value != TriStateMatch.Any &&
                complianceOverrideMatch.Value != TriStateMatch.Yes &&
                complianceOverrideMatch.Value != TriStateMatch.No)
            {
                _tracing?.Trace(
                    "TEMPORARY SKIP: Rule {0} ({1}) has invalid Compliance Override Match value {2}.",
                    rule.Id,
                    ruleName,
                    complianceOverrideMatch.Value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Evaluates only the currently confirmed Routing Rule conditions against the context.
        /// All conditions are evaluated with logical AND. A blank condition on the rule means 'Any'.
        /// </summary>
        /// <param name="rule">The routing rule to evaluate.</param>
        /// <param name="context">The resolved routing context.</param>
        /// <returns>True if every populated condition matches; otherwise false.</returns>
        public bool RuleMatchesConfirmedConditions(Entity rule, RoutingContextData context)
        {
            string ruleName = rule.GetAttributeValue<string>(RoutingRuleEntity.Name) ?? "(blank)";

            // Guard: An empty rule with 0 conditions must never become an accidental catch-all winner.
            if (!HasAnyConfirmedSpecificCondition(rule))
            {
                _tracing?.Trace(
                    "Rule {0} ({1}) is ignored because all currently supported routing conditions are blank/Any.",
                    rule.Id,
                    ruleName);
                return false;
            }

            // 1. Client Unit text condition (exact match or prefix with space boundary)
            if (!MatchClientUnitText(rule, context.ClientUnitName))
            {
                return false;
            }

            // 2. Client Type (Party Type: BC or NP)
            if (!MatchChoice(rule, RoutingRuleEntity.ClientType, context.ClientType))
            {
                return false;
            }

            // 3. Routing Scenario (Single BC Number or Logical Group)
            if (!MatchChoice(rule, RoutingRuleEntity.RoutingScenario, context.RoutingScenario))
            {
                return false;
            }

            // 4. Group Composition (Not Applicable, NP Only, BC Only, Mixed, Unknown)
            if (!MatchChoice(rule, RoutingRuleEntity.GroupComposition, context.GroupComposition))
            {
                return false;
            }

            // 5. Routing Risk (Medium, Increased, Unacceptable)
            if (!MatchChoice(rule, RoutingRuleEntity.RoutingRisk, context.RoutingRisk))
            {
                return false;
            }

            // 6. OneKYC Stage (Medium Approval, Compliance Advice, Increased Approval, CARC Approval)
            if (!MatchChoice(rule, RoutingRuleEntity.OneKycStage, context.OneKycStage))
            {
                return false;
            }

            // 7. Case Type
            if (!MatchChoice(rule, RoutingRuleEntity.OneKycCaseType, context.OneKycCaseType))
            {
                return false;
            }

            _tracing?.Trace("Supported rule matched: {0} ({1}).", rule.Id, ruleName);
            return true;
        }

        /// <summary>
        /// Validates that at least one supported condition is populated on the rule to prevent unintentional catch-alls.
        /// </summary>
        public bool HasAnyConfirmedSpecificCondition(Entity rule)
        {
            if (!string.IsNullOrWhiteSpace(rule.GetAttributeValue<string>(RoutingRuleEntity.ClientUnitMatchText)))
            {
                return true;
            }

            if (rule.Contains(RoutingRuleEntity.ClientType)) return true;
            if (rule.Contains(RoutingRuleEntity.RoutingScenario)) return true;
            if (rule.Contains(RoutingRuleEntity.GroupComposition)) return true;
            if (rule.Contains(RoutingRuleEntity.RoutingRisk)) return true;
            if (rule.Contains(RoutingRuleEntity.OneKycStage)) return true;
            if (rule.Contains(RoutingRuleEntity.OneKycCaseType)) return true;

            return false;
        }

        /// <summary>
        /// Compares the actual OneKYC Client Unit name with the text configured on the Routing Rule.
        /// </summary>
        /// <remarks>
        /// Technical Matching Logic:
        /// 1. Blank rule text -> Returns true ('Any' Client Unit).
        /// 2. Blank source Client Unit name -> Returns false.
        /// 3. Exact string match (case-insensitive) -> Returns true.
        /// 4. Prefix match: checks if actual name starts with (ruleText + " ") (case-insensitive).
        ///    Adding a trailing space boundary avoids partial token matches (e.g. 'Wealth' matching 'WealthX'),
        ///    while successfully matching sub-units like 'Wealth Management Netherlands'.
        /// </remarks>
        /// <param name="rule">The routing rule entity containing ka_clientunitmatchtext.</param>
        /// <param name="actualClientUnitName">The human-readable name of the OneKYC Client Unit.</param>
        /// <returns>True if condition matches; otherwise false.</returns>
        public bool MatchClientUnitText(Entity rule, string actualClientUnitName)
        {
            string ruleClientUnitText = rule.GetAttributeValue<string>(RoutingRuleEntity.ClientUnitMatchText);

            // Blank rule text means Any Client Unit.
            if (string.IsNullOrWhiteSpace(ruleClientUnitText))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(actualClientUnitName))
            {
                _tracing?.Trace(
                    "Client Unit comparison failed because the actual OneKYC Client Unit name is blank while rule {0} contains Client Unit Match Text '{1}'.",
                    rule.Id,
                    ruleClientUnitText);
                return false;
            }

            ruleClientUnitText = ruleClientUnitText.Trim();
            actualClientUnitName = actualClientUnitName.Trim();

            // 1. Exact string match
            if (string.Equals(actualClientUnitName, ruleClientUnitText, StringComparison.OrdinalIgnoreCase))
            {
                _tracing?.Trace(
                    "Client Unit exact match for rule {0}. Actual='{1}', RuleText='{2}'.",
                    rule.Id,
                    actualClientUnitName,
                    ruleClientUnitText);
                return true;
            }

            // 2. StartsWith match with space delimiter
            string rulePrefix = ruleClientUnitText + " ";
            if (actualClientUnitName.StartsWith(rulePrefix, StringComparison.OrdinalIgnoreCase))
            {
                _tracing?.Trace(
                    "Client Unit StartsWith match for rule {0}. Actual='{1}', RuleText='{2}'.",
                    rule.Id,
                    actualClientUnitName,
                    ruleClientUnitText);
                return true;
            }

            _tracing?.Trace(
                "Client Unit did not match rule {0}. Actual='{1}', RuleText='{2}'.",
                rule.Id,
                actualClientUnitName,
                ruleClientUnitText);

            return false;
        }

        /// <summary>
        /// Compares an OptionSet choice condition.
        /// Blank condition on the rule acts as a wildcard ('Any').
        /// Populated condition requires exact integer value match.
        /// </summary>
        /// <param name="rule">The routing rule entity.</param>
        /// <param name="attributeName">Logical name of the Choice attribute.</param>
        /// <param name="actual">Actual integer value from the routing context.</param>
        /// <returns>True if match or blank; false on mismatch.</returns>
        public bool MatchChoice(Entity rule, string attributeName, int actual)
        {
            OptionSetValue expected = rule.GetAttributeValue<OptionSetValue>(attributeName);
            if (expected == null)
            {
                return true;
            }

            return expected.Value == actual;
        }
    }
}
