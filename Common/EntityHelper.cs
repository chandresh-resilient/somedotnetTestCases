using System;
using Microsoft.Xrm.Sdk;

namespace OneApproval.Plugins.Common
{
    /// <summary>
    /// Utility methods for comparing and updating Dataverse Entity attributes safely.
    /// </summary>
    public static class EntityHelper
    {
        /// <summary>
        /// Compares common Dataverse SDK value types for logical equality.
        /// Handles EntityReference, OptionSetValue, strings, DateTime, and nulls.
        /// </summary>
        public static bool AreValuesEqual(object left, object right)
        {
            if (left == null && right == null)
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            EntityReference leftReference = left as EntityReference;
            EntityReference rightReference = right as EntityReference;
            if (leftReference != null || rightReference != null)
            {
                if (leftReference == null || rightReference == null)
                {
                    return false;
                }

                return leftReference.Id == rightReference.Id &&
                       string.Equals(
                           leftReference.LogicalName,
                           rightReference.LogicalName,
                           StringComparison.OrdinalIgnoreCase);
            }

            OptionSetValue leftChoice = left as OptionSetValue;
            OptionSetValue rightChoice = right as OptionSetValue;
            if (leftChoice != null || rightChoice != null)
            {
                if (leftChoice == null || rightChoice == null)
                {
                    return false;
                }

                return leftChoice.Value == rightChoice.Value;
            }

            string leftText = left as string;
            string rightText = right as string;
            if (leftText != null || rightText != null)
            {
                if (leftText == null || rightText == null)
                {
                    return false;
                }

                return string.Equals(leftText, rightText, StringComparison.Ordinal);
            }

            DateTime? leftDate = left is DateTime ? (DateTime?)((DateTime)left) : null;
            DateTime? rightDate = right is DateTime ? (DateTime?)((DateTime)right) : null;
            if (leftDate.HasValue || rightDate.HasValue)
            {
                return leftDate.HasValue &&
                       rightDate.HasValue &&
                       leftDate.Value == rightDate.Value;
            }

            return object.Equals(left, right);
        }

        /// <summary>
        /// Adds an attribute to a sparse update Entity only if the proposed value differs from the existing record.
        /// If proposedValue is null and existing value is present, sets null so Dataverse clears the field.
        /// </summary>
        public static void AddIfDifferent(
            Entity updateTarget,
            Entity existingRecord,
            string attributeName,
            object proposedValue,
            ITracingService tracing,
            string recordDescription)
        {
            object existingValue = existingRecord != null && existingRecord.Contains(attributeName)
                ? existingRecord[attributeName]
                : null;

            if (AreValuesEqual(existingValue, proposedValue))
            {
                return;
            }

            updateTarget[attributeName] = proposedValue;

            tracing?.Trace(
                proposedValue == null
                    ? "{0}: field {1} changed to blank/null and will be cleared."
                    : "{0}: field {1} changed and will be updated.",
                recordDescription,
                attributeName);
        }

        /// <summary>
        /// Adds a value to an Entity only when the value is not null.
        /// Used for clean CREATE operations so missing optional fields stay blank.
        /// </summary>
        public static void SetIfNotNull(Entity target, string attributeName, object value)
        {
            if (value != null)
            {
                target[attributeName] = value;
            }
        }

        /// <summary>
        /// Writes a string attribute only when the string is not blank.
        /// </summary>
        public static void SetStringIfNotBlank(Entity entity, string attributeName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            entity[attributeName] = value.Trim();
        }

        /// <summary>
        /// Builds standard human-readable record names for generated Approval Tasks and KYC Approval Cases.
        /// </summary>
        public static string BuildRecordName(string prefix, string oneKycCaseNumber, string flowLabel)
        {
            if (!string.IsNullOrWhiteSpace(oneKycCaseNumber))
            {
                return prefix + " - " + oneKycCaseNumber.Trim() + " - " + flowLabel;
            }

            return prefix + " - " + flowLabel + " - " + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        }
    }
}
