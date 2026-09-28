using System;
using Microsoft.Xrm.Sdk;

namespace OneApproval.Plugins.Repositories
{
    /// <summary>
    /// Contract for persistence operations on OneApproval entities (Approval Task and KYC Approval Case).
    /// </summary>
    public interface IOneApprovalRepository
    {
        /// <summary>
        /// Queries for an existing active KYC Approval Case linked to the specified OneKYC Case.
        /// Returns null if no active Case exists.
        /// </summary>
        Entity FindActiveApprovalCaseByOneKycCaseId(Guid oneKycCaseId);

        /// <summary>
        /// Retrieves the linked Approval Task with name and flow attributes for sparse update comparison.
        /// </summary>
        Entity GetApprovalTask(Guid taskId);

        /// <summary>
        /// Creates a new Approval Task record.
        /// </summary>
        Guid CreateApprovalTask(Entity task);

        /// <summary>
        /// Updates an existing Approval Task record.
        /// </summary>
        void UpdateApprovalTask(Entity task);

        /// <summary>
        /// Creates a new KYC Approval Case record.
        /// </summary>
        Guid CreateApprovalCase(Entity approvalCase);

        /// <summary>
        /// Updates an existing KYC Approval Case record.
        /// </summary>
        void UpdateApprovalCase(Entity approvalCase);
    }
}
