using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;

namespace Grouper2.Core.GPPAssess
{
    public partial class AssessGpp
    {
        private JObject GetAssessedNetworkShareSettings(JObject gppCategory, int intLevelToShow)
        {
            JToken gppNetSharesJToken = gppCategory["NetShare"];

            JObject assessedGppNetShares = new JObject();
            if (gppNetSharesJToken is JArray)
            {
                foreach (JToken netShare in gppNetSharesJToken)
                {
                    JProperty assessedGppNetShare = GetAssessedNetworkShare(netShare, intLevelToShow);
                    if (assessedGppNetShare != null)
                    {
                        assessedGppNetShares.Add(assessedGppNetShare);
                    }
                }
            }
            else
            {
                JProperty assessedGppNetShare = GetAssessedNetworkShare(gppNetSharesJToken, intLevelToShow);
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

        private JProperty GetAssessedNetworkShare(JToken netShare, int intLevelToShow)
        {
            int interestLevel = 2;

            JObject assessedGppNetShare = new JObject
            {
                {"Name", JUtil.GetSafeString(netShare, "@name")},
                {"Changed", JUtil.GetSafeString(netShare, "@changed")},
                {"Action", JUtil.GetActionString(netShare["Properties"]["@action"].ToString(), debugWrite)}
            };

            assessedGppNetShare.Add("Path", JUtil.GetSafeString(netShare["Properties"], "@path"));
            assessedGppNetShare.Add("Comment", JUtil.GetSafeString(netShare["Properties"], "@comment"));
            // removed InvestigatePath because it's a network share, it's literally always going to be local and therefore not super interesting.
            if (interestLevel >= intLevelToShow)
            {
                return new JProperty(netShare["@uid"].ToString(), assessedGppNetShare);
            }

            return null;
        }
    }
}