using System.Collections.Generic;

using Nuke.Common.CI.GitHubActions;
using Nuke.Common.CI.GitHubActions.Configuration;
using Nuke.Common.Execution;
using Nuke.Common.Utilities;

[GitHubActions(
    "nuke-build",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] { "*" },
    OnPushTags = new[] { "!v*" },
    PublishArtifacts = false,
    FetchDepth = 0,
    InvokedTargets = new[] { nameof(Test)})
]
[GitHubActions(
    "nuke-publish",
    GitHubActionsImage.UbuntuLatest,
    OnPushTags = new[] { "v*" },
    PublishArtifacts = false,
    FetchDepth = 0,
    ImportSecrets = new[] { nameof(NugetApiKey), nameof(NugetApiIntKey) },
    InvokedTargets = new[] { nameof(Push)})
]
partial class Build
{

}