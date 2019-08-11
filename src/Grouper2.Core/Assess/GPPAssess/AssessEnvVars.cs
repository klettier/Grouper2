using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;
using System;

namespace Grouper2.Core.GPPAssess
{
    public partial class AssessGpp
    {
        // ReSharper disable once UnusedMember.Local
        private JObject GetAssessedEnvironmentVariables(JObject gppCategory, int intLevelToShow, Action<string> debugWrite)
        {

            int interestLevel = 1;

            if (interestLevel < intLevelToShow)
            {
                return null;
            }

            JObject assessedGppEvs = new JObject();

            if (gppCategory["EnvironmentVariable"] is JArray)
            {
                foreach (JToken gppEv in gppCategory["EnvironmentVariable"])
                {
                    JProperty assessedGppEv = AssessGppEv(gppEv, debugWrite);
                    assessedGppEvs.Add(assessedGppEv);
                }
            }
            else
            {
                JProperty assessedGppEv = AssessGppEv(gppCategory["EnvironmentVariable"], debugWrite);
                assessedGppEvs.Add(assessedGppEv);
            }

            return assessedGppEvs;
        }

        static JProperty AssessGppEv(JToken gppEv, Action<string> debugWrite)
        {
            JObject assessedGppEv = new JObject
            {
                {"Name", JUtil.GetSafeString(gppEv, "@name")},
                {"Status", JUtil.GetSafeString(gppEv, "@status")},
                {"Changed", JUtil.GetSafeString(gppEv, "@changed")},
                {"Action", JUtil.GetActionString(gppEv["Properties"]["@action"].ToString(), debugWrite)}
            };
            return new JProperty(JUtil.GetSafeString(gppEv, "@uid"), assessedGppEv);
        }
    }
}