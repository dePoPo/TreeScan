# Working on TreeScan

TreeScan has two Git repositories. The internal working copy uses GitLab as its `origin` remote. The separate `../TreeScan-public` working copy uses the `github` remote for `dePoPo/TreeScan`. Both use the `master` branch.

## Commits and pushes

- Commit the same public changes in both working copies. Make a separate commit on the existing public history in the public working copy.
- When asked to push, push both working copies. Use `git push origin master` internally and `git push github master` from the public working copy. Verify that both remotes received the intended commits.
- Review the files and metadata before every public commit. Keep internal URLs, references, secrets, and local build outputs out of the public copy. Ask if it is unclear whether a change may be shared publicly.
- Keep the two histories separate. Add the public remote only to the public working copy. Resolve differences without force-pushing or mirror-pushing.
- Tell the user if a commit or push to either repository is blocked, for example when sign-in is required.
