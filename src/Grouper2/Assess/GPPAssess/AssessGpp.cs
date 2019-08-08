﻿using Newtonsoft.Json.Linq;
using System;
using System.Reflection;

namespace Grouper2.GPPAssess
{
    public partial class AssessGpp
    {
        private readonly JObject _gpp;

        public AssessGpp(JObject gpp)
        {
            _gpp = gpp;
        }

        public JObject GetAssessed(string assessName)
        {
            //construct the method name based on the assessName and get it using reflection
            MethodInfo mi = GetType().GetMethod("GetAssessed" + assessName, BindingFlags.NonPublic | BindingFlags.Instance);
            //invoke the found method
            try
            {
                JObject gppToAssess = (JObject)_gpp[assessName];
                if (mi != null)
                {
                    JObject assessedThing = (JObject)mi.Invoke(this, parameters: new object[] { gppToAssess });
                    if (assessedThing != null)
                    {
                        return assessedThing;
                    }
                    else
                    {
                        Utility.Output.DebugWrite("GetAssessed" + assessName + " didn't return anything. This isn't a problem in itself, it just means that nothing met the interest level criteria. This message is only for debugging.");

                        return null;
                    }
                }
                else
                {
                    Utility.Output.DebugWrite("Failed to find method: GetAssessed" + assessName + ". This probably just means I never wrote one. If you think that Group Policy Preferences " + assessName + " are likely to have useful stuff in there, let me know on GitHub?");

                    return null;
                }
            }
            catch (Exception e)
            {
                Utility.Output.DebugWrite(e.ToString());
                return null;
            }
        }
    }
}
