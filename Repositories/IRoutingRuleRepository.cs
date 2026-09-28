using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace OneApproval.Plugins.Repositories
{
    /// <summary>
    /// Contract for accessing active OneApproval routing rules configuration in Dataverse.
    /// </summary>
    public interface IRoutingRuleRepository
    {
        /// <summary>
        /// Retrieves all active ka_routingrule records with confirmed condition columns,
        /// sorted deterministically by rule name.
        /// </summary>
        /// <returns>List of active routing rule entities.</returns>
        List<Entity> GetActiveRoutingRules();
    }
}
