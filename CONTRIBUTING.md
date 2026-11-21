# Contributing to UVBake

Thanks for your interest. UVBake is a clean-room C# automatic UV unwrapper and atlas packer. Bug reports, test meshes that break it, and focused
pull requests are all welcome.

## Before you start

- **Bugs:** open an issue with the bug template. A small mesh that reproduces the problem is the most
  useful thing you can attach.
- **Features:** open an issue (or a Discussion) first, so we can agree on the approach before you
  write code.
- **Security problems:** do not open a public issue. See [SECURITY.md](SECURITY.md).

## Build and test

Requires the .NET 8 SDK (`global.json` rolls forward to newer SDKs).

```sh
dotnet build
dotnet test
```

CI runs the same commands on Windows and Linux for every pull request, and both must pass.

## Ground rules

- **Clean-room only.** UVBake reimplements published algorithms. Do not paste or translate code from
  other projects, and never from GPL-licensed ones. Reading papers and permissively-licensed
  reference implementations for understanding is fine; cite them in `NOTICE` or `docs/design/`.
- **Deterministic output.** The same input and options must produce bit-identical output, regardless of
  thread count. Add a test if your change touches anything parallel or randomized.
- **Tests with every change.** Bug fixes come with a regression test; features come with acceptance
  tests.
- **Dependencies.** The core library stays dependency-free or dependency-light. Propose any new
  package in an issue first.
- **Style.** Follow `.editorconfig`; the build treats analyzer warnings seriously.

## Pull requests

1. Fork, then branch from `main`.
2. Keep each PR to one logical change, with a clear description of what and why.
3. Make sure `dotnet test` passes locally.
4. A maintainer reviews every PR before it merges; `main` is protected.

By contributing, you agree that your contributions are licensed under the project's
[MIT License](LICENSE).
