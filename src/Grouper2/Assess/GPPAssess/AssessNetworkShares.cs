﻿using Grouper2.Utility;
using Newtonsoft.Json.Linq;

namespace Grouper2.GPPAssess
{
    public partial class AssessGpp
    {
        private JObject GetAssessedNetworkShareSettings(JObject gppCategory)
        {
            JToken gppNetSharesJToken = gppCategory["NetShare"];

            JObject assessedGppNetShares = new JObject();
            if (gppNetSharesJToken is JArray)
            {
                foreach (JToken netShare in gppNetSharesJToken)
                {
                    JProperty assessedGppNetShare = GetAssessedNetworkShare(netShare);
                    if (assessedGppNetShare != null)
                    {
                        assessedGppNetShares.Add(assessedGppNetShare);
                    }
                }
            }
            else
            {
                JProperty assessedGppNetShare = GetAssessedNetworkShare(gppNetSharesJToken);
                if (assessedGppNetShare != null)
                {
                    assessedGppNetShares.Add(assessedGppNetShare);
                }
            }

            if (assessedGppNetShares.HasValues)
            {
                return assessedGppNetShares;
            }

            return null;
        }

        private JProperty GetAssessedNetworkShare(JToken netShare)
        {
            int interestLevel = 2;

            JObject assessedGppNetShare = new JObject
            {
                {"Name", JUtil.GetSafeString(netShare, "@name")},
                {"Changed", JUtil.GetSafeString(netShare, "@changed")},
                {"Action", JUtil.GetActionString(netShare["Properties"]["@action"].ToString())}
            };

            assessedGppNetShare.Add("Path", JUtil.GetSafeString(netShare["Properties"], "@path"));
            assessedGppNetShare.Add("Comment", JUtil.GetSafeString(netShare["Properties"], "@comment"));
            // removed InvestigatePath because it's a network share, it's literally always going to be local and therefore not super interesting.
            if (interestLevel >= GlobalVar.IntLevelToShow)
            {
                return new JProperty(netShare["@uid"].ToString(), assessedGppNetShare);
            }

            return null;
        }
    }
}