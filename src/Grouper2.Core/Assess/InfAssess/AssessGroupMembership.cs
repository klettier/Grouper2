﻿using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;
using System;

namespace Grouper2.Core.InfAssess
{
    public static partial class AssessInf
    {
        public static JObject AssessGroupMembership(
            JToken parsedGrpMemberships,
            int intLevelToShow,
            JArray wellKnownSids,
            bool onlineChecks,
            Func<string, string> getUserFromSid,
            Action<string> debugWrite)
        {
            // base interest level
            int interestLevel = 4;
            // really not sure about this one at all. Think it's ok now but could use a recheck.
            // output object
            JObject assessedGrpMemberships = new JObject();
            // cast input object
            JEnumerable<JToken> parsedGrpMembershipsEnumerable = parsedGrpMemberships.Children();

            foreach (JToken parsedGrpMembership in parsedGrpMembershipsEnumerable)
            {
                JProperty parsedGrpMembershipJProp = (JProperty)parsedGrpMembership;

                // break immediately if there's no value.
                if (parsedGrpMembershipJProp.Value.ToString() == "")
                {
                    continue;
                }

                // strip the asterisk off the front of the line
                string cleanedKey = parsedGrpMembershipJProp.Name.Trim('*');
                // split out the sid from the 'memberof' vs 'members' bit.
                string[] splitKey = cleanedKey.Split('_');
                // get the sid
                string cleanSid = splitKey[0];
                // get the type of entry
                string memberWhat = splitKey[2];

                // check if the Key SID is a well known sid and get some info about it if it is.
                JToken checkedSid = Sid.CheckSid(cleanSid, wellKnownSids);
                string displayName = "";
                // if we're online, try to look up the Key SID
                if (onlineChecks)
                {
                    displayName = getUserFromSid(cleanSid);
                }
                // otherwise try to get it from the well known sid data
                else if (checkedSid != null)
                {
                    displayName = checkedSid["displayName"].ToString();
                }
                else
                {
                    displayName = cleanSid;
                }

                if (memberWhat == "Memberof")
                {
                    JProperty assessedGrpMemberKey = AssessGrpMemberItem(parsedGrpMembershipJProp.Name.Split('_')[0], onlineChecks, getUserFromSid, () => wellKnownSids);

                    if (parsedGrpMembershipJProp.Value is JArray)
                    {
                        foreach (string rawGroup in parsedGrpMembershipJProp.Value)
                        {
                            JProperty assessedGrpMemberItem = AssessGrpMemberItem(rawGroup, onlineChecks, getUserFromSid, () => wellKnownSids);
                            if (!(assessedGrpMemberships.ContainsKey(assessedGrpMemberItem.Name)))
                            {
                                assessedGrpMemberships.Add(
                                    new JProperty(assessedGrpMemberItem.Name,
                                        new JObject(
                                            new JProperty("SID", assessedGrpMemberItem.Value),
                                            new JProperty("Members", new JObject())))
                                );
                            }

                            JObject targetJObject = (JObject)assessedGrpMemberships[assessedGrpMemberItem.Name]["Members"];
                            targetJObject.Add(assessedGrpMemberKey);
                        }
                    }
                    else
                    {
                        // get a cleaned up version of the memberof
                        JProperty assessedGrpMemberValue = AssessGrpMemberItem(parsedGrpMembershipJProp.Value.ToString(), onlineChecks, getUserFromSid, () => wellKnownSids);


                        if (!(assessedGrpMemberships.ContainsKey(assessedGrpMemberValue.Name)))
                        {
                            // create one
                            assessedGrpMemberships.Add(
                                new JProperty(assessedGrpMemberValue.Name,
                                    new JObject(
                                        new JProperty("SID", assessedGrpMemberValue.Value),
                                        new JProperty("Members", new JObject())))
                            );
                        }

                        JObject targetJObject = (JObject)assessedGrpMemberships[assessedGrpMemberValue.Name]["Members"];
                        targetJObject.Add(assessedGrpMemberKey);
                    }
                }
                else
                {
                    // if we don't have an entry for this group yet
                    //Utility.Output.DebugWrite(displayName);
                    if (!(assessedGrpMemberships.ContainsKey(displayName)))
                    {
                        assessedGrpMemberships.Add(
                            new JProperty(displayName,
                                new JObject(
                                    new JProperty("SID", cleanSid),
                                    new JProperty("Members", new JObject())))
                        );
                    }

                    JObject targetJObject = (JObject)assessedGrpMemberships[displayName]["Members"];
                    // iterate over members and put them in the appropriate JArray
                    if (parsedGrpMembershipJProp.Value is JArray)
                    {
                        foreach (string rawMember in parsedGrpMembershipJProp.Value)
                        {
                            JProperty assessedGrpMember = AssessGrpMemberItem(rawMember, onlineChecks, getUserFromSid, () => wellKnownSids);
                            try
                            {
                                targetJObject.Add(assessedGrpMember);
                            }
                            catch (Exception e)
                            {
                                debugWrite(e.ToString());
                            }
                        }
                    }
                    else
                    {
                        JProperty assessedGrpMember = AssessGrpMemberItem(parsedGrpMembershipJProp.Value.ToString(), onlineChecks, getUserFromSid, () => wellKnownSids);
                        try
                        {
                            //Utility.Output.DebugWrite(assessedGrpMember.ToString());
                            targetJObject.Add(assessedGrpMember);
                        }
                        catch (Exception e)
                        {
                            debugWrite(e.ToString());
                        }
                    }
                }

                // if the resulting interest level of this shit is sufficient, add it to the output JObject.
                if (intLevelToShow <= interestLevel)
                {
                    return assessedGrpMemberships;
                }
            }

            return null;
        }

        public static JProperty AssessGrpMemberItem(
            string rawMember,
            bool onlineChecks,
            Func<string,string> getUserFromSid,
            Func<JArray> getWellKnownSids)
        {
            string memberDisplayName = rawMember;
            string memberSid = "unknown";
            // if it's a SID
            if (rawMember.StartsWith("*"))
            {
                // clean it up
                memberSid = rawMember.Trim('*');
                if (onlineChecks)
                {
                    // look it up
                    memberDisplayName = getUserFromSid(memberSid);
                }
                else
                {
                    // see if it's well known
                    JToken checkedMemberSid = Sid.CheckSid(memberSid, getWellKnownSids());
                    if (checkedMemberSid != null)
                    {
                        memberDisplayName = checkedMemberSid["displayName"].ToString();
                    }
                }
            }
            return new JProperty(memberDisplayName, memberSid);
        }
    }
}