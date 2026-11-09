using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.ServiceModel;
using Serilog;

namespace PlexServiceWCF
{
    /// <summary>
    /// Only lets administrators (or the account the service runs as) call operations that change something.
    /// Reading the state stays open for every authenticated user.
    /// </summary>
    public class AdminAuthorizationManager : ServiceAuthorizationManager
    {
        private static readonly string[] ReadOnlyOperations =
        {
            "GetStatus", "GetSettings", "GetWebLink", "GetPmsDataPath", "IsAuxAppRunning",
            "GetLogPath", "Subscribe", "UnSubscribe", "LogMessage", "CanControl"
        };

        private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);

        private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);

        private static readonly SecurityIdentifier ServiceAccount = WindowsIdentity.GetCurrent().User;

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            var action = operationContext.IncomingMessageHeaders.Action ?? string.Empty;
            var operation = action.Substring(action.LastIndexOf('/') + 1);

            // metadata exchange and anything that isn't part of our contract is left to WCF
            if (!action.Contains("/ITrayInteraction/")) return true;
            if (ReadOnlyOperations.Contains(operation)) return true;

            var identity = operationContext.ServiceSecurityContext?.WindowsIdentity;
            if (IsPrivileged(identity)) return true;

            Log.Warning($"Denied '{operation}' for {identity?.Name ?? "anonymous"}, administrator rights are required");
            return false;
        }

        internal static bool IsPrivileged(WindowsIdentity identity)
        {
            if (identity == null || identity.IsAnonymous || identity.User == null) return false;
            if (identity.User == ServiceAccount || identity.User == LocalSystem) return true;

            // with UAC the administrators group is only present as a deny-only group unless the caller is elevated,
            // we still want to treat that account as an administrator.
            if (identity.Groups?.Contains(Administrators) == true) return true;
            return identity.Claims.Any(c =>
                (c.Type == ClaimTypes.DenyOnlySid || c.Type == ClaimTypes.GroupSid) &&
                string.Equals(c.Value, Administrators.Value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
