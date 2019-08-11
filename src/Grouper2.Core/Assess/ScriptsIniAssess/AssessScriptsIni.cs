﻿using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Grouper2.Core.ScriptsIniAssess
{
    class AssessScriptsIni
    {
        public static JObject GetAssessedScriptsIni(
            JObject parsedScriptsIni,
            int intLevelToShow,
            Func<string,JObject> investigatePath,
            Func<string,JObject> investigateString)
        {
            JObject assessedScriptsIni = new JObject();

            foreach (KeyValuePair<string, JToken> parsedScriptIniType in parsedScriptsIni)
            {
                // JObject for everything from the 'type' to go into
                JObject assessedScriptIniType = new JObject();
                // get the type of scripts we're looking at i.e. Startup, Shutdown, etc.
                string scriptType = parsedScriptIniType.Key;
                // cast the JToken to a JObj
                JObject parsedScriptIniTypeJObject = (JObject)parsedScriptIniType.Value;
                // iterate over individual scripts
                foreach (KeyValuePair<string, JToken> parsedScript in parsedScriptIniTypeJObject)
                {
                    int interestLevel = 4;
                    // set up script results object
                    JObject assessedScriptIni = new JObject();
                    // get the unique ID of this script
                    string scriptNum = parsedScript.Key;
                    string parameters = "";
                    string cmdLine = "";
                    if (parsedScript.Value["CmdLine"] != null)
                    {
                        cmdLine = parsedScript.Value["CmdLine"].ToString();
                    }
                    // params are optional, handle it if it's missing.
                    if (parsedScript.Value["Parameters"] != null)
                    {
                        parameters = parsedScript.Value["Parameters"].ToString();
                    }

                    // add cmdLine to result
                    if (cmdLine.Length > 0)
                    {
                        JObject investigatedCommand = investigatePath(cmdLine);
                        if (investigatedCommand != null)
                        {
                            if (investigatedCommand["InterestLevel"] != null)
                            {
                                if ((int)investigatedCommand["InterestLevel"] > interestLevel)
                                {
                                    interestLevel = (int)investigatedCommand["InterestLevel"];
                                }
                            }

                            assessedScriptIni.Add("Command Line", investigatedCommand);
                        }
                    }

                    if (parameters.Length > 0)
                    {
                        JObject investigatedParams = investigateString(parameters);
                        if (investigatedParams != null)
                        {
                            if (investigatedParams["InterestLevel"] != null)
                            {
                                if ((int)investigatedParams["InterestLevel"] > interestLevel)
                                {
                                    interestLevel = (int)investigatedParams["InterestLevel"];
                                }
                            }
                            assessedScriptIni.Add("Parameters", investigateString(parameters));
                        }
                    }

                    if (interestLevel >= intLevelToShow)
                    {
                        assessedScriptIniType.Add(scriptNum, assessedScriptIni);
                    }
                }

                // add all the results from the type to the object being returned
                if (assessedScriptIniType.HasValues)
                {
                    assessedScriptsIni.Add(scriptType, assessedScriptIniType);
                }
            }

            if (assessedScriptsIni.HasValues)
            {
                JObject scriptsIniResults = new JObject()
                {
                    {"Scripts", assessedScriptsIni }
                };
                return scriptsIniResults;
            }
            else
            {
                return null;
            }

        }
    }
}
