using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;
using System;

namespace Grouper2.Core.GPPAssess
{
    public partial class AssessGpp
    {
        // ReSharper disable once UnusedMember.Local
        private JObject GetAssessedDrives(JObject gppCategory, int intLevelToShow, Action<string> debugWrite, Func<string, JObject> investigatePath)
        {
            JObject assessedGppDrives = new JObject();

            if (gppCategory["Drive"] is JArray)
            {
                foreach (JToken gppDrive in gppCategory["Drive"])
                {
                    JProperty assessedGppDrive = AssessGppDrive(gppDrive, intLevelToShow, debugWrite, investigatePath);
                    if (assessedGppDrive != null)
                    {
                        try
                        {
                            assessedGppDrives.Add(assessedGppDrive);
                        }
                        catch (System.ArgumentException)
                        {
                            // in some rare cases we can have duplicated drive UIDs in the same file, just ignore it
                        }
                    }
                }
            }
            else
            {
                JProperty assessedGppDrive = AssessGppDrive(gppCategory["Drive"], intLevelToShow, debugWrite, investigatePath);
                assessedGppDrives.Add(assessedGppDrive);
            }

            if (assessedGppDrives.HasValues)
            {
                return assessedGppDrives;
            }
            else
            {
                return null;
            }
        }

        static JProperty AssessGppDrive(JToken gppDrive, int intLevelToShow, Action<string> debugWrite, Func<string, JObject> investigatePath)
        {
            int interestLevel = 1;
            string gppDriveUid = JUtil.GetSafeString(gppDrive, "@uid");
            string gppDriveName = JUtil.GetSafeString(gppDrive, "@name");
            string gppDriveChanged = JUtil.GetSafeString(gppDrive, "@changed");
            string gppDriveAction = JUtil.GetActionString(gppDrive["Properties"]["@action"].ToString(), debugWrite);
            string gppDriveUserName = JUtil.GetSafeString(gppDrive["Properties"], "@userName");
            string gppDrivecPassword = JUtil.GetSafeString(gppDrive["Properties"], "@cpassword");
            string gppDrivePassword = "";
            if (gppDrivecPassword.Length > 0)
            {
                gppDrivePassword = Util.DecryptCpassword(gppDrivecPassword);
                interestLevel = 10;
            }

            string gppDriveLetter = "";
            if (gppDrive["Properties"]["@useLetter"] != null)
            {
                if (gppDrive["Properties"]["@useLetter"].ToString() == "1")
                {
                    gppDriveLetter = JUtil.GetSafeString(gppDrive["Properties"], "@letter");
                }
                else if (gppDrive["Properties"]["@useLetter"].ToString() == "0")
                {
                    gppDriveLetter = "First letter available, starting at " +
                                     JUtil.GetSafeString(gppDrive["Properties"], "@letter");
                }
            }

            string gppDriveLabel = JUtil.GetSafeString(gppDrive["Properties"], "@label");
            JObject gppDrivePath = investigatePath(gppDrive["Properties"]["@path"].ToString());
            if (gppDrivePath != null)
            {
                if (gppDrivePath["InterestLevel"] != null)
                {
                    int pathInterestLevel = int.Parse(gppDrivePath["InterestLevel"].ToString());
                    if (pathInterestLevel > interestLevel)
                    {
                        interestLevel = pathInterestLevel;
                    }
                }
            }

            if (interestLevel >= intLevelToShow)
            {
                JObject assessedGppDrive = new JObject
                {
                    {"Name", gppDriveName},
                    {"Action", gppDriveAction},
                    {"Changed", gppDriveChanged},
                    {"Drive Letter", gppDriveLetter},
                    {"Label", gppDriveLabel}
                };
                if (gppDrivecPassword.Length > 0)
                {
                    assessedGppDrive.Add("Username", gppDriveUserName);
                    assessedGppDrive.Add("cPassword", gppDrivecPassword);
                    assessedGppDrive.Add("Decrypted Password", gppDrivePassword);
                }

                if (gppDrivePath != null)
                {
                    assessedGppDrive.Add("Path", gppDrivePath);
                }
                return new JProperty(gppDriveUid, assessedGppDrive);
            }

            return null;
        }
    }
}
