using System.Collections.Generic;

using Nuke.Common.CI.GitHubActions;
using Nuke.Common.CI.GitHubActions.Configuration;
using Nuke.Common.Execution;
using Nuke.Common.Utilities;

[GitHubActions(
    "nuke-build",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranchesIgnore = new[] { "main", "release/*" },
    PublishArtifacts = false,
    FetchDepth = 0,
    InvokedTargets = new[] { nameof(Test)})
]
[GitHubActions(
    "nuke-publish",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] { "main", "release/*" },
    PublishArtifacts = true,
    FetchDepth = 0,
    ImportSecrets = new[] { nameof(NugetApiKey), nameof(NugetApiIntKey) },
    InvokedTargets = new[] { nameof(Push)})
]
partial class Build
{

}