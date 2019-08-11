﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Grouper2.Core.Tests")]

namespace Grouper2.Core
{
    public interface IExecutionContextParser<T>
    {
        GrouperExecutionContext ParseToExecutionContext(T input);
    }
    public class GrouperExecutionContext
    {
        public bool OnlineChecks { get; set; }
        public string SysvolDir { get; set; }
        public int IntLevelToShow { get; set; }
        public bool HtmlOut { get; set; }
        public string HtmlOutPath { get; set; }
        public bool DebugMode { get; set; }
        public int MaxThreads { get; set; }
        public bool PrettyOutput { get; set; }
        public bool NoMess { get; set; }
        public bool QuietMode { get; set; }
        public bool NoNtfrs { get; set; }
        public string UserDefinedDomain { get; set; }
        public string UserDefinedDomainDn { get; set; }
        public string UserDefinedPassword { get; set; }
        public string UserDefinedUsername { get; set; }
        public bool NoGrepScripts { get; set; }
    }
    public class GrouperService
    {
        private readonly Action<string> log;
        private readonly Action<string> logError;
        private readonly Action<string> debugWrite;
        private readonly Func<JObject> getDomainGpoData;
        private readonly Func<string, string[]> getSysvolDirs;
        private readonly Func<string, string[]> getGpoDirs;
        private readonly Func<JObject> getGpoPackages;
        private readonly Func<KeyValuePair<string, JToken>, JProperty> assessPackage;
        private readonly Func<string, JObject, JObject> processGpo;
        private readonly Func<List<string>, JObject> processScripts;

        public GrouperService(
            Action<string> log,
            Action<string> logError,
            Action<string> debugWrite,
            Func<JObject> getDomainGpoData,
            Func<string, string[]> getSysvolDirs,
            Func<string, string[]> getGpoDirs,
            Func<JObject> getGpoPackages,
            Func<KeyValuePair<string, JToken>, JProperty> assessPackage,
            Func<string, JObject, JObject> processGpo,
            Func<List<string>, JObject> processScripts)
        {
            this.log = log;
            this.logError = logError;
            this.debugWrite = debugWrite;
            this.getDomainGpoData = getDomainGpoData;
            this.getSysvolDirs = getSysvolDirs;
            this.getGpoDirs = getGpoDirs;
            this.getGpoPackages = getGpoPackages;
            this.assessPackage = assessPackage;
            this.processGpo = processGpo;
            this.processScripts = processScripts;
        }

