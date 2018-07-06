#!/usr/bin/env pwsh -c

<#
.DESCRIPTION
Creates a GitHub pull request to merge a head branch into a base branch
.PARAMETER RepoOwner
The GitHub repository owner.
.PARAMETER RepoName
The GitHub repository name.
.PARAMETER BaseBranch
The base branch -- the target branch for the PR
.PARAMETER HeadBranch
The current branch
.PARAMETER Username
The GitHub username
.PARAMETER AuthToken
A personal access token
.PARAMETER Fork
Make PR from a fork
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Alias('o')]
    [Parameter(Mandatory = $true)]
    $RepoOwner,

    [Alias('n')]
    [Parameter(Mandatory = $true)]
    $RepoName,

    [Alias('b')]
    [Parameter(Mandatory = $true)]
    $BaseBranch,

    [Alias('h')]
    [Parameter(Mandatory = $true)]
    $HeadBranch,

    [Parameter(Mandatory = $true)]
    [Alias('a')]
    $AuthToken,

    [Alias('u')]
    $Username,

    [switch]$Fork
)

$ErrorActionPreference = 'stop'
Set-StrictMode -Version 1

if ($Fork -and (-not $Username)) {
    throw 'You must specify -Username if you also specify -Fork'
}

$headers = @{
    Authorization = "bearer $AuthToken"
}

[hashtable] $script:emails = @{}

function Invoke-Block([scriptblock]$cmd) {
    $cmd | Out-String | Write-Verbose
    & $cmd

    # Need to check both of these cases for errors as they represent different items
    # - $?: did the powershell script block throw an error
    # - $lastexitcode: did a windows command executed by the script block end in error
    if ((-not $?) -or ($lastexitcode -ne 0)) {
        if ($error -ne $null) {
            Write-Warning $error[0]
        }
        throw "Command failed to execute: $cmd"
    }
}

