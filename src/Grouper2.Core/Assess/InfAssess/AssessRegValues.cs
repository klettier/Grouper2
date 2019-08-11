﻿using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace Grouper2.Core.InfAssess
{
    public static partial class AssessInf
    {
        // these are the reg keys that the admin doesn't usually set manually
        // e.g. set using the 'security policy' gui
        public static JObject AssessRegValues(JToken regValues, JArray intRegKeysData, Action<string> debugWrite, int intLevelToShow)
        {
            //JObject jankyDb = JankyDb.Instance;
            // get our data about what regkeys are interesting
            //JArray intRegKeysData = (JArray)jankyDb["regKeys"];
            // set up a jobj for our results to go into
            JObject assessedRegValues = new JObject();

            foreach (JProperty regValue in regValues.Children<JProperty>())
            {
                // iterate over the list of interesting keys in our json "db".
                foreach (JToken intRegKey in intRegKeysData)
                {
                    // if it matches an interesting key
                    if (regValue.Name.ToLower().Contains(intRegKey["regKey"].ToString().ToLower()))
                    {
                        // get the name
                        string interestLevelString = intRegKey["intLevel"].ToString();
                        // if it matches at all it's a 1.
                        int interestLevel = 1;
                        // if we can get the interest level from it, do so, otherwise throw an error that we need to fix something.
                        if (!int.TryParse(interestLevelString, out interestLevel))
                        {
                            debugWrite(intRegKey["regKey"].ToString() + " in jankydb doesn't have an interest level assigned.");
                        }

                        string matchedRegKey = regValue.Name;
                        string keyTypeNum = regValue.Value[0].ToString();
                        string keyTypeString = "";
                        switch (keyTypeNum)
                        {
                            case "4":
                                keyTypeString = "REG_DWORD";
                                break;
                            case "7":
                                keyTypeString = "REG_MULTI_SZ";
                                break;
                        }

                        if (interestLevel >= intLevelToShow)
                        {
                            if (keyTypeString == "REG_DWORD")
                            {
                                // if it's a dword it'll only have one value
                                assessedRegValues.Add(matchedRegKey, regValue.Value[1].ToString());
                            }
                            else if (keyTypeString == "REG_MULTI_SZ")
                            {
                                // if it's a multi we'll need to process the rest of the values
                                JArray regValuesJArray = new JArray();
                                foreach (JToken value in regValue.Value.Skip(1))
                                {
                                    regValuesJArray.Add(value.ToString());
                                }
                                assessedRegValues.Add(matchedRegKey, regValuesJArray);
                            }
                        }
                    }
                }
            }

            if (assessedRegValues.HasValues)
            {
                return assessedRegValues;
            }

            return null;

        }
    }
}