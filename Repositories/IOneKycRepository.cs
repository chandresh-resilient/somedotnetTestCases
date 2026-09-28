using System;
using Microsoft.Xrm.Sdk;

namespace OneApproval.Plugins.Repositories
{
    /// <summary>
    /// Contract for data access operations against OneKYC entities in Microsoft Dataverse.
    /// </summary>
    public interface IOneKycRepository
    {
        /// <summary>
        /// Retrieves the triggering Approval &amp; Advice record with stage and case lookup columns.
        /// </summary>
        Entity GetApprovalAndAdvice(Guid id);

        /// <summary>
        /// Retrieves the parent OneKYC Case record with confirmed routing and detail mapping columns.
        /// </summary>
        Entity GetOneKycCase(Guid id);

        /// <summary>
        /// Retrieves the OneKYC Party record containing Party Type, Client Number, and Source System.
        /// </summary>
        Entity GetParty(Guid id);

        /// <summary>
        /// Retrieves the OneKYC Client Unit record containing its readable name.
        /// </summary>
        Entity GetClientUnit(Guid id);

        /// <summary>
        /// Retrieves all member Party records belonging to a specified Logical Group.
        /// </summary>
        EntityCollection GetPartiesByGroupId(Guid groupId);

        /// <summary>
        /// Finds the unique active Case Owner record acting as Case Analyst (Role Group = 958630000).
        /// Returns null if 0 or multiple matching records are found to prevent arbitrary assignment.
        /// </summary>
        Entity FindCaseAnalyst(Guid oneKycCaseId);

        /// <summary>
        /// Retrieves optional Logical Group display information (group ID number and group name).
        /// </summary>
        Entity GetGroup(Guid groupId);
    }
}
