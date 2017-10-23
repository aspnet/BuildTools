// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.BuildTools.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
#if SDK
    public class Sdk_GetGitCommitHash : Task
#elif BuildTools
    public class GetGitCommitHash : Task
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
        /// The full commit SHA of the current commit referenced by HEAD.
        /// </summary>
        [Output]
        public string CommitHash { get; set; }

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

                if (string.IsNullOrEmpty(repoInfo.CommitHash))
                {
                    Log.LogError("Could not determine the commit hash of the current git repo in '{0}'.", repoInfo.RepositoryRootPath);
                    return false;
                }

                CommitHash = repoInfo.CommitHash;
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