        public IRunResult Run(GrouperExecutionContext executionContext)
        {
            // Ask the DC for GPO details
            string currentDomainString = "";

            if (executionContext.OnlineChecks)
            {
                if (!string.IsNullOrWhiteSpace(executionContext.UserDefinedDomain))
                {
                    currentDomainString = executionContext.UserDefinedDomain;
                }
                else
                {
                    log?.Invoke("\nTrying to figure out what AD domain we're working with.");
                    try
                    {
                        currentDomainString = Domain.GetCurrentDomain().ToString();
                    }
                    catch (ActiveDirectoryOperationException e)
                    {
                        logError?.Invoke("\nCouldn't talk to the domain properly. If you're trying to run offline you should use the -o switch. Failing that, try rerunning with -d to specify a domain or -v to get more information about the error.");
                        debugWrite?.Invoke(e.ToString());

                        return new InvalidDomain();
                    }
                }

                log?.Invoke("\nCurrent AD Domain is: " + currentDomainString);

                // if we're online, get a bunch of metadata about the GPOs via LDAP
                JObject domainGpos = new JObject();

                if (executionContext.OnlineChecks)
                {
                    domainGpos = getDomainGpoData();
                }

                log?.Invoke("");

                if (executionContext.SysvolDir == "")
                {
                    executionContext.SysvolDir = @"\\" + currentDomainString + @"\sysvol\" + currentDomainString + @"\";
                    log?.Invoke("Targeting SYSVOL at: " + executionContext.SysvolDir);
                }
            }
            else if ((executionContext.OnlineChecks == false) && executionContext.SysvolDir.Length > 1)
            {
                log?.Invoke("\nTargeting SYSVOL at: " + executionContext.SysvolDir);
            }
            else
            {
                logError?.Invoke("\nSomething went wrong with parsing the path to sysvol and I gave up.");
                return new InvalidDomain();
            }


            // get all the dirs with Policies and scripts in an array.
            string[] sysvolDirs = getSysvolDirs(executionContext.SysvolDir);

            logError("\nI found all these directories in SYSVOL...");
            logError("#########################################");
            foreach (string line in sysvolDirs)
            {
                logError(line);
            }
            logError?.Invoke("#########################################");

            List<string> sysvolPolDirs = new List<string>();
            List<string> sysvolScriptDirs = new List<string>();

            if (executionContext.NoNtfrs)
            {
                logError("... but I'm not going to look in any of them except .\\Policies and .\\Scripts because you told me not to.");
                sysvolPolDirs.Add(executionContext.SysvolDir + "Policies\\");
                sysvolScriptDirs.Add(executionContext.SysvolDir + "Scripts\\");
            }
            else
            {
                logError("... and I'm going to find all the goodies I can in all of them.");
                foreach (string dir in sysvolDirs)
                {
                    if (dir.ToLower().Contains("scripts"))
                    {
                        sysvolScriptDirs.Add(dir);
                    }

                    if (dir.ToLower().Contains("policies"))
                    {
                        sysvolPolDirs.Add(dir);
                    }
                }
            }

            // get all the policy dirs
            List<string> gpoPaths = new List<string>();
            foreach (string policyPath in sysvolPolDirs)
            {
                try
                {
                    gpoPaths = getGpoDirs?.Invoke(policyPath).ToList();
                }
                catch (Exception e)
                {
                    logError("I had a problem with " + policyPath + ". I guess you could try to fix it?");
                    debugWrite(e.ToString());
                }
            }


            return new Grouper2Output
            {
                Value = RunCore(
                    executionContext.MaxThreads,
                    logError,
                    gpoPaths,
                    executionContext.OnlineChecks,
                    getGpoPackages,
                    assessPackage,
                    processGpo,
                    executionContext.QuietMode,
                    executionContext.NoGrepScripts,
                    sysvolScriptDirs,
                    processScripts)
            };
        }


