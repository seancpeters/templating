﻿using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class RandomMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public int Low { get; private set; }

        public int High { get; private set; }

        public RandomMacroConfig(string variableName, string action, int low, int? high)
        {
            VariableName = variableName;
            Type = "random";
            Action = action;
            Low = low;
            High = high ?? int.MaxValue;
        }
    }
}

    
