// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.BuildTools.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
#if SDK
    public class Sdk_GetGitCommitInfo : Task
#elif BuildTools
    public class GetGitCommitInfo : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
    {
        private const string HeadContentStart = "ref: refs/heads/";
        private const string GitDirRefContentStart = "gitdir:";

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
            var repoRoot = GetRepositoryRoot(WorkingDirectory);
            if (repoRoot == null)
            {
                Log.LogError("Could not find the git directory for '{0}'", WorkingDirectory);
                return false;
            }

            RepositoryRootPath = FileHelpers.EnsureTrailingSlash(repoRoot.FullName);

            string gitDir;
            string headFile;
            switch (repoRoot.GetFileSystemInfos(".git").FirstOrDefault())
            {
                case DirectoryInfo d:
                    // regular git working directories
                    gitDir = d.FullName;
                    headFile = Path.Combine(gitDir, "HEAD");
                    break;
                case FileInfo f:
                    // submodules and worktrees
                    var contents = File.ReadAllText(f.FullName);
                    if (contents.StartsWith(GitDirRefContentStart, StringComparison.OrdinalIgnoreCase))
                    {
                        var gitdirRef = contents.Substring(GitDirRefContentStart.Length).Trim();
                        var gitDirRoot = Path.IsPathRooted(gitdirRef)
                            ? new DirectoryInfo(gitdirRef)
                            : new DirectoryInfo(Path.Combine(f.Directory.FullName, gitdirRef));

                        headFile = Path.Combine(gitDirRoot.FullName, "HEAD");

                        var commonDir = gitDirRoot.GetFiles("commondir").FirstOrDefault();
                        if (commonDir != null)
                        {
                            // happens in worktrees
                            var commonDirRef = File.ReadAllText(commonDir.FullName).Trim();
                            gitDir = Path.IsPathRooted(commonDirRef)
                            ? commonDirRef
                            : Path.Combine(gitDirRoot.FullName, commonDirRef);
                        }
                        else
                        {
                            // happens with submodules
                            gitDir = gitDirRoot.FullName;
                        }
                    }
                    else
                    {
                        Log.LogError("Unable to determine the location of the .git directory. Unrecognized file format: {0}", f.FullName);
                        return false;
                    }
                    break;
                case null:
                default:
                    throw new ArgumentOutOfRangeException("Unrecognized implementation of FileSystemInfo");
            }

            if (!File.Exists(headFile))
            {
                Log.LogError("Unable to determine active git branch.");
                return false;
            }

            var content = File.ReadAllText(headFile).Trim();
            if (content.StartsWith(HeadContentStart, StringComparison.OrdinalIgnoreCase))
            {
                return ResolveFromBranch(gitDir, content);
            }
            if (content.Length == CommitShaLength)
            {
                return ResolveFromDetachedHead(content);
            }

            Log.LogError("Unable to determine active git branch. '.git/HEAD' file in unexpected format: '{0}'", content);
            return false;
        }

        private bool ResolveFromBranch(string gitDir, string head)
        {
            Branch = head.Substring(HeadContentStart.Length);

            if (string.IsNullOrEmpty(Branch))
            {
                Log.LogMessage("Current branch appears to be empty. Failed to retrieve current branch.");
            }

            var branchFile = Path.Combine(gitDir, "refs", "heads", Branch);
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

        private static DirectoryInfo GetRepositoryRoot(string start)
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var dotGit = dir.GetFileSystemInfos(".git").FirstOrDefault();
                if (dotGit != null)
                {
                    return dir;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
