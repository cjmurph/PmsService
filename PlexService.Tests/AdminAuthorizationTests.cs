using System.Linq;
using System.Security.Principal;
using System.ServiceModel.Description;
using PlexServiceCommon.Interface;
using PlexServiceWCF;
using Xunit;

namespace PlexService.Tests
{
    public class AdminAuthorizationTests
    {
        /// <summary>
        /// Everything not listed here must need an administrator. A new operation fails this test until someone decides.
        /// </summary>
        private static readonly string[] OpenToEveryone =
        {
            "GetStatus", "GetSettings", "GetWebLink", "GetPmsDataPath", "IsAuxAppRunning", "GetLogPath",
            "Subscribe", "UnSubscribe", "LogMessage", "CanControl", "GetUsedDriveLetters",
            "UpdateAllLibraries", "UpdateLibraries", "GetLibraries"
        };

        [Fact]
        public void Only_the_listed_operations_are_open_to_everyone()
        {
            var contract = ContractDescription.GetContract(typeof(ITrayInteraction));
            var open = contract.Operations
                .Select(o => o.Messages[0].Action)
                .Where(AdminAuthorizationManager.IsOpenToEveryone)
                .Select(a => a.Substring(a.LastIndexOf('/') + 1))
                .OrderBy(n => n);

            Assert.Equal(OpenToEveryone.OrderBy(n => n), open);
        }

        [Theory]
        [InlineData("http://tempuri.org/ITrayInteraction/SetSettings")]
        [InlineData("http://tempuri.org/ITrayInteraction/StartAuxApp")]
        [InlineData("http://tempuri.org/ITrayInteraction/GetStatus/SetSettings")]
        [InlineData("http://tempuri.org/ITrayInteraction/")]
        public void Changes_need_an_administrator(string action)
        {
            Assert.False(AdminAuthorizationManager.IsOpenToEveryone(action));
        }

        [Fact]
        public void Nobody_is_privileged_without_an_identity()
        {
            Assert.False(AdminAuthorizationManager.IsPrivileged(null));
            Assert.False(AdminAuthorizationManager.IsPrivileged(WindowsIdentity.GetAnonymous()));
        }
    }
}
