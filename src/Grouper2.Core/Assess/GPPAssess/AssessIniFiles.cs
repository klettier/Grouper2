using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;
using System;

namespace Grouper2.Core.GPPAssess
{
    public partial class AssessGpp
    {
        // ReSharper disable once UnusedMember.Local
        private JObject GetAssessedIniFiles(JObject gppCategory, int intLevelToShow)
        {

            JObject assessedGppInis = new JObject();

            if (gppCategory["Ini"] is JArray)
            {
                foreach (JToken gppIni in gppCategory["Ini"])
                {
                    JProperty assessedGppIni = AssessGppIni(gppIni, debugWrite, investigatePath, investigateString, intLevelToShow);
                    assessedGppInis.Add(assessedGppIni);
                }
            }
            else
            {
                JProperty assessedGppIni = AssessGppIni(gppCategory["Ini"], debugWrite, investigatePath, investigateString, intLevelToShow);
                if (assessedGppIni != null)
                {
                    assessedGppInis.Add(assessedGppIni);
                }
            }

            if (assessedGppInis.HasValues)
            {
                return assessedGppInis;
            }
            else
            {
                return null;
            }

        }

        static JProperty AssessGppIni(
            JToken gppIni,
            Action<string> debugWrite,
            Func<string, JObject> investigatePath,
            Func<string, JObject> investigateString,
            int intLevelToShow)
        {
            int interestLevel = 1;
            string gppIniUid = JUtil.GetSafeString(gppIni, "@uid");
            string gppIniName = JUtil.GetSafeString(gppIni, "@name");
            string gppIniChanged = JUtil.GetSafeString(gppIni, "@changed");
            string gppIniStatus = JUtil.GetSafeString(gppIni, "@status");

            JToken gppIniProps = gppIni["Properties"];
            string gppIniAction = JUtil.GetActionString(gppIniProps["@action"].ToString(), debugWrite);
            JToken gppIniPath = investigatePath(JUtil.GetSafeString(gppIniProps, "@path"));
            JToken gppIniSection = investigateString(JUtil.GetSafeString(gppIniProps, "@section"));
            JToken gppIniValue = investigateString(JUtil.GetSafeString(gppIniProps, "@value"));
            JToken gppIniProperty = investigateString(JUtil.GetSafeString(gppIniProps, "@property"));

            // check each of our potentially interesting values to see if it raises our overall interest level
            JToken[] valuesWithInterest = { gppIniPath, gppIniSection, gppIniValue, gppIniProperty };
            foreach (JToken val in valuesWithInterest)
            {
                if ((val != null) && (val["InterestLevel"] != null))
                {
                    int valInterestLevel = int.Parse(val["InterestLevel"].ToString());
                    if (valInterestLevel > interestLevel)
                    {
                        interestLevel = valInterestLevel;
                    }
                }
            }

            if (interestLevel >= intLevelToShow)
            {
                JObject assessedGppIni = new JObject
                {
                    {"Name", gppIniName},
                    {"Changed", gppIniChanged},
                    {"Path", gppIniPath},
                    {"Action", gppIniAction},
                    {"Status", gppIniStatus},
                    {"Section", gppIniSection},
                    {"Value", gppIniValue},
                    {"Property", gppIniProperty}
                };

                return new JProperty(gppIniUid, assessedGppIni);
            }

            return null;
        }
    }
}