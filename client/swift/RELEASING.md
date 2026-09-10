# Publishing NonaClient to CocoaPods

A personal Mac is not required. The `Swift CocoaPods release` GitHub Actions
workflow runs on a hosted macOS runner with Xcode. It is manual and defaults to
validation only; pushing a tag does not publish a pod.

## One-time setup

1. Merge `.github/workflows/swift-release.yml` and the reusable Swift CI workflow
   into the repository's default branch so GitHub displays **Run workflow**.
2. Register a CocoaPods Trunk account and confirm its email. Obtain a valid Trunk
   session token for CI. The account must own `NonaClient` if it already exists;
   for a first publication the name must be available.
3. In GitHub, open **Settings → Secrets and variables → Actions → New repository
   secret**. Set the name to `COCOAPODS_TRUNK_TOKEN` and the value to that token.
   Do not commit the token or paste it into workflow inputs, issues or logs.
4. Restrict release tag creation to maintainers and prevent release tag updates
   and deletion using repository tag rulesets. Consumers resolve the tag from
   GitHub, so published tags must remain immutable.

The token is exposed only to the publishing step. The tests and dry run do not
receive it. No paid Apple Developer membership or app signing certificate is
needed for this workflow.

## Release a version

1. Update `s.version` in `NonaClient.podspec`, merge the release changes and wait
   for CI. Use a stable version such as `0.1.0`, without a `v` prefix.
2. Create and push that exact tag on the intended release commit. For example,
   after checking out the correct commit:

   ```sh
   git tag -a 0.1.0 -m "Nona Swift SDK 0.1.0"
   git push origin 0.1.0
   ```

   Alternatively, create the tag through GitHub's release UI, explicitly choosing
   the intended commit. The workflow requires an existing tag and never creates
   or moves one.
3. Open **Actions → Swift CocoaPods release → Run workflow**. Select the trusted
   default branch for the workflow, enter `0.1.0` as `version`, and leave
   `publish` unchecked. The workflow resolves the tag to a commit and runs:
   - shared QA checks, Swift unit tests and strict concurrency builds;
   - iOS simulator tests against a disposable backend;
   - the CocoaPods sample build;
   - podspec identity/version/source checks and `pod spec lint` against the remote
     tag for iOS and macOS, with Swift language versions 5.9 and 6.0.
4. After a successful dry run, run it again with the same version and `publish`
   checked. It repeats all checks and calls `pod trunk push NonaClient.podspec`
   only after they succeed. A tag change detected before publishing fails the run.
5. Check the successful publication step. Verify installation in a clean consumer
   project, allowing time for registry/CDN propagation:

   ```ruby
   pod 'NonaClient', '~> 0.1.0'
   ```

   Run `pod install --repo-update`, open the `.xcworkspace`, and check
   `import NonaClient` and a request to your test backend. Publish the installation
   instructions after this check.

## Failed runs and later releases

Validation failures prevent publication. Fix the cause before retrying. If the
publishing step times out or loses its connection, check `pod trunk info NonaClient`
before retrying: the server may already have accepted the version. The workflow
does not silently skip an existing version or overwrite it. Publish fixes under
a new version/tag; never move an already published tag.

Dry runs still need a public, reachable release tag because `pod spec lint`
downloads the source specified by the podspec. A local-only tag is insufficient.

CocoaPods has announced that Trunk will become read-only on December 2, 2026.
This publication path depends on Trunk continuing to accept releases. Maintain
Swift Package Manager support for releases after that transition and check the
official announcement before scheduling a release.

References:
- [CocoaPods Trunk account setup](https://guides.cocoapods.org/making/getting-setup-with-trunk.html)
- [CocoaPods commands](https://guides.cocoapods.org/terminal/commands.html)
- [Trunk token environment variable](https://github.com/CocoaPods/cocoapods-trunk/blob/master/lib/pod/command/trunk.rb)
- [CocoaPods read-only announcement](https://blog.cocoapods.org/CocoaPods-Specs-Repo/)
