using Grouper2.Core.SddlParser;
using Newtonsoft.Json.Linq;
using System;

namespace Grouper2.Core
{

    public class ParseSddl
    {
        public static JObject ParseSddlString(
            string rawSddl,
            Action<string> debugWrite,
            Func<string, Sid> toSid,
            Func<string, Acl> toAcl,
            Func<Acl, JObject> toJObject)
        {
            Sddl sddl = new Sddl(rawSddl, debugWrite, toSid, toAcl);

            JObject sddlJObject = sddl.ToJObject(toJObject);

            return sddlJObject;
        }
    }
}