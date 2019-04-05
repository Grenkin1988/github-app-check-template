using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using GitHubApp.Core;

namespace GitHubApp.Checks {
    public abstract class ConsistencyCheckBase {
        private readonly ConsistencyContext _context;

        protected ConsistencyCheckBase(ConsistencyContext context) {
            _context = context;
        }

        public abstract Interfaces.ConsistencyCheckResult Run();

        public abstract string Name { get; }
        public DirectoryInfo RootDirectory => _context.RootDirectory;
        public FileInfo[] AllProjectFiles => _context.AllProjectFiles;
        public FileInfo[] AllFiles => _context.AllFiles;

        protected static XmlNamespaceManager NamespaceManager = CreateNamespaceManager();

        protected string FileWithoutRootDirectory(FileInfo file) {
            return file.FullName.Replace(RootDirectory.FullName, "");
        }

        public static string[] GetResourceContent(string filename) {
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream("Consistency.Checks.IgnoreFiles." + filename)) {
                using (var reader = new StreamReader(stream)) {
                    return reader
                          .ReadToEnd()
                          .Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None)
                          .Where(x => !string.IsNullOrWhiteSpace(x))
                          .ToArray();
                }
            }
        }

        public static string[] GetIgnoredRegexes(string fileName) {
            var text = GetResourceContent(fileName);

            return text
                  .Where(x => !x.StartsWith("#") && !string.IsNullOrWhiteSpace(x))
                  .Select(Regex.Escape)
                  .Select(x => x.Replace("\\*", ".*"))
                  .ToArray();
        }

        public static XmlDocument ReadXmlDocument(FileInfo projectFile) {
            var doc = new XmlDocument();

            using (var reader = projectFile.OpenRead()) {
                doc.Load(reader);
            }

            return doc;
        }

        private static XmlNamespaceManager CreateNamespaceManager() {
            var mgr = new XmlNamespaceManager(new NameTable());
            mgr.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");
            return mgr;
        }
    }
}
