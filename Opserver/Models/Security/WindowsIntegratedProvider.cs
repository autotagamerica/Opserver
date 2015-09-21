using System;
using System.Linq;
using System.Security.Principal;
using System.Web;

namespace StackExchange.Opserver.Models.Security
{
    public class WindowsIntegratedProvider : SecurityProvider
    {
        public override bool InGroups(string groupNames, string accountName)
        {
            var context = HttpContext.Current;
            if (context == null) return false;
            
            var groups = groupNames.Split(StringSplits.Comma_SemiColon);
            var principal = new WindowsPrincipal((WindowsIdentity) context.User.Identity);
            
            return groups.Any(groupName => principal.IsInRole(groupName));
        }

        public override bool ValidateUser(string userName, string password)
        {
            var context = HttpContext.Current;
            if (context == null) return false;

            return context.User.Identity.IsAuthenticated;
        }
    }
}