using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class VariableNameGenerator
    {
        private readonly HashSet<string> _unavailableNames;

        public VariableNameGenerator(IEnumerable<string> initialUnavailableNames)
        {
            _unavailableNames = new HashSet<string>(initialUnavailableNames);
        }

        public string GenerateFreshName(string originalName)
        {
            string name;
            if (_unavailableNames.Contains(originalName))
            {
                int index = 1;
                do
                {
                    index++;
                    name = $"{originalName}{index}";
                }
                while (_unavailableNames.Contains(name));
            }
            else
            {
                name = originalName;
            }
            _unavailableNames.Add(name);
            return name;
        }
    }
}
