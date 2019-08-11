using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;

namespace Grouper2.Core.GPPAssess
{
    public partial class AssessGpp
    {
        private JObject GetAssessedShortcuts(JObject gppCategory, int intLevelToShow)
        {
            JObject assessedShortcuts = new JObject();

            if (gppCategory["Shortcut"] is JArray)
            {
                foreach (JObject gppShortcuts in gppCategory["Shortcut"])
                {
                    JObject assessedShortcut = GetAssessedShortcut(gppShortcuts, intLevelToShow);
                    if ((assessedShortcut != null) && assessedShortcut.HasValues)
                    {
                        assessedShortcuts.Add(gppShortcuts["@uid"].ToString(), assessedShortcut);
                    }
                }
            }
            else
            {
                if (gppCategory["Shortcut"] != null)
                {
                    JObject gppShortcuts = (JObject)JToken.FromObject(gppCategory["Shortcut"]);
                    JObject assessedShortcut = GetAssessedShortcut(gppShortcuts, intLevelToShow);
                    if (assessedShortcut.HasValues)
                    {
                        assessedShortcuts.Add(gppShortcuts["@uid"].ToString(), assessedShortcut);
                    }
                }
            }

            return assessedShortcuts;
        }

        private JObject GetAssessedShortcut(JObject gppShortcut, int intLevelToShow)
        {
            int interestLevel = 3;
            JObject assessedShortcut = new JObject();
            JToken gppShortcutProps = gppShortcut["Properties"];
            assessedShortcut.Add("Name", JUtil.GetSafeString(gppShortcut, "@name"));
            assessedShortcut.Add("Status", JUtil.GetSafeString(gppShortcut, "@status"));
            assessedShortcut.Add("Changed", JUtil.GetSafeString(gppShortcut, "@changed"));
            string gppShortcutAction = JUtil.GetActionString(gppShortcutProps["@action"].ToString(), debugWrite);
            assessedShortcut.Add("Action", gppShortcutAction);
            assessedShortcut.Add("Target Type", JUtil.GetSafeString(gppShortcutProps, "@targetType"));
            string arguments = JUtil.GetSafeString(gppShortcutProps, "@arguments");
            if (arguments != null)
            {
                JToken investigatedArguments = investigateString(arguments);
                assessedShortcut.Add("Arguments", arguments);
                if (investigatedArguments["InterestLevel"] != null)
                {
                    if ((int)investigatedArguments["InterestLevel"] > interestLevel)
                    {
                        interestLevel = (int)investigatedArguments["InterestLevel"];
                    }
                }
            }

            string iconPath = JUtil.GetSafeString(gppShortcutProps, "@iconPath");

            JObject investigatedIconPath = investigatePath(iconPath);
            if ((investigatedIconPath != null) && (investigatedIconPath["InterestLevel"] != null))
            {
                if ((int)investigatedIconPath["InterestLevel"] > interestLevel)
                {
                    interestLevel = (int)investigatedIconPath["InterestLevel"];
                }
            }

            assessedShortcut.Add("Icon Path", investigatedIconPath);
            assessedShortcut.Add("Icon Index", JUtil.GetSafeString(gppShortcutProps, "@iconIndex"));

            string workingDir = JUtil.GetSafeString(gppShortcutProps, "@startIn");
            if (workingDir != null)
            {
                JToken assessedWorkingDir = investigatePath(workingDir);
                if (assessedWorkingDir != null)
                {
                    assessedShortcut.Add("Working Directory", assessedWorkingDir);
                }
            }

            assessedShortcut.Add("Comment", JUtil.GetSafeString(gppShortcutProps, "@comment"));

            string shortcutPath = JUtil.GetSafeString(gppShortcutProps, "@shortcutPath");
            JObject investigatedShortcutPath = investigatePath(shortcutPath);
            if (investigatedShortcutPath["InterestLevel"] != null)
            {
                if ((int)investigatedShortcutPath["InterestLevel"] > interestLevel)
                {
                    interestLevel = (int)investigatedShortcutPath["InterestLevel"];
                }
            }

            string targetPath = JUtil.GetSafeString(gppShortcutProps, "@targetPath");
            JObject investigatedTargetPath = investigatePath(targetPath);

            if ((investigatedTargetPath != null) && (investigatedTargetPath["InterestLevel"] != null))
            {
                if ((int)investigatedTargetPath["InterestLevel"] > interestLevel)
                {
                    interestLevel = (int)investigatedTargetPath["InterestLevel"];
                }
            }

            assessedShortcut.Add("Shortcut Path", investigatedShortcutPath);
            assessedShortcut.Add("Target Path", investigatedTargetPath);

            // if it's too boring to be worth showing, return an empty jobj.
            if (interestLevel < intLevelToShow)
            {
                return null;
            }

            return assessedShortcut;
        }
    }
}