        internal static JObject RunCore(
            int maxThreads,
            Action<string> logError,
            List<string> gpoPaths,
            bool onlineChecks,
            Func<JObject> getGpoPackages,
            Func<KeyValuePair<string, JToken>, JProperty> assessPackage,
            Func<string, JObject, JObject> processGpo,
            bool quietMode,
            bool noGrepScripts,
            List<string> sysvolScriptDirs,
            Func<List<string>, JObject> processScripts)
        {
            // create a JObject to put all our output goodies in.
            JObject grouper2Output = new JObject();
            // so for each uid directory (including ones with that dumb broken domain replication condition)
            // we're going to gather up all our goodies and put them into that dict we just created.
            // Create a TaskScheduler
            LimitedConcurrencyLevelTaskScheduler lcts = new LimitedConcurrencyLevelTaskScheduler(maxThreads);

            // create a TaskFactory
            TaskFactory gpoFactory = new TaskFactory(lcts);
            CancellationTokenSource gpocts = new CancellationTokenSource();

            logError?.Invoke("\n" + gpoPaths.Count.ToString() + " GPOs to process.");
            logError?.Invoke("\nStarting processing GPOs with " + maxThreads.ToString() + " threads.");


            JObject taskErrors = new JObject();
            List<Task> gpoTasks = new List<Task>();

            JObject gpoPackageData = new JObject();

            // Grab Packages from LDAP
            if (onlineChecks)
            {
                gpoPackageData = getGpoPackages();
            }

            // Create a task for each GPO
            foreach (string gpoPath in gpoPaths)
            {
                // skip PolicyDefinitions directory
                if (gpoPath.Contains("PolicyDefinitions"))
                {
                    continue;
                }
                Task t = gpoFactory.StartNew(() =>
                {
                    try
                    {
                        JObject matchedPackages = new JObject();
                        if (onlineChecks)
                        {
                            // figure out the gpo UID from the path so we can see which packages need to be processed here.
                            string[] gpoPathArr = gpoPath.Split('{');
                            string gpoPathBackString = gpoPathArr[1];
                            string[] gpoPathBackArr = gpoPathBackString.Split('}');
                            string gpoUid = gpoPathBackArr[0].ToString().ToLower();

                            // see if we have any appropriate matching packages and construct a little bundle
                            foreach (KeyValuePair<string, JToken> gpoPackage in gpoPackageData)
                            {
                                string packageParentGpoUid = gpoPackage.Value["ParentGPO"].ToString().ToLower().Trim('{', '}');
                                if (packageParentGpoUid == gpoUid)
                                {
                                    JProperty assessedPackage = assessPackage(gpoPackage);
                                    if (assessedPackage != null)
                                    {
                                        matchedPackages.Add(assessedPackage);
                                    }
                                }
                            }
                        }


                        JObject gpoFindings = processGpo(gpoPath, matchedPackages);

                        if (gpoFindings != null)
                        {
                            if (gpoFindings.HasValues)
                            {
                                lock (grouper2Output)
                                {
                                    if (!(gpoPath.Contains("NTFRS")))
                                    {
                                        grouper2Output.Add(("Current Policy - " + gpoPath), gpoFindings);
                                    }
                                    else
                                    {
                                        grouper2Output.Add(gpoPath, gpoFindings);
                                    }
                                }
                            }
                        }
                    }

                    catch (Exception e)
                    {
                        taskErrors.Add(gpoPath, e.ToString());
                    }
                }, gpocts.Token);
                gpoTasks.Add(t);
            }

            // put 'em all in a happy little array
            Task[] gpoTaskArray = gpoTasks.ToArray();

            // create a little counter to provide status updates
            int totalGpoTasksCount = gpoTaskArray.Length;
            int incompleteTaskCount = gpoTaskArray.Length;
            int remainingTaskCount = gpoTaskArray.Length;

            while (remainingTaskCount > 0)
            {
                Task[] incompleteTasks = Array.FindAll(gpoTaskArray, element => element.Status != TaskStatus.RanToCompletion);
                incompleteTaskCount = incompleteTasks.Length;
                Task[] faultedTasks = Array.FindAll(gpoTaskArray, element => element.Status == TaskStatus.Faulted);
                int faultedTaskCount = faultedTasks.Length;
                int completeTaskCount = totalGpoTasksCount - incompleteTaskCount - faultedTaskCount;
                int percentage = (int)Math.Round((double)(100 * completeTaskCount) / totalGpoTasksCount);
                string percentageString = percentage.ToString();
                if (quietMode != true)
                {
                    logError?.Invoke("");

                    logError?.Invoke("\r" + completeTaskCount.ToString() + "/" + totalGpoTasksCount.ToString() + " GPOs processed. " + percentageString + "% complete. ");
                    if (faultedTaskCount > 0)
                    {
                        logError?.Invoke(faultedTaskCount.ToString() + " GPOs failed to process.");
                    }
                }

                remainingTaskCount = incompleteTaskCount - faultedTaskCount;
            }

            // make double sure tasks all finished
            Task.WaitAll(gpoTasks.ToArray());
            gpocts.Dispose();

            // do the script grepping
            if (!noGrepScripts)
            {
                logError("\n\nProcessing SYSVOL script dirs.\n\n");
                JObject processedScriptDirs = processScripts(sysvolScriptDirs);
                if ((processedScriptDirs != null) && (processedScriptDirs.HasValues))
                {
                    grouper2Output.Add("Scripts", processedScriptDirs);
                }
            }

            logError?.Invoke("Errors in processing GPOs:");
            logError?.Invoke(taskErrors.ToString());

            return grouper2Output;
        }

        public interface IRunResult { }

        public struct InvalidDomain : IRunResult { }

        public class Grouper2Output : IRunResult
        {
            public JObject Value { get; set; }
        }

