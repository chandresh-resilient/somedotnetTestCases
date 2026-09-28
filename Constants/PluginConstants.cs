namespace OneApproval.Plugins.Constants
{
    /// <summary>
    /// Pipeline execution stages for Microsoft Dataverse plugins.
    /// </summary>
    public static class PluginStage
    {
        public const int PreValidation = 10;
        public const int PreOperation = 20;
        public const int PostOperation = 40;
    }

    /// <summary>
    /// Standard Microsoft Dataverse SDK message names.
    /// </summary>
    public static class PluginMessage
    {
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Delete = "Delete";
    }

    /// <summary>
    /// Standard Dataverse execution context input parameter keys.
    /// </summary>
    public static class PluginParameter
    {
        public const string Target = "Target";
    }

    /// <summary>
    /// Standard Dataverse entity state codes.
    /// </summary>
    public static class EntityState
    {
        public const int Active = 0;
        public const int Inactive = 1;
    }

    /// <summary>
    /// Execution safety constraints.
    /// </summary>
    public static class ExecutionLimits
    {
        public const int MaxDepth = 1;
    }
}
