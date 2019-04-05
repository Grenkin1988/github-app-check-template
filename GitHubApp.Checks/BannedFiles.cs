using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GitHubApp.Core;

namespace GitHubApp.Checks {
    public class BannedFiles : ConsistencyCheckBase {
        public BannedFiles(ConsistencyContext context) : base(context) { }

        private static readonly Dictionary<string, string> m_bannedFilesAndReasons = new Dictionary<string, string> {
            { "^packages.config$", "A 'packages.config' file was found - should be a paket reference" },
            { "^paket.references$", "A 'paket.references' file was found - this should be in the style '<*.csproj>.paket.references'" }
        };

        public override string Name => "Banned Files";

        public override Interfaces.ConsistencyCheckResult Run() {
            var results = new List<Interfaces.Problem>();
            foreach (var kvp in m_bannedFilesAndReasons) {
                var matchingFiles = AllFiles
                                   .Where(x => Regex.IsMatch(x.Name, kvp.Key, RegexOptions.IgnoreCase))
                                   .Select(FileWithoutRootDirectory)
                                   .Select(x => new Interfaces.Problem(x, $" * {x} - {kvp.Value}"))
                                   .ToArray();

                results.AddRange(matchingFiles);
            }

            if (results.Any()) {
                string header = "Banned files were found. Please check below for details";

                return Interfaces.ConsistencyCheckResult.NewProblemsFound(new Interfaces.ConsistencyCheckProblem(Name, header, results.ToArray()));
            }

            return Interfaces.ConsistencyCheckResult.Passed;
        }
    }
}
