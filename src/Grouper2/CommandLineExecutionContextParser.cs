using CommandLineParser.Arguments;
using Grouper2.Core;
using System;
using System.Linq;
using System.Text;

namespace Grouper2
{
    class InvalidArgs : GrouperExecutionContext
    {

    }

    class CommandLineExecutionContextParser : IExecutionContextParser<string[]>
    {
        private readonly Action<string> log;
        private readonly Action<string> logError;

        public CommandLineExecutionContextParser(
            Action<string> log,
            Action<string> logError)
        {
            this.log = log;
            this.logError = logError;
        }
        public GrouperExecutionContext ParseToExecutionContext(string[] input)
        {
            ValueArgument<string> htmlArg = new ValueArgument<string>('f', "html", "Path for html output file.");
            SwitchArgument debugArg = new SwitchArgument('v', "verbose", "Enables debug mode. Probably quite noisy and rarely necessary. Will also show you the names of any categories of policies that Grouper saw but didn't have any means of processing. I eagerly await your pull request.", false);
            SwitchArgument offlineArg = new SwitchArgument('o', "offline", "Disables checks that require LDAP comms with a DC or SMB comms with file shares found in policy settings. Requires that you define a value for -s.", false);
            ValueArgument<string> sysvolArg = new ValueArgument<string>('s', "sysvol", "Set the path to a domain SYSVOL directory.");
            ValueArgument<int> intlevArg = new ValueArgument<int>('i', "interestlevel", "The minimum interest level to display. i.e. findings with an interest level lower than x will not be seen in output. Defaults to 1, i.e. show everything except some extremely dull defaults. If you want to see those too, do -i 0.");
            ValueArgument<int> threadsArg = new ValueArgument<int>('t', "threads", "Max number of threads. Defaults to 10.");
            ValueArgument<string> domainArg = new ValueArgument<string>('d', "domain", "Domain to query for Group Policy Goodies.");
            ValueArgument<string> passwordArg = new ValueArgument<string>('p', "password", "Password to use for LDAP operations.");
            ValueArgument<string> usernameArg = new ValueArgument<string>('u', "username", "Username to use for LDAP operations.");
            SwitchArgument helpArg = new SwitchArgument('h', "help", "Displays this help.", false);
            SwitchArgument prettyArg = new SwitchArgument('g', "pretty", "Switches output from the raw Json to a prettier format.", false);
            SwitchArgument noMessArg = new SwitchArgument('m', "nomess", "Avoids file writes at all costs. May find less stuff.", false);
            SwitchArgument currentPolOnlyArg = new SwitchArgument('c', "currentonly", "Only checks current policies, ignoring stuff in those Policies_NTFRS_* directories that result from replication failures.", false);
            SwitchArgument noGrepScriptsArg = new SwitchArgument('n', "nogrepscripts", "Don't grep through the files in the \"Scripts\" subdirectory", false);
            SwitchArgument quietModeArg = new SwitchArgument('q', "quiet", "Enables quiet mode. Turns off progress counter.", false);

            CommandLineParser.CommandLineParser parser = new CommandLineParser.CommandLineParser();
            parser.ShowUsageCommands.Clear();

            parser.Arguments.Add(usernameArg);
            parser.Arguments.Add(passwordArg);
            parser.Arguments.Add(debugArg);
            parser.Arguments.Add(intlevArg);
            parser.Arguments.Add(sysvolArg);
            parser.Arguments.Add(offlineArg);
            parser.Arguments.Add(threadsArg);
            parser.Arguments.Add(helpArg);
            parser.Arguments.Add(prettyArg);
            parser.Arguments.Add(noMessArg);
            parser.Arguments.Add(currentPolOnlyArg);
            parser.Arguments.Add(noGrepScriptsArg);
            parser.Arguments.Add(domainArg);
            parser.Arguments.Add(htmlArg);
            parser.Arguments.Add(quietModeArg);

            try
            {
                if ((input.Contains("--help") || input.Contains("/?") || input.Contains("help")))
                {
                    parser.ShowUsage();
                    return null;
                }

                parser.ParseCommandLine(input);

                if (helpArg.Parsed)
                {
                    parser.ShowUsage();
                    return null;
                }

                var onlineChecks = true;
                string sysvolDir = "";
                int maxThreads = 10;
                bool prettyOutput = false;
                bool debugMode = false;
                var noMess = false;
                bool noNtfrs = false;
                bool quietMode = false;
                bool noGrepScripts = false;
                string userDefinedDomain = "";
                bool htmlOut = false;
                string htmlOutPath = "";
                int intLevelToShow = 0;
                string userDefinedDomainDn = null;
                string userDefinedPassword = null;
                string userDefinedUsername = null;

                if (offlineArg.Parsed && offlineArg.Value && sysvolArg.Parsed)
                {
                    // args config for valid offline run.
                    onlineChecks = false;
                    sysvolDir = sysvolArg.Value;
                }

                if (offlineArg.Parsed && offlineArg.Value && !sysvolArg.Parsed)
                {
                    // handle someone trying to run in offline mode without giving a value for sysvol
                    logError?.Invoke("\nOffline mode requires you to provide a value for -s, the path where Grouper2 can find the domain SYSVOL share, or a copy of it at least.");
                    return new InvalidArgs();
                }

                if (intlevArg.Parsed)
                {
                    // handle interest level parsing
                    logError?.Invoke("\nRoger. Everything with an Interest Level lower than " + intlevArg.Value.ToString() + " is getting thrown on the floor.");
                    intLevelToShow = intlevArg.Value;
                }
                else
                {
                    intLevelToShow = 1;
                }

                if (htmlArg.Parsed)
                {
                    htmlOut = true;
                    htmlOutPath = htmlArg.Value;
                }
                if (debugArg.Parsed)
                {
                    logError?.Invoke("\nVerbose debug mode enabled. Hope you like yellow.");
                    debugMode = true;
                }

                if (threadsArg.Parsed)
                {
                    logError?.Invoke("\nMaximum threads set to: " + threadsArg.Value);
                    maxThreads = threadsArg.Value;
                }

                if (sysvolArg.Parsed)
                {
                    logError?.Invoke("\nYou specified that I should assume SYSVOL is here: " + sysvolArg.Value);
                    sysvolDir = sysvolArg.Value;
                }

                if (prettyArg.Parsed)
                {
                    logError?.Invoke("\nSwitching output to pretty mode. Nice.");
                    prettyOutput = true;
                }

                if (noMessArg.Parsed)
                {
                    logError?.Invoke("\nNo Mess mode enabled. Good for OPSEC, maybe bad for finding all the vulns? All \"Directory Is Writable\" checks will return false.");

                    noMess = true;
                }
                if (quietModeArg.Parsed)
                {
                    quietMode = true;
                }
                if (currentPolOnlyArg.Parsed)
                {
                    logError?.Invoke("\nOnly looking at current policies and scripts, not checking any of those weird old NTFRS dirs.");
                    noNtfrs = true;
                }

                if (domainArg.Parsed)
                {
                    Console.Error.Write("\nYou told me to talk to domain " + domainArg.Value + " so I'm gonna do that.");
                    if (!(usernameArg.Parsed) || !(passwordArg.Parsed))
                    {
                        Console.Error.Write("\nIf you specify a domain you need to specify a username and password too using -u and -p.");
                    }

                    userDefinedDomain = domainArg.Value;
                    string[] splitDomain = userDefinedDomain.Split('.');
                    StringBuilder sb = new StringBuilder();
                    int pi = splitDomain.Length;
                    int ind = 1;
                    foreach (string piece in splitDomain)
                    {
                        sb.Append("DC=" + piece);
                        if (pi != ind)
                        {
                            sb.Append(",");
                        }
                        ind++;
                    }

                    userDefinedDomainDn = sb.ToString();
                    userDefinedPassword = passwordArg.Value;
                    userDefinedUsername = usernameArg.Value;
                }

                if (noGrepScriptsArg.Parsed)
                {
                    logError?.Invoke("\nNot gonna look through scripts in SYSVOL for goodies.");
                    noGrepScripts = true;
                }

                return new GrouperExecutionContext
                {
                    OnlineChecks = onlineChecks,
                    SysvolDir = sysvolDir,
                    IntLevelToShow = intLevelToShow,
                    HtmlOut = htmlOut,
                    HtmlOutPath = htmlOutPath,
                    DebugMode = debugMode,
                    MaxThreads = maxThreads,
                    PrettyOutput = prettyOutput,
                    NoMess = noMess,
                    QuietMode = quietMode,
                    NoNtfrs = noNtfrs,
                    UserDefinedDomain = userDefinedDomain,
                    UserDefinedDomainDn = userDefinedDomainDn,
                    UserDefinedPassword = userDefinedPassword,
                    UserDefinedUsername = userDefinedUsername,
                    NoGrepScripts = noGrepScripts
                };
            }
            catch (Exception e)
            {
                log?.Invoke(e.Message);
                parser.ShowUsage();
            }

            return null;
        }
    }
}
