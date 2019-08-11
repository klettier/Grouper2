﻿using Grouper2.Core.Utility;
using Newtonsoft.Json.Linq;
using System;

namespace Grouper2.Core.GPPAssess
{
    public partial class AssessGpp
    {
        // ReSharper disable once UnusedMember.Local
        private JObject GetAssessedDataSources(JObject gppCategory, int intLevelToShow, Action<string> debugWrite)
        {
            JObject assessedGppDataSources = new JObject();
            if (gppCategory["DataSource"] is JArray)
            {
                foreach (JToken gppDataSource in gppCategory["DataSource"])
                {
                    JProperty assessedGppDataSource = AssessGppDataSource(gppDataSource, intLevelToShow, debugWrite);
                    if (assessedGppDataSource != null)
                    {
                        assessedGppDataSources.Add(assessedGppDataSource);
                    }
                }
            }
            else
            {
                JProperty assessedGppDataSource = AssessGppDataSource(gppCategory["DataSource"], intLevelToShow, debugWrite);
                assessedGppDataSources.Add(assessedGppDataSource);
            }

            if (assessedGppDataSources.HasValues)
            {
                return assessedGppDataSources;
            }
            else
            {
                return null;
            }

        }

        static JProperty AssessGppDataSource(JToken gppDataSource, int intLevelToShow, Action<string> debugWrite)
        {
            int interestLevel = 1;
            string gppDataSourceUid = JUtil.GetSafeString(gppDataSource, "@uid");
            string gppDataSourceName = JUtil.GetSafeString(gppDataSource, "@name");
            string gppDataSourceChanged = JUtil.GetSafeString(gppDataSource, "@changed");

            JToken gppDataSourceProps = gppDataSource["Properties"];
            string gppDataSourceAction = JUtil.GetActionString(gppDataSourceProps["@action"].ToString(), debugWrite);
            string gppDataSourceUserName = JUtil.GetSafeString(gppDataSourceProps, "@username");
            string gppDataSourcecPassword = JUtil.GetSafeString(gppDataSourceProps, "@cpassword");
            string gppDataSourcePassword = "";
            if (gppDataSourcecPassword.Length > 0)
            {
                gppDataSourcePassword = Util.DecryptCpassword(gppDataSourcecPassword);
                interestLevel = 10;
            }

            string gppDataSourceDsn = JUtil.GetSafeString(gppDataSourceProps, "@dsn");
            string gppDataSourceDriver = JUtil.GetSafeString(gppDataSourceProps, "@driver");
            string gppDataSourceDescription = JUtil.GetSafeString(gppDataSourceProps, "@description");
            JToken gppDataSourceAttributes = gppDataSourceProps["Attributes"];

            if (interestLevel >= intLevelToShow)
            {
                JObject assessedGppDataSource = new JObject
                {
                    {"Name", gppDataSourceName},
                    {"Changed", gppDataSourceChanged},
                    {"Action", gppDataSourceAction},
                    {"Username", gppDataSourceUserName}
                };
                if (gppDataSourcecPassword.Length > 0)
                {
                    assessedGppDataSource.Add("cPassword", gppDataSourcecPassword);
                    assessedGppDataSource.Add("Decrypted Password", gppDataSourcePassword);
                }
                assessedGppDataSource.Add("DSN", gppDataSourceDsn);
                assessedGppDataSource.Add("Driver", gppDataSourceDriver);
                assessedGppDataSource.Add("Description", gppDataSourceDescription);
                if ((gppDataSourceAttributes != null) && (gppDataSourceAttributes.HasValues))
                {
                    assessedGppDataSource.Add("Attributes", gppDataSourceAttributes);
                }

                return new JProperty(gppDataSourceUid, assessedGppDataSource);
            }

            return null;
        }
    }
}