﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Grouper2.Utility
{
    class FileSystem
    {
        public static List<string> FindFilePathsInString(string inString)
        {
            List<string> foundFilePaths = new List<string>();

            string[] stringBits = inString.Split(' ');

            foreach (string stringBit in stringBits)
            {
                string cleanedBit = stringBit.Trim('\'', '\"');
                if (IsValidPath(cleanedBit))
                {
                    foundFilePaths.Add(cleanedBit);
                }
            }

            return foundFilePaths;
        }


        public static JObject InvestigateString(string inString)
        // general purpose method for returning some information about why a string might be interesting.
        {
            int interestLevel = 0;
            JObject investigationResults = new JObject { { "Value", inString } };

            // make a list to put any interesting words we find in it
            JArray interestingWordsFound = new JArray();
            // refer to our master list of interesting words
            JArray interestingWords = (JArray)JankyDb.Instance["interestingWords"];
            foreach (string interestingWord in interestingWords)
            {
                if (inString.ToLower().Contains(interestingWord))
                {
                    interestingWordsFound.Add(interestingWord);
                    interestLevel = 4;
                }
            }

            List<string> foundFilePaths = FindFilePathsInString(inString);

            JArray investigatedPaths = new JArray();

            foreach (string foundFilePath in foundFilePaths)
            {
                JObject investigatedPath = FileSystem.InvestigatePath(foundFilePath);

                if (investigatedPath != null)
                {
                    if (investigatedPath["InterestLevel"] != null && Int32.Parse(investigatedPath["InterestLevel"].ToString()) >= GlobalVar.IntLevelToShow)
                    {
                        investigatedPaths.Add(investigatedPath);
                    }
                }
            }

            if (investigatedPaths.Count > 0)
            {
                investigationResults.Add("Paths", investigatedPaths);
            }

            if (interestingWordsFound.Count > 0)
            {
                investigationResults.Add("Interesting Words", interestingWordsFound);
            }

            investigationResults.Add("InterestLevel", interestLevel);
            return investigationResults;

        }


        public static bool IsValidPath(string path, bool allowRelativePaths = false)
        {
            // lifted from Dao Seeker on stackoverflow.com https://stackoverflow.com/questions/6198392/check-whether-a-path-is-valid
            bool isValid;

            try
            {
                string fullPath = Path.GetFullPath(path);

                if (allowRelativePaths)
                {
                    isValid = Path.IsPathRooted(path);
                }
                else
                {
                    string root = Path.GetPathRoot(path);
                    isValid = String.IsNullOrEmpty(root.Trim('\\', '/')) == false;
                }
            }
            catch (Exception)
            {
                isValid = false;
            }

            return isValid;
        }

        public static JObject GetFileDaclJObject(string filePathString)
        {
            int inc = 0;

            string[] interestingTrustees = new string[] { "Everyone", "BUILTIN\\Users", "Authenticated Users", "Domain Users", "INTERACTIVE", };
            string[] boringTrustees = new string[] { "TrustedInstaller", "Administrators", "NT AUTHORITY\\SYSTEM", "Domain Admins", "Enterprise Admins", "Domain Controllers" };
            string[] interestingRights = new string[] { "FullControl", "Modify", "Write", "AppendData", "TakeOwnership" };
            string[] boringRights = new string[] { "Synchronize", "ReadAndExecute" };

            if (!GlobalVar.OnlineChecks)
            {
                return null;
            }
            // object for result
            JObject fileDaclsJObject = new JObject();

            FileSecurity filePathSecObj;
            try
            {
                filePathSecObj = File.GetAccessControl(filePathString);
            }
            catch (ArgumentException e)
            {
                Utility.Output.DebugWrite("Tried to check file permissions on invalid path: " + filePathString);
                Utility.Output.DebugWrite(e.ToString());
                return null;
            }
            catch (UnauthorizedAccessException e)
            {
                Utility.Output.DebugWrite(e.ToString());

                return null;
            }

            AuthorizationRuleCollection fileAccessRules =
                filePathSecObj.GetAccessRules(true, true, typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule fileAccessRule in fileAccessRules)
            {
                // get inheritance and access control type values
                string isInheritedString = "False";
                if (fileAccessRule.IsInherited) isInheritedString = "True";
                string accessControlTypeString = "Allow";
                if (fileAccessRule.AccessControlType == AccessControlType.Deny) accessControlTypeString = "Deny";

                // get the user's SID
                string sid = fileAccessRule.IdentityReference.ToString();
                string displayNameString = LDAPstuff.GetUserFromSid(sid);
                // do some interest level analysis
                bool trusteeBoring = false;
                bool trusteeInteresting = false;
                // check if our trustee is boring
                foreach (string boringTrustee in boringTrustees)
                {
                    // if we're showing everything that's fine, keep going
                    if (GlobalVar.IntLevelToShow == 0)
                    {
                        break;
                    }
                    // otherwise if the trustee is boring, set the interest level to 0
                    if (displayNameString.ToLower().EndsWith(boringTrustee.ToLower()))
                    {
                        trusteeBoring = true;
                        // and don't bother comparing rest of array
                        break;
                    }
                }
                // skip rest of access rule if trustee is boring and we're not showing int level 0
                if ((GlobalVar.IntLevelToShow != 0) && trusteeBoring)
                {
                    continue;
                }
                // see if the trustee is interesting
                foreach (string interestingTrustee in interestingTrustees)
                {
                    if (displayNameString.ToLower().EndsWith(interestingTrustee.ToLower()))
                    {
                        trusteeInteresting = true;
                        break;
                    }
                }
                // get the rights
                string fileSystemRightsString = fileAccessRule.FileSystemRights.ToString();
                // strip spaces
                fileSystemRightsString = fileSystemRightsString.Replace(" ", "");
                // turn them into an array
                string[] fileSystemRightsArray = fileSystemRightsString.Split(',');
                // then do some 'interest level' analysis
                // JArray for output
                JArray fileSystemRightsJArray = new JArray();
                foreach (string right in fileSystemRightsArray)
                {
                    bool rightInteresting = false;
                    bool rightBoring = false;

                    foreach (string boringRight in boringRights)
                    {
                        if (right.ToLower() == boringRight.ToLower())
                        {
                            rightBoring = true;
                            break;
                        }
                    }

                    foreach (string interestingRight in interestingRights)
                    {
                        if (right.ToLower() == interestingRight.ToLower())
                        {
                            rightInteresting = true;
                            break;
                        }
                    }

                    // if we're showing defaults, just add it to the result and move on
                    if (GlobalVar.IntLevelToShow == 0)
                    {
                        fileSystemRightsJArray.Add(right);
                        continue;
                    }
                    // if we aren't, and it's boring, skip it and move on.
                    if (rightBoring)
                    {
                        continue;
                    }
                    // if it's interesting, add it and move on.
                    if (rightInteresting)
                    {
                        fileSystemRightsJArray.Add(right);
                        continue;
                    }
                    // if it's neither boring nor interesting, add it if the 'interestlevel to show' value is low enough
                    else if (GlobalVar.IntLevelToShow < 3)
                    {
                        Utility.Output.DebugWrite(right + " was not labelled as boring or interesting.");
                        fileSystemRightsJArray.Add(right);
                    }
                    else
                    {
                        Utility.Output.DebugWrite("Shouldn't hit here, label FS right as boring or interesting." + right);
                    }
                }

                // no point continuing if no rights to show
                if (fileSystemRightsJArray.HasValues)
                {
                    // if the trustee isn't interesting and we're excluding low-level findings, bail out
                    if ((!trusteeInteresting) && (GlobalVar.IntLevelToShow > 4))
                    {
                        return null;
                    }
                    // build the object
                    string rightsString = fileSystemRightsJArray.ToString().Trim('[', ']').Trim().Replace("\"", "");

                    JObject fileDaclJObject = new JObject
                    {
                        {accessControlTypeString, displayNameString},
                        {"Inherited?", isInheritedString},
                        {"Rights", rightsString}
                    };
                    // add the object to the array.
                    fileDaclsJObject.Add(inc.ToString(), fileDaclJObject);

                    inc++;
                }
            }
            //DebugWrite(fileDaclsJObject.ToString());
            return fileDaclsJObject;
        }

        public static JObject InvestigatePath(string pathToInvestigate)
        {
            // general purpose method for returning some information about why a path might be interesting.

            // set up all our bools and empty JObjects so everything is boring until proven interesting.
            JArray interestingFileExts = (JArray)JankyDb.Instance["interestingExtensions"];
            bool fileExists = false;
            bool fileWritable = false;
            bool fileReadable = false;
            bool dirExists = false;
            bool dirWritable = false;
            bool fileContentsInteresting = false;
            bool isFilePath = false;
            bool isDirPath = false;
            bool parentDirExists = false;
            bool parentDirWritable = false;
            bool extIsInteresting = false;
            string fileExt = "";
            string extantParentDir = "";
            string writableParentDir = "";
            JObject parentDirDacls = new JObject();
            JObject fileDacls = new JObject();
            JObject dirDacls = new JObject();
            JArray interestingWordsFromFile = new JArray();
            string dirPath = "";
            // remove quotes
            string inPath = pathToInvestigate.Trim('\'', '\"', ',', ';');
            // and whitespace
            inPath = inPath.Trim();

            if (inPath.Length > 1)
            {
                try
                {
                    dirPath = Path.GetDirectoryName(inPath);
                    fileExt = Path.GetExtension(inPath);
                }
                catch (ArgumentException)
                {
                    // can happen if "inPath" contains invalid characters (ex. '"') or does not look like a path (ex. "mailto:...")
                    return new JObject(new JProperty("Not a path?", inPath));
                }
            }
            else
            {
                return new JObject(new JProperty("Not a path?", inPath));
            }

            if (inPath.Contains("http://") || inPath.Contains("https://"))
            {
                return new JObject(new JProperty("HTTP/S URL?", inPath));
            }

            if (inPath.Contains("://") && !(inPath.Contains("http://")))
            {
                return new JObject(new JProperty("URI?", inPath));
            }

            if (inPath.Contains('%'))
            {
                return new JObject(new JProperty("Env var found in path", inPath));
            }

            if (inPath.StartsWith("C:") || inPath.StartsWith("D:"))
            {
                return new JObject(new JProperty("Local Drive?", inPath));
            }

            // if it doesn't seem to have any path separators it's probably a single file on sysvol.
            if (!inPath.Contains('\\') && !inPath.Contains('/'))
            {
                return new JObject(new JProperty("No path separators, file in SYSVOL?", inPath));
            }
            // figure out if it's a file path or just a directory even if the file doesn't exist

            string pathFileComponent = Path.GetFileName(inPath);

            if (pathFileComponent == "")
            {
                isDirPath = true;
                isFilePath = false;
            }
            else
            {
                isDirPath = false;
                isFilePath = true;
            }

            if (isFilePath)
            {
                // check if the file exists
                fileExists = DoesFileExist(inPath);

                if (fileExists)
                {
                    // if it does, the parent Dir must exist.
                    dirExists = true;
                    // check if we can read it
                    fileReadable = CanIRead(inPath);
                    // check if we can write it
                    fileWritable = CanIWrite(inPath);
                    // see what the file extension is and if it's interesting
                    fileExt = Path.GetExtension(inPath);
                    foreach (string intExt in interestingFileExts)
                    {
                        if ((fileExt.ToLower().Trim('.')) == (intExt.ToLower()))
                        {
                            extIsInteresting = true;
                        }
                    }

                    // if we can read it, have a look if it has interesting strings in it.
                    if (fileReadable)
                    {
                        // make sure the file isn't massive so we don't waste ages grepping whole disk images over the network
                        long fileSize = new FileInfo(inPath).Length;

                        if (fileSize < 1048576) // 1MB for now. Can tune if too slow.
                        {
                            interestingWordsFromFile = GetInterestingWordsFromFile(inPath);
                            if (interestingWordsFromFile.Count > 0)
                            {
                                fileContentsInteresting = true;
                            }
                        }
                    }

                    // get the file permissions
                    fileDacls = GetFileDaclJObject(inPath);
                }

            }

            if (isDirPath)
            {
                dirExists = DoesDirExist(inPath);
            }
            else if (!isDirPath && !fileExists)
            {
                dirExists = DoesDirExist(dirPath);
            }

            if (dirExists)
            {
                dirDacls = GetFileDaclJObject(dirPath);
                dirWritable = CanIWrite(dirPath);
            }
            // if the dir doesn't exist, iterate up the file path checking if any exist and if we can write to any of them.
            if (!dirExists)
            {
                // we want to allow a path like C: but not one like "\"
                if ((dirPath != null) && (dirPath.Length > 1))
                {
                    // get the root of the path
                    try
                    {
                        // ReSharper disable once UnusedVariable
                        string pathRoot = Path.GetPathRoot(dirPath);
                    }
                    catch (ArgumentException e)
                    {
                        Utility.Output.DebugWrite(e.ToString());

                        return new JObject(new JProperty("Not a path?", inPath));
                    }

                    // get the first parent dir
                    string dirPathParent = "";

                    try
                    {
                        if (GetParentDirPath(dirPath) != null)
                        {
                            dirPathParent = GetParentDirPath(dirPath);
                        }
                    }
                    catch (ArgumentException e)
                    {
                        Utility.Output.DebugWrite(e.ToString());

                        return new JObject(new JProperty("Not a path?", inPath));
                    }

                    // iterate until the path root 
                    while ((dirPathParent != null) && (dirPathParent != "\\\\") && (dirPathParent != "\\"))
                    {
                        // check if the parent dir exists
                        parentDirExists = DoesDirExist(dirPathParent);
                        // if it does
                        if (parentDirExists)
                        {
                            // get the dir dacls
                            parentDirDacls = GetFileDaclJObject(dirPathParent);
                            // check if it's writable
                            parentDirWritable = CanIWrite(dirPathParent);
                            if (parentDirWritable)
                            {
                                writableParentDir = dirPathParent;
                            }

                            break;
                        }

                        //prepare for next iteration by aiming at the parent dir
                        if (GetParentDirPath(dirPathParent) != null)
                        {
                            dirPathParent = GetParentDirPath(dirPathParent);
                        }
                        else break;
                    }
                }
            }

            // put all the values we just collected into a jobject for reporting and calculate how interesting it is.
            JObject filePathAssessment = new JObject();
            int interestLevel = 1;
            filePathAssessment.Add("Path assessed", inPath);
            if (isFilePath)
            {
                if (fileExists)
                {
                    filePathAssessment.Add("File exists", true);
                    if (extIsInteresting)
                    {
                        interestLevel = interestLevel + 2;
                        filePathAssessment.Add("File extension interesting", extIsInteresting);
                    }
                    filePathAssessment.Add("File readable", fileReadable);
                    if (fileContentsInteresting)
                    {
                        filePathAssessment.Add("File contents interesting", "True");
                        filePathAssessment.Add("Interesting strings found", interestingWordsFromFile);
                        interestLevel = interestLevel + 2;
                    }
                    filePathAssessment.Add("File writable", fileWritable);
                    if (fileWritable) interestLevel = interestLevel + 10;
                    if ((fileDacls != null) && fileDacls.HasValues)
                    {
                        filePathAssessment.Add("File DACLs", fileDacls);
                    }
                }
                else
                {
                    filePathAssessment.Add("File exists", false);
                    filePathAssessment.Add("Directory exists", dirExists);
                    if (dirExists)
                    {
                        filePathAssessment.Add("Directory writable", dirWritable);
                        if (!(inPath.StartsWith("C:") || inPath.StartsWith("D:")))
                        {
                            if (dirWritable) interestLevel = interestLevel + 10;
                        }

                        if ((dirDacls != null) && dirDacls.HasValues)
                        {
                            filePathAssessment.Add("Directory DACL", dirDacls);
                        }
                    }
                    else if (parentDirExists)
                    {
                        filePathAssessment.Add("Parent dir exists", true);
                        if (parentDirWritable)
                        {
                            filePathAssessment.Add("Parent dir writable", "True");
                            if (!(inPath.StartsWith("C:") || inPath.StartsWith("D:")))
                            {
                                interestLevel = interestLevel + 10;
                            }
                            filePathAssessment.Add("Writable parent dir", writableParentDir);
                        }
                        else
                        {
                            filePathAssessment.Add("Extant parent dir", extantParentDir);
                            filePathAssessment.Add("Parent dir DACLs", parentDirDacls);
                        }
                    }
                }
            }
            else if (isDirPath)
            {
                filePathAssessment.Add("Directory exists", dirExists);
                if (dirExists)
                {
                    filePathAssessment.Add("Directory is writable", dirWritable);
                    // quick n dirty way of excluding local drives while keeping mapped network drives.
                    if (!(inPath.StartsWith("C:") || inPath.StartsWith("D:")))
                    {
                        if (dirWritable) interestLevel = interestLevel + 10;
                    }
                    filePathAssessment.Add("Directory DACLs", dirDacls);
                }
                else if (parentDirExists)
                {
                    filePathAssessment.Add("Parent dir exists", true);
                    if (parentDirWritable)
                    {
                        filePathAssessment.Add("Parent dir writable", "True");
                        if (!(inPath.StartsWith("C:") || inPath.StartsWith("D:")))
                        {
                            interestLevel = interestLevel + 10;
                        }
                        filePathAssessment.Add("Writable parent dir", writableParentDir);
                    }
                    else
                    {
                        filePathAssessment.Add("Extant parent dir", extantParentDir);
                        filePathAssessment.Add("Parent dir DACLs", parentDirDacls);
                    }
                }
            }
            filePathAssessment.Add("InterestLevel", interestLevel.ToString());
            return filePathAssessment;
        }


        public static JArray GetInterestingWordsFromFile(string inPath)
        {
            // validate if the file exists
            bool fileExists = FileSystem.DoesFileExist(inPath);
            if (!fileExists)
            {
                return null;
            }

            // get our list of interesting words
            JArray interestingWords = (JArray)JankyDb.Instance["interestingWords"];

            // get contents of the file and smash case
            string fileContents = "";
            try
            {
                fileContents = File.ReadAllText(inPath).ToLower();
            }
            catch (UnauthorizedAccessException e)
            {
                Utility.Output.DebugWrite(e.ToString());
            }

            // set up output object
            JArray interestingWordsFound = new JArray();

            foreach (string word in interestingWords)
            {
                if (fileContents.Contains(word))
                {
                    interestingWordsFound.Add(word);
                }
            }

            return interestingWordsFound;
        }

        public static string GetParentDirPath(string dirPath)
        {
            int count = dirPath.Length - dirPath.Replace("\\", "").Length;

            if (count < 1)
            {
                return null;
            }

            int lastDirSepIndex = Util.IndexOfNth(dirPath, "\\", count);

            string parentPath = dirPath.Remove(lastDirSepIndex);

            return parentPath;
        }

        public static bool DoesFileExist(string inPath)
        {
            if (!GlobalVar.OnlineChecks)
            {
                return false;
            }
            bool fileExists = false;
            try
            {
                fileExists = File.Exists(inPath);
            }
            catch (ArgumentException)
            {
                Utility.Output.DebugWrite("Checked if file " + inPath +
                                       " exists but it doesn't seem to be a valid file path.");
            }
            catch (UnauthorizedAccessException)
            {
                Utility.Output.DebugWrite("Tried to check if file " + inPath +
                                       " exists but I'm not allowed.");
            }
            return fileExists;
        }

        public static bool DoesDirExist(string inPath)
        {
            if (!GlobalVar.OnlineChecks)
            {
                return false;
            }
            bool dirExists = false;
            try
            {
                dirExists = Directory.Exists(inPath);
            }
            catch (ArgumentException)
            {
                Utility.Output.DebugWrite("Checked if directory " + inPath + " exists but it doesn't seem to be a valid file path.");
            }
            return dirExists;
        }

        public static bool CanIRead(string inPath)
        {
            bool canRead = false;
            if (!GlobalVar.OnlineChecks)
            {
                return false;
            }
            try
            {
                FileStream stream = File.OpenRead(inPath);
                canRead = stream.CanRead;
                stream.Close();
            }
            catch (UnauthorizedAccessException)
            {
                Utility.Output.DebugWrite("Tested read perms for " + inPath + " and couldn't read.");
            }
            catch (ArgumentException)
            {
                Utility.Output.DebugWrite("Tested read perms for " + inPath + " but it doesn't seem to be a valid file path.");
            }
            catch (Exception e)
            {
                Utility.Output.DebugWrite(e.ToString());
            }
            return canRead;
        }

        public static bool CanIWrite(string inPath)
        {
            // this will return true if write or modify or take ownership or any of those other good perms are available.

            CurrentUserSecurity currentUserSecurity = new CurrentUserSecurity();

            FileSystemRights[] fsRights = {
                FileSystemRights.Write,
                FileSystemRights.Modify,
                FileSystemRights.FullControl,
                FileSystemRights.TakeOwnership,
                FileSystemRights.ChangePermissions,
                FileSystemRights.AppendData,
                FileSystemRights.CreateFiles,
                FileSystemRights.CreateDirectories,
                FileSystemRights.WriteData
            };

            try
            {
                FileAttributes attr = File.GetAttributes(inPath);
                foreach (FileSystemRights fsRight in fsRights)
                {
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(inPath);
                        return currentUserSecurity.HasAccess(dirInfo, fsRight);
                    }

                    FileInfo fileInfo = new FileInfo(inPath);
                    return currentUserSecurity.HasAccess(fileInfo, fsRight);
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentNullException)
            {
                return false;
            }
            return false;
        }

        public static JObject InvestigateFileContents(string inString)
        {
            string fileString;
            JObject investigatedFileContents = new JObject();
            try
            {
                fileString = File.ReadAllText(inString).ToLower();

                // feed the whole thing through FileSystem.InvestigateString
                investigatedFileContents = FileSystem.InvestigateString(fileString);
            }
            catch (UnauthorizedAccessException e)
            {
                Utility.Output.DebugWrite(e.ToString());
            }

            if (investigatedFileContents["InterestLevel"] != null)
            {
                if (((int)investigatedFileContents["InterestLevel"]) >= GlobalVar.IntLevelToShow)
                {
                    investigatedFileContents.Remove("Value");
                    investigatedFileContents.AddFirst(new JProperty("File Path", inString));
                    return investigatedFileContents;
                }
            }

            return null;
        }
    }
}
