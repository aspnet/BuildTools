// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.AspNetCore.BuildTools.Utilities
{
    internal class GitRepoInfo
    {
        private const string HeadContentStart = "ref: refs/heads/";
        private const string GitDirRefContentStart = "gitdir:";

        private const int CommitShaLength = 40;

        /// <summary>
        /// The name of the branch HEAD is pointed to. Can be null or empty
        /// for repositories in detached HEAD mode.
        /// </summary>
        public string Branch { get; set; }

        /// <summary>
        /// The full commit SHA of the current commit referenced by HEAD.
        /// </summary>
        public string CommitHash { get; private set; }

        /// <summary>
        /// The root folder of the working directory. Not the .git folder itself.
        /// </summary>
        public string RepositoryRootPath { get; private set; }

        /// <summary>
        /// The folder containing the .git folder
        /// </summary>
        public string GitDir { get; private set; }

        /// <summary>
        /// The repo is in detached head mode.
        /// </summary>
        public bool DetachedHeadMode { get; private set; }

        /// <summary>
        /// Full path to the HEAD file.
        /// </summary>
        public string HeadFile { get; set; }

        /// <summary>
        /// Find give info for a folder inside the git project. Does not need to be the top folder.
        /// This task will search upwards for the .git folder.
        /// </summary>
        public static GitRepoInfo Load(string workingDirectory)
        {
            var repoRoot = GetRepositoryRoot(workingDirectory);
            if (repoRoot == null)
            {
                throw new DirectoryNotFoundException($"Could not find the git directory for '{workingDirectory}'");
            }

            var info = new GitRepoInfo
            {
                RepositoryRootPath = FileHelpers.EnsureTrailingSlash(repoRoot.FullName)
            };

            switch (repoRoot.GetFileSystemInfos(".git").FirstOrDefault())
            {
                case DirectoryInfo d:
                    // regular git working directories
                    info.GitDir = d.FullName;
                    info.HeadFile = Path.Combine(info.GitDir, "HEAD");
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

                        info.HeadFile = Path.Combine(gitDirRoot.FullName, "HEAD");

                        var commonDir = gitDirRoot.GetFiles("commondir").FirstOrDefault();
                        if (commonDir != null)
                        {
                            // happens in worktrees
                            var commonDirRef = File.ReadAllText(commonDir.FullName).Trim();
                            info.GitDir = Path.IsPathRooted(commonDirRef)
                            ? commonDirRef
                            : Path.Combine(gitDirRoot.FullName, commonDirRef);
                        }
                        else
                        {
                            // happens with submodules
                            info.GitDir = gitDirRoot.FullName;
                        }
                    }
                    else
                    {
                        throw new DirectoryNotFoundException($"Unable to determine the location of the .git directory. Unrecognized file format: {f.FullName}");
                    }
                    break;
                case null:
                default:
                    throw new ArgumentOutOfRangeException("Unrecognized implementation of FileSystemInfo");
            }

            if (!File.Exists(info.HeadFile))
            {
                throw new FileNotFoundException("Unable to determine the status of the git repo. No HEAD file found.");
            }

            var content = File.ReadAllText(info.HeadFile).Trim();
            if (content.StartsWith(HeadContentStart, StringComparison.OrdinalIgnoreCase))
            {
                info.Branch = content.Substring(HeadContentStart.Length);
                info.CommitHash = ResolveHashFromBranch(info.GitDir, info.Branch);
            }
            else if (content.Length == CommitShaLength)
            {
                info.DetachedHeadMode = true;
                info.CommitHash = content;
            }
            else
            {
                throw new InvalidOperationException($"Unable to determine the status of the git repo. THe HEAD file has an unexpected format: '{content}'");
            }

            return info;
        }

        private static string ResolveHashFromBranch(string gitDir, string branch)
        {
            if (string.IsNullOrEmpty(branch))
            {
                throw new InvalidOperationException("Current branch appears to be empty. Failed to retrieve current branch.");
            }

            var branchFile = Path.Combine(gitDir, "refs", "heads", branch);
            if (!File.Exists(branchFile))
            {
                throw new FileNotFoundException("Unable to determine current git commit hash");
            }

            return File.ReadAllText(branchFile).Trim();
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
