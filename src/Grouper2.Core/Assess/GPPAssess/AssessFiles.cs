﻿using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;
using System;

namespace Grouper2.Core.GPPAssess
{
    public partial class AssessGpp
    {
        // ReSharper disable once UnusedMember.Local
        private JObject GetAssessedFiles(JObject gppCategory, int intLevelToShow)
        {
            JObject assessedFiles = new JObject();

            if (gppCategory["File"] is JArray)
            {
                foreach (JObject gppFile in gppCategory["File"])
                {
                    JObject assessedFile = GetAssessedFile(gppFile, intLevelToShow);
                    if (assessedFile != null)
                    {
                        assessedFiles.Add(gppFile["@uid"].ToString(), assessedFile);
                    }
                }
            }
            else
            {
                JObject gppFile = (JObject)JToken.FromObject(gppCategory["File"]);
                JObject assessedFile = GetAssessedFile(gppFile, intLevelToShow);
                if (assessedFile != null)
                {
                    assessedFiles.Add(gppFile["@uid"].ToString(), assessedFile);
                }
            }

            return assessedFiles;
        }

        private JObject GetAssessedFile(JObject gppFile, int intLevelToShow)
        {
            int interestLevel = 3;
            JObject assessedFile = new JObject();
            JToken gppFileProps = gppFile["Properties"];
            assessedFile.Add("Name", gppFile["@name"].ToString());
            assessedFile.Add("Status", gppFile["@status"].ToString());
            assessedFile.Add("Changed", gppFile["@changed"].ToString());
            string gppFileAction = JUtil.GetActionString(gppFileProps["@action"].ToString(), debugWrite);
            assessedFile.Add("Action", gppFileAction);
            JToken targetPathJToken = gppFileProps["@targetPath"];
            if (targetPathJToken != null)
            {
                assessedFile.Add("Target Path", gppFileProps["@targetPath"].ToString());
            }

            JToken fromPathJToken = gppFileProps["@fromPath"];
            if (fromPathJToken != null)
            {
                string fromPath = gppFileProps["@fromPath"].ToString();

                if (fromPath.Length > 0)
                {
                    JObject assessedPath = investigatePath(gppFileProps["@fromPath"].ToString());
                    if (assessedPath != null)
                    {
                        assessedFile.Add("From Path", assessedPath);
                        if (assessedPath["InterestLevel"] != null)
                        {
                            int pathInterest = (int)assessedPath["InterestLevel"];
                            interestLevel = interestLevel + pathInterest;
                        }
                    }
                }
                else
                {
                    assessedFile.Add("From Path", fromPath);
                }
            }

            // if it's too boring to be worth showing, return an empty jobj.
            if (interestLevel <= intLevelToShow)
            {
                return null;
            }

            return assessedFile;
        }
    }
}