        internal JObject RunCore2(
            int maxThreads,
            Action<string> logError,
            List<string> gpoPaths,
            bool onlineChecks,
            Func<JObject> getGpoPackages,
            Func<KeyValuePair<string, JToken>, JProperty> assessPackage,
            Func<string, JObject, JObject> processGpo,
            bool quietMode,
            bool noGrepScripts,
            List<string> sysvolScriptDirs,
            Func<List<string>, JObject> processScripts)
        {
            // create a JObject to put all our output goodies in.
            JObject grouper2Output = new JObject();
            // so for each uid directory (including ones with that dumb broken domain replication condition)
            // we're going to gather up all our goodies and put them into that dict we just created.
            // Create a TaskScheduler
            //LimitedConcurrencyLevelTaskScheduler lcts = new LimitedConcurrencyLevelTaskScheduler(maxThreads);

            // create a TaskFactory
            //TaskFactory gpoFactory = new TaskFactory(lcts);
            CancellationTokenSource gpocts = new CancellationTokenSource();

            logError?.Invoke("\n" + gpoPaths.Count.ToString() + " GPOs to process.");
            logError?.Invoke("\nStarting processing GPOs with " + maxThreads.ToString() + " threads.");


            //JObject taskErrors = new JObject();
            //List<Task> gpoTasks = new List<Task>();

            //JObject gpoPackageData = new JObject();

            // Grab Packages from LDAP
            //if (onlineChecks)
            //{
            //    gpoPackageData = getGpoPackages();
            //}

            var properties = new ConcurrentBag<(string propertyName, JToken value)>();
            var threadSafeGetGpoPackages = new Lazy<JObject>(getGpoPackages, true);

            // Create a task for each GPO
            (Task[] gpoTasks, (string gpoPath, string error)[] taskErrors) =
                RR(maxThreads, s => P(onlineChecks, s, threadSafeGetGpoPackages), gpoPaths, properties, gpocts.Token);

            // put 'em all in a happy little array
            //Task[] gpoTaskArray = gpoTasks.ToArray();

            // create a little counter to provide status updates
            int totalGpoTasksCount = gpoTasks.Length;
            int incompleteTaskCount = gpoTasks.Length;
            int remainingTaskCount = gpoTasks.Length;

            while (remainingTaskCount > 0)
            {
                Task[] incompleteTasks = Array.FindAll(gpoTasks, element => element.Status != TaskStatus.RanToCompletion);
                incompleteTaskCount = incompleteTasks.Length;
                Task[] faultedTasks = Array.FindAll(gpoTasks, element => element.Status == TaskStatus.Faulted);
                int faultedTaskCount = faultedTasks.Length;
                int completeTaskCount = totalGpoTasksCount - incompleteTaskCount - faultedTaskCount;
                int percentage = (int)Math.Round((double)(100 * completeTaskCount) / totalGpoTasksCount);
                string percentageString = percentage.ToString();
                if (quietMode != true)
                {
                    logError?.Invoke("");

                    logError?.Invoke("\r" + completeTaskCount.ToString() + "/" + totalGpoTasksCount.ToString() + " GPOs processed. " + percentageString + "% complete. ");
                    if (faultedTaskCount > 0)
                    {
                        logError?.Invoke(faultedTaskCount.ToString() + " GPOs failed to process.");
                    }
                }

                remainingTaskCount = incompleteTaskCount - faultedTaskCount;
            }

            // make double sure tasks all finished
            Task.WaitAll(gpoTasks);
            gpocts.Dispose();

            // do the script grepping
            if (!noGrepScripts)
            {
                logError("\n\nProcessing SYSVOL script dirs.\n\n");
                JObject processedScriptDirs = processScripts(sysvolScriptDirs);
                if ((processedScriptDirs != null) && (processedScriptDirs.HasValues))
                {
                    grouper2Output.Add("Scripts", processedScriptDirs);
                }
            }

            logError?.Invoke("Errors in processing GPOs:");

            if (taskErrors.Any())
            {
                var jObj = new JObject();
                foreach ((string gpoPath, string error) in taskErrors)
                {
                    jObj.Add(gpoPath, error);
                }
                logError?.Invoke(jObj.ToString());
            }

            return grouper2Output;
        }

