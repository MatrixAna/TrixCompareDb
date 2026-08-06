namespace TrixCompareDb.Models
namespace TrixCompareDb.Models
{
    // Request to compare a table between two different databases using Azure AD MFA authentication
    public class CompareRequest
    {
        // Legacy properties (kept for backward compatibility, deprecated)
        public string DatabaseSource { get; set; }
        public string DatabaseTarget { get; set; }

        // MFA-based authentication properties (new recommended way)
        public string SourceServer { get; set; }
        public string TargetServer { get; set; }
        public string DatabaseName { get; set; }
        public string Email { get; set; }

        // Table name to compare in both databases
        public string TableName { get; set; }

        // Deprecated properties (kept for backward compatibility)
        [System.Obsolete("Use Email instead")]
        public string SourceEmail { get; set; }

        [System.Obsolete("Use Email instead")]
        public string TargetEmail { get; set; }

        [System.Obsolete("Use DatabaseName instead")]
        public string SourceDatabase { get; set; }

        [System.Obsolete("Use DatabaseName instead")]
        public string TargetDatabase { get; set; }
    }
}
