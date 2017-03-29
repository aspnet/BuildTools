// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.AspNetCore.BuildTools.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    public class GetGitCommitInfo : Task
    {
        private const string HeadContentStart = "ref: refs/heads/";
        private const int CommitShaLength = 40;

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
            RepositoryRootPath = FileHelpers.EnsureTrailingSlash(GetRepositoryRoot(WorkingDirectory));
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
            if (content.StartsWith(HeadContentStart))
            {
                return ResolveFromBranch(content);
            }
            else if (content.Length == CommitShaLength)
            {
                return ResolveFromDetachedHead(content);
            }

            Log.LogError("Unable to determine active git branch. '.git/HEAD' file in unexpected format: '{0}'", content);
            return false;
        }

        private bool ResolveFromBranch(string head)
        {
            Branch = head.Substring(HeadContentStart.Length);

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

        private bool ResolveFromDetachedHead(string head)
        {
            CommitHash = head;
            Log.LogWarning("The repo in '{0}' appears to be in detached HEAD mode. Unable to determine current git branch.", RepositoryRootPath);
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
