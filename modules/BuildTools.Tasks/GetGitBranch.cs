// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.BuildTools.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
#if SDK
    public class Sdk_GetGitBranch : Task
#elif BuildTools
    public class GetGitBranch : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
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

        public override bool Execute()
        {
            try
            {
                var repoInfo = GitRepoInfo.Load(WorkingDirectory);
                Log.LogMessage(MessageImportance.Low, "Resolved git repo info to: RootPath = {0}, GitDir = {1}, HEAD = {2}, Branch = {3}, Hash = {4}",
                    repoInfo.RepositoryRootPath,
                    repoInfo.GitDir,
                    repoInfo.HeadFile,
                    repoInfo.Branch,
                    repoInfo.CommitHash);

                if (repoInfo.DetachedHeadMode)
                {
                    Log.LogError("The current git repo is in detached HEAD mode. It is not possible to determine the branch name.");
                    return false;
                }

                if (string.IsNullOrEmpty(repoInfo.Branch))
                {
                    Log.LogError("Could not determine the branch name of the current git repo in '{0}'.", repoInfo.RepositoryRootPath);
                    return false;
                }

                Branch = repoInfo.Branch;
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
                return false;
            }
        }
    }
}