function GetCommiterGitHubName($sha) {
    $email = & git show -s --format='%ce' $sha
    $key = 'committer'

    if ((-not $email) -or ($email -eq 'noreply@github.com')) {
        $key = 'author'
        $email = & git show -s --format='%ae' $sha
    }

    if ($email -like '*@users.noreply.github.com') {
        return $email -replace '@users.noreply.github.com', ''
    }
    elseif ($script:emails[$email]) {
        return $script:emails[$email]
    }
    else {
        Write-Verbose "Attempting to find GitHub username for $email"
        try {
            $resp = Invoke-RestMethod -Method GET -Headers $headers `
                "https://api.github.com/repos/$RepoOwner/$RepoName/commits/$sha"
            $resp | Write-Verbose
            if ($resp -and $resp.$key) {
                $script:emails[$email] = $resp.committer.login
            }
            return $resp.$key.login
        }
        catch {
            Write-Warning "Failed to find github user for $committerEmail. $_"
        }
    }
}

$workDir = "$PSScriptRoot/obj/$repoName"
New-Item "$PSScriptRoot/obj/" -ItemType Directory -ErrorAction Ignore | Out-Null

$fetch = $true
if (-not (Test-Path $workDir)) {
    $fetch = $false
    Invoke-Block { & git clone "https://github.com/$RepoOwner/$RepoName.git" $workDir `
            --quiet `
            --no-tags `
            --branch $HeadBranch
    }
}

# see https://git-scm.com/docs/pretty-formats
$formatString = '%h %cn <%ce>: %s (%cr)'

Push-Location $workDir
try {
    if ($fetch) {
        Invoke-Block { & git fetch --quiet }
        Invoke-Block { & git checkout --quiet $HeadBranch }
        Invoke-Block { & git reset --hard origin/$HeadBranch }
    }

    Write-Host -f Magenta "${HeadBranch}:`t`t$(& git log --format=$formatString -1 HEAD)"

    Invoke-Block { & git checkout --quiet $BaseBranch }
    Invoke-Block { & git reset --quiet --hard origin/$BaseBranch }

    Write-Host -f Magenta "${BaseBranch}:`t$(& git log --format=$formatString -1 HEAD)"

    [string[]] $commitsToMerge = & git rev-list "$BaseBranch..$HeadBranch" # find all commits which will be merged

    if (-not $commitsToMerge) {
        Write-Warning "There were no commits to be merged from $HeadBranch into $BaseBranch"
        return 0
    }

    $authors = $commitsToMerge `
        | % { Write-Host -f Cyan "Merging:`t$(git log --format=$formatString -1 $_)"; $_ } `
        | % { GetCommiterGitHubName $_ } `
        | ? { $_ -ne $null } `
        | Get-Unique

    $list = $authors | % { "* @$_" }
    $prMessage = "This PR merges commits made on $HeadBranch by the following committers:`n`n$($list -join "`n")"

    Write-Host $prMessage

    $mergeBranchName = "automerge/$HeadBranch"
    Invoke-Block { & git checkout -B $mergeBranchName  }

    try {
        Invoke-Block { & git merge $HeadBranch `
                -m "[automated] Merge branch '$HeadBranch'" `
                --no-ff }
    }
    catch {
        # fallback if the automatic merge produces conflicts
        Write-Warning "Could not automatically merge, but proceeding to open PR anyways"
    }

    $remoteName = 'origin'
    $prOwnerName = 'aspnet'

    if ($Fork) {
        $remoteName = 'fork'
        Invoke-Block { & git remote add -f fork "https://${Username}:${AuthToken}@github.com/${Username}/${RepoName}.git" }
        $prOwnerName = $Username
    }

    if ($PSCmdlet.ShouldProcess("Update remote branch $mergeBranchName on $remoteName")) {
        Invoke-Block { & git push --force $remoteName "${mergeBranchName}:${mergeBranchName}" }
    }

    $query = 'query ($repoName: String!, $baseName: String!) {
        repository(owner: "aspnet", name: $repoName) {
          pullRequests(baseRefName: $baseName, states: OPEN, first: 100) {
            totalCount
            nodes {
              number
              headRef {
                name
                repository {
                  name
                  owner {
                    login
                  }
                }
              }
            }
          }
        }
      }'

    $data = @{
        query     = $query
        variables = @{
            repoName    = $RepoName
            baseRefName = $BaseBranch
        }
    }

    $resp = Invoke-RestMethod -Method Post `
        -Headers $headers `
        https://api.github.com/graphql `
        -Body ($data | ConvertTo-Json)
    $resp | Write-Verbose

    $matchingPr = $resp.data.repository.pullRequests.nodes `
        | ? { $_.headRef.name -eq $mergeBranchName -and $_.headRef.repository.owner.login -eq $prOwnerName } `
        | select -First 1

    if ($matchingPr) {
        $data = @{
            body = "This pull request has been updated.`n`n$prMessage"
        }

        $prNumber = $matchingPr.number
        $prUrl = "https://github.com/aspnet/$RepoName/pull/$prNumber"

        if ($PSCmdlet.ShouldProcess("Update $prUrl")) {
            $resp = Invoke-RestMethod -Method Post -Headers $headers `
                "https://api.github.com/repos/aspnet/$RepoName/issues/$prNumber/comments" `
                -Body ($data | ConvertTo-Json)
            $resp | Write-Verbose
            Write-Host -f green "Updated pull request $url"
        }
    }
    else {
        $previewHeaders = @{
            #  Required while this api is in preview: https://developer.github.com/v3/pulls/#create-a-pull-request
            Accept        = 'application/vnd.github.symmetra-preview+json'
            Authorization = "bearer $AuthToken"
        }

        $data = @{
            title                 = "[automated] Merge branch '$HeadBranch' => '$BaseBranch'"
            head                  = "${prOwnerName}:${mergeBranchName}"
            base                  = $BaseBranch
            body                  = $prMessage
            maintainer_can_modify = $true
        }

        if ($PSCmdlet.ShouldProcess("Create PR from ${prOwnerName}:${mergeBranchName} to $BaseBranch on $Reponame")) {
            $resp = Invoke-RestMethod -Method POST -Headers $previewHeaders `
                https://api.github.com/repos/aspnet/$RepoName/pulls `
                -Body ($data | ConvertTo-Json)
            $resp | Write-Verbose
            Write-Host -f green "Created pull request https://github.com/aspnet/$RepoName/pull/$($resp.number)"
        }
    }
}
finally {
    Pop-Location
}
