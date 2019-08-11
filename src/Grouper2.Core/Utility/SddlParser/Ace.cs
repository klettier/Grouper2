using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grouper2.Core.SddlParser
{
    public class Ace
    {
        public string Raw { get; }
        public string AceType { get; }
        public string[] AceFlags { get; }
        public string[] Rights { get; }
        public string ObjectGuid { get; }
        public string InheritObjectGuid { get; }
        public Sid AceSid { get; }

        public Ace(string ace, Action<string> debugWrite, Func<string, Sid> toSid, SecurableObjectType type = SecurableObjectType.Unknown)
        {
            string[] parts = ace.Split(SeparatorToken);

            if (parts.Length < 6)
            {
                debugWrite?.Invoke("Incorrect ACE format?");
                debugWrite?.Invoke(ace.ToString());
                // ERROR Ace have incorrect format - less parts than 6.
            }

            // ace_type
            if (parts.Length > 0 && parts[0].Length > 0)
            {
                string aceType = Match.OneByPrefix(parts[0], AceTypesDict, out string reminder);

                if (aceType == null || !string.IsNullOrEmpty(reminder))
                    aceType = Format.Unknown(parts[0]);

                AceType = aceType;
            }

            // ace_flags
            if (parts.Length > 1 && parts[1].Length > 0)
            {
                LinkedList<string> flags = Match.ManyByPrefix(parts[1], AceFlagsDict, out string reminder);

                if (!string.IsNullOrEmpty(reminder))
                    flags.AddLast(Format.Unknown(reminder));

                AceFlags = flags.ToArray();
            }

            // rights
            if (parts.Length > 2 && parts[2].Length > 0)
            {
                if (TryParseHex(parts[2], out uint accessMask))
                {
                    IEnumerable<string> rights = Enumerable.Empty<string>();

                    if (AceUintSpecificRightsDict.TryGetValue(type, out Dictionary<uint, string> aceUintSpecificRightsForType))
                        rights = rights.Concat(Match.ManyByUint(accessMask, aceUintSpecificRightsForType, out accessMask));

                    rights = rights.Concat(Match.ManyByUint(accessMask, AceUintRightsDict, out accessMask));

                    if (accessMask > 0)
                        rights = rights.Concat(new[] { Format.Unknown($"0x{accessMask:X}") });

                    Rights = rights.ToArray();
                }
                else
                {
                    LinkedList<string> rights = Match.ManyByPrefix(parts[2], AceAliasRightsDict, out string reminder);

                    if (!string.IsNullOrEmpty(reminder))
                        rights.AddLast(Format.Unknown(reminder));

                    Rights = rights.ToArray();
                }
            }

            // object_guid
            if (parts.Length > 3 && parts[3].Length > 0)
            {
                ObjectGuid = parts[3];
            }

            // inherit_object_guid
            if (parts.Length > 4 && parts[4].Length > 0)
            {
                InheritObjectGuid = parts[4];
            }

            // account_sid
            if (parts.Length > 5 && parts[5].Length > 0)
            {
                AceSid = toSid(parts[5]);
            }

            // resource_attribute
            if (parts.Length > 6)
            {
                // unsupported
            }
        }

        private bool TryParseHex(string hex, out uint result)
        {
            if (hex.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase) ||
                hex.StartsWith("&H", StringComparison.CurrentCultureIgnoreCase))
            {
                hex = hex.Substring(2);
            }
            else
            {
                result = default(uint);
                return false;
            }

            return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out result);
        }

        internal const char BeginToken = '(';
        internal const char EndToken = ')';
        internal const char SeparatorToken = ';';

        /// <summary>
        /// A dictionary of ace type strings as defined in https://msdn.microsoft.com/en-us/library/windows/desktop/aa374928(v=vs.85).aspx#ace_types
        /// </summary>
        internal static Dictionary<string, string> AceTypesDict = new Dictionary<string, string>
        {
            { "A", "ACCESS_ALLOWED" },
            { "D", "ACCESS_DENIED" },
            { "OA", "OBJECT_ACCESS_ALLOWED" },
            { "OD", "OBJECT_ACCESS_DENIED" },
            { "AU", "AUDIT" },
            { "AL", "ALARM" },
            { "OU", "OBJECT_AUDIT" },
            { "OL", "OBJECT_ALARM" },
            { "ML", "MANDATORY_LABEL" },
            { "TL", "PROCESS_TRUST_LABEL" },
            { "XA", "CALLBACK_ACCESS_ALLOWED" },
            { "XD", "CALLBACK_ACCESS_DENIED" },
            { "RA", "RESOURCE_ATTRIBUTE" },
            { "SP", "SCOPED_POLICY_ID" },
            { "XU", "CALLBACK_AUDIT" },
            { "ZA", "CALLBACK_OBJECT_ACCESS_ALLOWED" },
        };

        /// <summary>
        /// A dictionary of ace flag strings as defined in https://msdn.microsoft.com/en-us/library/windows/desktop/aa374928(v=vs.85).aspx#ace_flags
        /// </summary>
        internal static Dictionary<string, string> AceFlagsDict = new Dictionary<string, string>
        {
            { "CI", "CONTAINER_INHERIT" },
            { "OI", "OBJECT_INHERIT" },
            { "NP", "NO_PROPAGATE" },
            { "IO", "INHERIT_ONLY" },
            { "ID", "INHERITED" },
            { "SA", "AUDIT_SUCCESS" },
            { "FA", "AUDIT_FAILURE" },
        };

        /// <summary>
        /// A dictionary of access right alias strings as defined in https://msdn.microsoft.com/en-us/library/windows/desktop/aa374928(v=vs.85).aspx#rights
        /// </summary>
        internal static Dictionary<string, string> AceAliasRightsDict = new Dictionary<string, string>
        {
            // Generic access rights
            { "GA", "GENERIC_ALL" },
            { "GR", "GENERIC_READ" },
            { "GW", "GENERIC_WRITE" },
            { "GX", "GENERIC_EXECUTE" },
            
            // Standard access rights
            { "RC", "READ_CONTROL" },
            { "SD", "STANDARD_DELETE" },
            { "WD", "WRITE_DAC" },
            { "WO", "WRITE_OWNER" },

            // Directory service object access rights
            { "RP", "READ_PROPERTY" },
            { "WP", "WRITE_PROPERTY" },
            { "CC", "CREATE_CHILD" },
            { "DC", "DELETE_CHILD" },
            { "LC", "LIST_CHILDREN" },
            { "SW", "SELF_WRITE" },
            { "LO", "LIST_OBJECT" },
            { "DT", "DELETE_TREE" },
            { "CR", "CONTROL_ACCESS" },

            // File access rights
            { "FA", "FILE_ALL" },
            { "FR", "FILE_READ" },
            { "FW", "FILE_WRITE" },
            { "FX", "FILE_EXECUTE" },

            // Registry access rights
            { "KA", "KEY_ALL" },
            { "KR", "KEY_READ" },
            { "KW", "KEY_WRITE" },
            { "KX", "KEY_EXECUTE" },

            // Mandatory label rights
            { "NR", "NO_READ_UP" },
            { "NW", "NO_WRITE_UP" },
            { "NX", "NO_EXECUTE_UP" },
        };

        /// <summary>
        /// A dictionary of access right alias strings as defined in winnt.h.
        /// The numeric layout of AccessMask is explained here https://msdn.microsoft.com/pl-pl/library/windows/desktop/aa374896(v=vs.85).aspx.
        /// </summary>
        internal static Dictionary<uint, string> AceUintRightsDict = new Dictionary<uint, string>
        {
            // Generic access rights
            { 0x80000000, "GENERIC_READ" },
            { 0x40000000, "GENERIC_WRITE" },
            { 0x20000000, "GENERIC_EXECUTE" },
            { 0x10000000, "GENERIC_ALL" },

            // Reserved access rights
            { 0x02000000, "MAXIMUM_ALLOWED"},
            { 0x01000000, "ACCESS_SYSTEM_SECURITY"},

            // Standard access rights
            { 0x001f0000, "STANDARD_RIGHTS_ALL"},
            { 0x00100000, "SYNCHRONIZE"},
            { 0x00080000, "WRITE_OWNER"},
            { 0x00040000, "WRITE_DAC"},
            { 0x00020000, "READ_CONTROL"},
            { 0x00010000, "DELETE"},
        };

        /// <summary>
        /// A dictionary of object-specific access right alias strings as defined in winnt.h.
        /// Sample winnt.h definition can be found here: https://source.winehq.org/source/include/winnt.h.
        /// </summary>
        internal static Dictionary<SecurableObjectType, Dictionary<uint, string>> AceUintSpecificRightsDict = new Dictionary<SecurableObjectType, Dictionary<uint, string>>
        {
            {
                SecurableObjectType.File,
                new Dictionary<uint, string>
                {
                    // combined
                    { 0x001f01ff, "ALL_ACCESS" },
                    { 0x001200a0, "GENERIC_EXECUTE"},
                    { 0x00120116, "GENERIC_WRITE" },
                    { 0x00120089, "GENERIC_READ" },

                    { 0x00000100, "WRITE_ATTRIBUTES" },
                    { 0x00000080, "READ_ATTRIBUTES" },
                    { 0x00000020, "EXECUTE" },
                    { 0x00000010, "WRITE_PROPERTIES" },
                    { 0x00000008, "READ_PROPERTIES" },
                    { 0x00000004, "APPEND_DATA" },
                    { 0x00000002, "WRITE_DATA" },
                    { 0x00000001, "READ_DATA" },
                }
            },
            {
                SecurableObjectType.Directory,
                new Dictionary<uint, string>
                {
                    // combined
                    { 0x001f01ff, "ALL_ACCESS" },
                    { 0x001200a0, "GENERIC_EXECUTE"},
                    { 0x00120116, "GENERIC_WRITE" },
                    { 0x00120089, "GENERIC_READ" },

                    { 0x00000100, "WRITE_ATTRIBUTES" },
                    { 0x00000080, "READ_ATTRIBUTES" },
                    { 0x00000040, "DELETE_CHILD" },
                    { 0x00000020, "TRAVERSE" },
                    { 0x00000010, "WRITE_PROPERTIES" },
                    { 0x00000008, "READ_PROPERTIES" },
                    { 0x00000004, "ADD_SUBDIRECTORY" },
                    { 0x00000002, "ADD_FILE" },
                    { 0x00000001, "LIST_DIRECTORY" },
                }
            },
            {
                SecurableObjectType.Pipe,
                new Dictionary<uint, string>
                {
                    // combined
                    { 0x001f01ff, "ALL_ACCESS" },
                    { 0x001200a0, "GENERIC_EXECUTE"},
                    { 0x00120116, "GENERIC_WRITE" },
                    { 0x00120089, "GENERIC_READ" },

                    { 0x00000100, "WRITE_ATTRIBUTES" },
                    { 0x00000080, "READ_ATTRIBUTES" },
                    { 0x00000004, "CREATE_PIPE_INSTANCE" },
                    { 0x00000002, "WRITE_DATA" },
                    { 0x00000001, "READ_DATA" },
                }
            },
            {
                SecurableObjectType.Process,
                new Dictionary<uint, string>
                {
                    // combined
                    { 0x0012ffff, "ALL_ACCESS" },

                    { 0x00002000, "SET_LIMITED_INFORMATION" },
                    { 0x00001000, "QUERY_LIMITED_INFORMATION" },
                    { 0x00000800, "SUSPEND_RESUME" },
                    { 0x00000400, "QUERY_INFORMATION" },
                    { 0x00000200, "SET_INFORMATION" },
                    { 0x00000100, "SET_QUOTA" },
                    { 0x00000080, "CREATE_PROCESS" },
                    { 0x00000040, "DUP_HANDLE" },
                    { 0x00000020, "VM_WRITE" },
                    { 0x00000010, "VM_READ" },
                    { 0x00000008, "VM_OPERATION" },
                    { 0x00000002, "CREATE_THREAD" },
                    { 0x00000001, "TERMINATE" },
                }
            },
            {
                SecurableObjectType.Thread,
                new Dictionary<uint, string>
                {
                    // combined
                    { 0x0012ffff, "ALL_ACCESS" },

                    { 0x00001000, "RESUME" },
                    { 0x00000800, "QUERY_LIMITED_INFORMATION" },
                    { 0x00000400, "SET_LIMITED_INFORMATION" },
                    { 0x00000200, "DIRECT_IMPERSONATION" },
                    { 0x00000100, "IMPERSONATE" },
                    { 0x00000080, "SET_THREAD_TOKEN" },
                    { 0x00000040, "QUERY_INFORMATION" },
                    { 0x00000020, "SET_INFORMATION" },
                    { 0x00000010, "SET_CONTEXT" },
                    { 0x00000008, "GET_CONTEXT" },
                    { 0x00000002, "SUSPEND_RESUME" },
                    { 0x00000001, "TERMINATE" },
                }
            },
            {
                SecurableObjectType.AccessToken,
                new Dictionary<uint, string>
                {
                    // combined
                    { 0x000201ff, "ALL_ACCESS" },
                    { 0x000200e0, "WRITE" },
                    { 0x00020008, "READ" },
                    { 0x00020000, "EXECUTE" },

                    { 0x00000100, "ADJUST_SESSIONID" },
                    { 0x00000080, "ADJUST_DEFAULT" },
                    { 0x00000040, "ADJUST_GROUPS" },
                    { 0x00000020, "ADJUST_PRIVILEGES" },
                    { 0x00000010, "QUERY_SOURCE" },
                    { 0x00000008, "QUERY" },
                    { 0x00000004, "IMPERSONATE" },
                    { 0x00000002, "DUPLICATE" },
                    { 0x00000001, "ASSIGN_PRIMARY" },
                }
            },
            {
                SecurableObjectType.RegistryKey,
                new Dictionary<uint, string>
                {
                    // combined
                    { 0x000201ff, "ALL_ACCESS" },
                    { 0x000200e0, "WRITE" },
                    { 0x00020008, "READ" },
                    { 0x00020000, "EXECUTE" },

                    { 0x00000300, "WOW64_RES" },
                    { 0x00000200, "WOW64_32KEY" },
                    { 0x00000100, "WOW64_64KEY" },
                    { 0x00000020, "CREATE_LINK" },
                    { 0x00000010, "NOTIFY" },
                    { 0x00000008, "ENUMERATE_SUB_KEYS" },
                    { 0x00000004, "CREATE_SUB_KEY" },
                    { 0x00000002, "SET_VALUE" },
                    { 0x00000001, "QUERY_VALUE" },
                }
            }
        };

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            if (AceSid != null)
                sb.AppendLineEnv($"{nameof(AceSid)}: {AceSid.ToString()}");

            if (AceType != null)
                sb.AppendLineEnv($"{nameof(AceType)}: {AceType.ToString()}");

            if (AceFlags != null && AceFlags.Any())
                sb.AppendLineEnv($"{nameof(AceFlags)}: {string.Join(", ", AceFlags)}");

            if (Rights != null && Rights.Any())
            {
                sb.AppendLineEnv($"{nameof(Rights)}:");
                foreach (string t in Rights)
                    sb.AppendIndentEnv(t);
            }

            if (ObjectGuid != null)
                sb.AppendLineEnv($"{nameof(ObjectGuid)}: {ObjectGuid.ToString()}");

            if (InheritObjectGuid != null)
                sb.AppendLineEnv($"{nameof(InheritObjectGuid)}: {InheritObjectGuid.ToString()}");

            return sb.ToString();
        }
    }
}