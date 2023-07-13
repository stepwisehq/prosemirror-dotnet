using System.Collections.Generic;

using Nuke.Common.CI.GitHubActions;
using Nuke.Common.CI.GitHubActions.Configuration;
using Nuke.Common.Execution;
using Nuke.Common.Utilities;

[GitHubActions(
    "nuke-build",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] { "*" },
    PublishArtifacts = false,
    InvokedTargets = new[] { nameof(Test)})
]
partial class Build
{

}