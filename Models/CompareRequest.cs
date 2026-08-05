namespace TrixCompareDb.Models
{
    // Request to compare a table between two different databases using Azure AD MFA authentication
    public class CompareRequest
    {
        // Legacy properties (kept for backward compatibility, deprecated)
        public string DatabaseSource { get; set; }
        public string DatabaseTarget { get; set; }

        // MFA-based authentication properties
        public string SourceServer { get; set; }
        public string SourceEmail { get; set; }
        public string TargetServer { get; set; }
        public string TargetEmail { get; set; }

        // Table name to compare in both databases
        public string TableName { get; set; }
    }
}
