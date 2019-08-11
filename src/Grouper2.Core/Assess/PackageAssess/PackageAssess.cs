using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Grouper2.Core
{
    class PackageAssess
    {
        public static JProperty AssessPackage(KeyValuePair<string, JToken> gpoPackageKvp, Func<string, JObject> investigatePath, int intLevelToShow)
        {
            JToken gpoPackage = gpoPackageKvp.Value;
            int interestLevel = 3;

            if (gpoPackage["MSI Path"] != null)
            {
                string msiPath = gpoPackage["MSI Path"].ToString();
                JObject assessedMsiPath = investigatePath(msiPath);
                if ((assessedMsiPath != null) && (assessedMsiPath.HasValues))
                {
                    gpoPackage["MSI Path"] = assessedMsiPath;
                    if (assessedMsiPath["InterestLevel"] != null)
                    {
                        if ((int)assessedMsiPath["InterestLevel"] > interestLevel)
                        {
                            interestLevel = (int)assessedMsiPath["InterestLevel"];
                        }
                    }
                }

                if (interestLevel >= intLevelToShow)
                {
                    return new JProperty(gpoPackageKvp.Key, gpoPackage);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

        }

    }
}
