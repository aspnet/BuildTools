// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    public class GetGitCommitInfo : Task
    {
        /// <summary>
        /// A folder inside the git project. Does not need to be the top folder.
        /// This task will search upwards for the .git folder.
        /// </summary>
        [Required]
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// The name of the branch HEAD is pointed to. Can be null or empty
        /// for repositories in detached HEAD mode.
        /// </summary>
        [Output]
        public string Branch { get; set; }

        /// <summary>
        /// The full commit SHA of the current commit referenced by HEAD.
        /// </summary>
        [Output]
        public string CommitHash { get; set; }

        /// <summary>
        /// The folder containing the '.git', not the .git folder itself.
        /// </summary>
        [Output]
        public string RepositoryRootPath { get; set; }

        public override bool Execute()
        {
            RepositoryRootPath = GetRepositoryRoot(WorkingDirectory);
            if (RepositoryRootPath == null)
            {
                Log.LogError("Could not find the git directory for '{0}'", WorkingDirectory);
                return false;
            }

            var headFile = Path.Combine(RepositoryRootPath, ".git", "HEAD");
            if (!File.Exists(headFile))
            {
                Log.LogError("Unable to determine active git branch.");
                return false;
            }

            var content = File.ReadAllText(headFile).Trim();
            const string HeadContentStart = "ref: refs/heads/";
            if (!content.StartsWith(HeadContentStart))
            {
                Log.LogError("Unable to determine active git branch. '.git/HEAD' file in unexpected format: '{0}'", content);
                return false;
            }

            Branch = content.Substring(HeadContentStart.Length);

            if (string.IsNullOrEmpty(Branch))
            {
                Log.LogMessage("Current branch appears to be empty. Failed to retrieve current branch.");
            }

            var branchFile = Path.Combine(RepositoryRootPath, ".git", "refs", "heads", Branch);
            if (!File.Exists(branchFile))
            {
                Log.LogError("Unable to determine current git commit hash");
                return false;
            }

            CommitHash = File.ReadAllText(branchFile).Trim();
            return true;
        }

        private static string GetRepositoryRoot(string start)
        {
            var dir = start;
            while (dir != null)
            {
                var gitDir = Path.Combine(dir, ".git");
                if (Directory.Exists(gitDir))
                {
                    return dir;
                }

                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
