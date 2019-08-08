﻿using Grouper2.Utility;
using Newtonsoft.Json.Linq;

namespace Grouper2.GPPAssess
{
    public partial class AssessGpp
    {
        // ReSharper disable once UnusedMember.Local
        private JObject GetAssessedIniFiles(JObject gppCategory)
        {

            JObject assessedGppInis = new JObject();

            if (gppCategory["Ini"] is JArray)
            {
                foreach (JToken gppIni in gppCategory["Ini"])
                {
                    JProperty assessedGppIni = AssessGppIni(gppIni);
                    assessedGppInis.Add(assessedGppIni);
                }
            }
            else
            {
                JProperty assessedGppIni = AssessGppIni(gppCategory["Ini"]);
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

        static JProperty AssessGppIni(JToken gppIni)
        {
            int interestLevel = 1;
            string gppIniUid = JUtil.GetSafeString(gppIni, "@uid");
            string gppIniName = JUtil.GetSafeString(gppIni, "@name");
            string gppIniChanged = JUtil.GetSafeString(gppIni, "@changed");
            string gppIniStatus = JUtil.GetSafeString(gppIni, "@status");

            JToken gppIniProps = gppIni["Properties"];
            string gppIniAction = JUtil.GetActionString(gppIniProps["@action"].ToString());
            JToken gppIniPath = FileSystem.InvestigatePath(JUtil.GetSafeString(gppIniProps, "@path"));
            JToken gppIniSection = FileSystem.InvestigateString(JUtil.GetSafeString(gppIniProps, "@section"));
            JToken gppIniValue = FileSystem.InvestigateString(JUtil.GetSafeString(gppIniProps, "@value"));
            JToken gppIniProperty = FileSystem.InvestigateString(JUtil.GetSafeString(gppIniProps, "@property"));

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

            if (interestLevel >= GlobalVar.IntLevelToShow)
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