        (Task[] gpoTasks, (string gpoPath, string error)[] taskErrors) RR(
            int maxThreads,
            Func<string, IEnumerable<(string propertyName, JToken value)>> f,
            List<string> gpoPaths,
            ConcurrentBag<(string propertyName, JToken value)> properties,
            CancellationToken token)
        {
            List<(string gpoPath, string error)> taskErrors = new List<(string gpoPath, string error)>();
            List<Task> gpoTasks = new List<Task>();
            LimitedConcurrencyLevelTaskScheduler lcts = new LimitedConcurrencyLevelTaskScheduler(maxThreads);
            TaskFactory gpoFactory = new TaskFactory(lcts);

            // Create a task for each GPO
            foreach (string gpoPath in gpoPaths)
            {
                // skip PolicyDefinitions directory
                if (gpoPath.Contains("PolicyDefinitions"))
                {
                    continue;
                }

                Task t = gpoFactory.StartNew(() =>
                {
                    try
                    {
                        foreach (var item in f(gpoPath))
                        {
                            properties.Add(item);
                        }
                    }

                    catch (Exception e)
                    {
                        taskErrors.Add((gpoPath, e.ToString()));
                    }
                }, token);

                gpoTasks.Add(t);
            }

            return (gpoTasks.ToArray(), taskErrors.ToArray());
        }

        IEnumerable<(string propertyName, JToken value)> P(bool onlineChecks, string gpoPath, Lazy<JObject> getGpoPackageData)
        {
            JObject matchedPackages = new JObject();

            if (onlineChecks)
            {
                // figure out the gpo UID from the path so we can see which packages need to be processed here.
                string[] gpoPathArr = gpoPath.Split('{');
                string gpoPathBackString = gpoPathArr[1];
                string[] gpoPathBackArr = gpoPathBackString.Split('}');
                string gpoUid = gpoPathBackArr[0].ToString().ToLower();

                // see if we have any appropriate matching packages and construct a little bundle
                foreach (KeyValuePair<string, JToken> gpoPackage in getGpoPackageData.Value)
                {
                    string packageParentGpoUid = gpoPackage.Value["ParentGPO"].ToString().ToLower().Trim('{', '}');
                    if (packageParentGpoUid == gpoUid)
                    {
                        JProperty assessedPackage = assessPackage(gpoPackage);
                        if (assessedPackage != null)
                        {
                            matchedPackages.Add(assessedPackage);
                        }
                    }
                }
            }

            JObject gpoFindings = processGpo(gpoPath, matchedPackages);

            if (gpoFindings != null)
            {
                if (gpoFindings.HasValues)
                {
                    if (!(gpoPath.Contains("NTFRS")))
                    {
                        yield return ("Current Policy - " + gpoPath, gpoFindings);
                    }
                    else
                    {
                        yield return (gpoPath, gpoFindings);
                    }
                }
            }
        }

