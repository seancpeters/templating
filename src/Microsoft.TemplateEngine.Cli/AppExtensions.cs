// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class AppExtensions
    {
        public static List<string> CreateArgListFromAdditionalFiles(IList<string> extraArgFileNames)
        {
            IDictionary<string, IList<string>> argsDict = ParseArgsFromFile(extraArgFileNames);

            List<string> argsFlattened = new List<string>();
            foreach (KeyValuePair<string, IList<string>> oneArg in argsDict)
            {
                argsFlattened.Add(oneArg.Key);
                if (oneArg.Value.Count > 0)
                {
                    argsFlattened.AddRange(oneArg.Value);
                }
            }

            return argsFlattened;
        }

        public static Dictionary<string, IList<string>> ParseArgsFromFile(IList<string> extraArgFileNames)
        {
            Dictionary<string, IList<string>> parameters = new Dictionary<string, IList<string>>();

            // Note: If the same param is specified multiple times across the files, last-in-wins
            // TODO: consider another course of action.
            if (extraArgFileNames.Count > 0)
            {
                foreach (string argFile in extraArgFileNames)
                {
                    if (!File.Exists(argFile))
                    {
                        throw new CommandParserException(string.Format(LocalizableStrings.ArgsFileNotFound, argFile), argFile);
                    }

                    try
                    {
                        using (Stream s = File.OpenRead(argFile))
                        using (TextReader r = new StreamReader(s, Encoding.UTF8, true, 4096, true))
                        using (JsonTextReader reader = new JsonTextReader(r))
                        {
                            JObject obj = JObject.Load(reader);

                            foreach (JProperty property in obj.Properties())
                            {
                                if (property.Value.Type == JTokenType.String)
                                {
                                    IList<string> values = new List<string>
                                {
                                    property.Value.ToString()
                                };

                                    // adding 2 dashes to the file-based params
                                    // won't work right if there's a param that should have 1 dash
                                    //
                                    // TOOD: come up with a better way to deal with this
                                    parameters["--" + property.Name] = values;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new CommandParserException(string.Format(LocalizableStrings.ArgsFileWrongFormat, argFile), argFile, ex);
                    }
                }
            }

            return parameters;
        }
    }
}
