using Nuke.Common.CI.GitHubActions;


[GitHubActions(
    "nuke-build",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranchesIgnore = new[] { "main", "release/*", "feature/*" },
    PublishArtifacts = false,
    FetchDepth = 0,
    InvokedTargets = new[] { nameof(Test)})
]
[GitHubActions(
    "nuke-publish",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] { "main", "release/*", "feature/*" },
    PublishArtifacts = true,
    FetchDepth = 0,
    ImportSecrets = new[] { nameof(NugetApiPubKey), nameof(NugetApiIntKey), nameof(DryRun) },
    InvokedTargets = new[] { nameof(Push)})
]
partial class Build {}