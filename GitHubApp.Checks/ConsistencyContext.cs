using System.IO;
using System.Linq;

namespace GitHubApp.Checks {
    public class ConsistencyContext {
        public DirectoryInfo RootDirectory { get; }

        public FileInfo[] AllProjectFiles { get; }
        public FileInfo[] AllFiles { get; }

        public ConsistencyContext(string repoPath) {
            repoPath = repoPath.EndsWith("\\") ? repoPath : repoPath + "\\";
            RootDirectory = new DirectoryInfo(repoPath);

            var extensions = new[] { "*.csproj", "*.fsproj" };
            AllProjectFiles = extensions.SelectMany(ext => RootDirectory.EnumerateFiles(ext, SearchOption.AllDirectories)).ToArray();
            AllFiles = RootDirectory.EnumerateFiles("*", SearchOption.AllDirectories)
                                    .Where(info => info?.DirectoryName?.Contains(".git") == false)
                                    .OrderBy(info => info?.FullName)
                                    .ToArray();
        }
    }
}