        public static JObject ProcessGpo(
            string gpoPath,
            JObject matchedPackages,
            bool onlineChecks,
            Func<JObject> getDomainGpoData,
            Action<string> debugWrite,
            Func<string, JArray> processInf,
            Func<string, JArray> processGpXml,
            Func<string, JArray> processScriptsIni)
        {
            if (gpoPath.Contains("PolicyDefinitions"))
            {
                return null;
            }

            try
            {
                // create a dict to put the stuff we find for this GPO into.
                JObject gpoResult = new JObject();
                // Get the UID of the GPO from the file path.
                string[] splitPath = gpoPath.Split(Path.DirectorySeparatorChar);
                string gpoUid = splitPath[splitPath.Length - 1];

                // Make a JObject for GPO metadata
                JObject gpoProps = new JObject();
                // If we're online and talking to the domain, just use that data
                if (onlineChecks)
                {
                    try
                    {
                        // select the GPO's details from the gpo data we got
                        if (getDomainGpoData()[gpoUid] != null)
                        {
                            JToken domainGpo = getDomainGpoData()[gpoUid];
                            gpoProps = (JObject)JToken.FromObject(domainGpo);
                            gpoProps.Add("gpoPath", gpoPath);
                        }
                        else
                        {
                            debugWrite("Couldn't get GPO Properties from the domain for the following GPO: " + gpoUid);
                            // if we weren't able to select the GPO's details, do what we can with what we have.
                            gpoProps = new JObject()
                            {
                                {"UID", gpoUid},
                                {"gpoPath", gpoPath}
                            };
                        }
                    }
                    catch (ArgumentNullException e)
                    {
                        debugWrite(e.ToString());
                    }
                }
                // otherwise do what we can with what we have
                else
                {
                    gpoProps = new JObject()
                    {
                        {"UID", gpoUid},
                        {"gpoPath", gpoPath}
                    };
                }

                gpoResult.Add("GPOProps", gpoProps);
                //JObject gpoResultJson = (JObject) JToken.FromObject(gpoResult);

                // if I were smarter I would have done this shit with the machine and user dirs inside the Process methods instead of calling each one twice out here.
                // @liamosaur you reckon you can see how to clean it up after the fact?
                // Get the paths for the machine policy and user policy dirs
                string machinePolPath = Path.Combine(gpoPath, "Machine");
                string userPolPath = Path.Combine(gpoPath, "User");

                // Process Inf and Xml Policy data for machine and user
                JArray machinePolInfResults = processInf(machinePolPath);
                JArray userPolInfResults = processInf(userPolPath);
                JArray machinePolGppResults = processGpXml(machinePolPath);
                JArray userPolGppResults = processGpXml(userPolPath);
                JArray machinePolScriptResults = processScriptsIni(machinePolPath);
                JArray userPolScriptResults = processScriptsIni(userPolPath);

                // add all our findings to a JArray in what seems a very inefficient manner but it's the only way i could see to avoid having a JArray of JArrays of Findings.
                JArray userFindings = new JArray();
                JArray machineFindings = new JArray();

                JArray[] allMachineGpoResults =
                {
                    machinePolInfResults,
                    machinePolGppResults,
                    machinePolScriptResults
                };

                JArray[] allUserGpoResults =
                {
                    userPolInfResults,
                    userPolGppResults,
                    userPolScriptResults
                };

                foreach (JArray machineGpoResult in allMachineGpoResults)
                {
                    if (machineGpoResult != null && machineGpoResult.HasValues)
                    {
                        foreach (JObject finding in machineGpoResult)
                        {
                            machineFindings.Add(finding);
                        }
                    }
                }

                foreach (JArray userGpoResult in allUserGpoResults)
                {
                    if (userGpoResult != null && userGpoResult.HasValues)
                    {
                        foreach (JObject finding in userGpoResult)
                        {
                            userFindings.Add(finding);
                        }
                    }
                }

                JObject allFindings = new JObject();

                // if there are any Findings, add it to the final output.
                if (userFindings.HasValues)
                {
                    JProperty userFindingsJProp = new JProperty("User Policy", userFindings);
                    allFindings.Add(userFindingsJProp);
                }
                if (machineFindings.HasValues)
                {
                    JProperty machineFindingsJProp = new JProperty("Machine Policy", machineFindings);
                    allFindings.Add(machineFindingsJProp);
                }
                if (matchedPackages.HasValues)
                {
                    JProperty packageFindingsJProp = new JProperty("Packages", matchedPackages);
                    allFindings.Add(packageFindingsJProp);
                }
                if (allFindings.HasValues)
                {
                    gpoResult.Add("Findings", allFindings);
                    return gpoResult;
                }
            }
            catch (UnauthorizedAccessException e)
            {
                debugWrite(e.ToString());
            }

            return null;
        }

        private static JObject ProcessScripts(
            Action<string> debugWrite,
            List<string> scriptDirs,
            Func<string, JObject> investigateFileContents,
            int intLevelToShow)
        {
            // output object
            JObject processedScripts = new JObject();

            foreach (string scriptDir in scriptDirs)
            {
                try
                {
                    // get all the files in this dir
                    string[] scriptDirFiles = Directory.GetFiles(scriptDir, "*", SearchOption.AllDirectories);
                    // add them all to the master list of files
                    foreach (string scriptDirFile in scriptDirFiles)
                    {
                        // get the file info so we can check size
                        FileInfo scriptFileInfo = new FileInfo(scriptDirFile);
                        // if it's not too big
                        if (scriptFileInfo.Length >= 200000)
                        {
                            continue;
                        }
                        // feed the whole thing through Utility.InvestigateFileContents
                        JObject investigatedScript = investigateFileContents(scriptDirFile);
                        // if we got anything good, add the result to processedScripts
                        if (investigatedScript != null)
                        {
                            if (((int)investigatedScript["InterestLevel"]) >= intLevelToShow)
                            {
                                processedScripts.Add(new JProperty(scriptDirFile, investigatedScript));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    debugWrite(e.ToString());
                }
            }

            if (!processedScripts.HasValues)
            {
                return null;
            }

            return processedScripts;
        }
    }
}
