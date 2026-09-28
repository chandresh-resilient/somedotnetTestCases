using System;
using Microsoft.Xrm.Sdk;
using OneApproval.Plugins.Constants;
using OneApproval.Plugins.Repositories;

namespace OneApproval.Plugins.Services
{
    /// <summary>
    /// Service responsible for calculating the Logical Group Composition based on the member Parties.
    /// Follows the confirmed OneApproval business requirements and isolates group membership logic.
    /// </summary>
    public class GroupCompositionCalculator
    {
        private readonly IOneKycRepository _oneKycRepository;
        private readonly ITracingService _tracing;

        /// <summary>
        /// Initializes a new instance of <see cref="GroupCompositionCalculator"/>.
        /// </summary>
        /// <param name="oneKycRepository">Repository to query related OneKYC parties.</param>
        /// <param name="tracing">Tracing service for diagnostic logging.</param>
        public GroupCompositionCalculator(IOneKycRepository oneKycRepository, ITracingService tracing)
        {
            _oneKycRepository = oneKycRepository ?? throw new ArgumentNullException(nameof(oneKycRepository));
            _tracing = tracing;
        }

        /// <summary>
        /// Derives the Group Composition OptionSet value from actual Logical Group members without relying on unconfirmed GCARC fields.
        /// </summary>
        /// <remarks>
        /// Technical Calculation / Decision Tree:
        /// 1. If groupReference is null:
        ///    Scenario is Single BC Number -> Returns <see cref="GroupComposition.NotApplicable"/> (123900000).
        /// 2. Queries all 'aab_party' records where 'aab_groupid' equals the Group's ID.
        /// 3. If no member rows are returned:
        ///    Falls back conservatively to the current case's Party Type:
        ///    - If Natural Person (2) -> <see cref="GroupComposition.NpOnly"/> (123900001).
        ///    - If Business Client (1) -> <see cref="GroupComposition.BcOnly"/> (123900002).
        ///    - Otherwise -> <see cref="GroupComposition.Unknown"/> (123900004).
        /// 4. Iterates across member parties to check for presence of:
        ///    - Natural Persons (hasNp)
        ///    - Business Clients (hasBc)
        ///    - Unsupported/Missing Party Types (hasUnknown)
        /// 5. Evaluates composition hierarchy:
        ///    - hasUnknown takes precedence -> Returns <see cref="GroupComposition.Unknown"/> (123900004).
        ///    - hasNp AND hasBc -> Returns <see cref="GroupComposition.MixedBcAndNp"/> (123900003).
        ///    - hasNp ONLY -> Returns <see cref="GroupComposition.NpOnly"/> (123900001).
        ///    - hasBc ONLY -> Returns <see cref="GroupComposition.BcOnly"/> (123900002).
        ///    - Any other state -> Returns <see cref="GroupComposition.Unknown"/> (123900004).
        /// </remarks>
        /// <param name="groupReference">Lookup to the OneKYC Logical Group (aab_groupid), or null for single BC cases.</param>
        /// <param name="currentPartyType">The OneKYC Party Type Choice value of the primary case Party.</param>
        /// <returns>The resolved Group Composition choice value.</returns>
        public int Calculate(EntityReference groupReference, int currentPartyType)
        {
            // A Single BC Number case has no Logical Group, so group composition is not applicable.
            if (groupReference == null)
            {
                return GroupComposition.NotApplicable;
            }

            // Retrieve all parties linked to this logical group via aab_groupid.
            EntityCollection parties = _oneKycRepository.GetPartiesByGroupId(groupReference.Id);

            // If no party records were returned, use the current Party Type as a conservative fallback.
            if (parties.Entities.Count == 0)
            {
                _tracing?.Trace(
                    "Group {0} returned no Party rows. Falling back to the current Party Type for temporary Group Composition derivation.",
                    groupReference.Id);

                if (currentPartyType == OneKycPartyType.NaturalPerson)
                {
                    return GroupComposition.NpOnly;
                }

                if (currentPartyType == OneKycPartyType.BusinessClient)
                {
                    return GroupComposition.BcOnly;
                }

                return GroupComposition.Unknown;
            }

            bool hasNp = false;
            bool hasBc = false;
            bool hasUnknown = false;

            // Inspect every member party returned for this group.
            foreach (Entity groupParty in parties.Entities)
            {
                OptionSetValue memberPartyType = groupParty.GetAttributeValue<OptionSetValue>(PartyEntity.PartyType);

                // A missing or blank Party Type means group composition cannot be reliably determined.
                if (memberPartyType == null)
                {
                    hasUnknown = true;
                    continue;
                }

                if (memberPartyType.Value == OneKycPartyType.NaturalPerson)
                {
                    hasNp = true;
                }
                else if (memberPartyType.Value == OneKycPartyType.BusinessClient)
                {
                    hasBc = true;
                }
                else
                {
                    // Any numeric value other than BC (1) or NP (2) is unclassified in the confirmed model.
                    hasUnknown = true;
                }
            }

            // Unknown member data takes precedence so we never falsely classify incomplete data.
            if (hasUnknown)
            {
                return GroupComposition.Unknown;
            }

            // A group containing both Natural Persons and Business Clients is Mixed.
            if (hasNp && hasBc)
            {
                return GroupComposition.MixedBcAndNp;
            }

            // A group containing only Natural Persons.
            if (hasNp)
            {
                return GroupComposition.NpOnly;
            }

            // A group containing only Business Clients.
            if (hasBc)
            {
                return GroupComposition.BcOnly;
            }

            return GroupComposition.Unknown;
        }
    }
}
