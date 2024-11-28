# forge
Forge is an alternative deployment tool for [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview).

_It also fixes `dotnet watch` on Aspire 9 projects until the a newer .NET SDK is out._

## Getting Started

Install the tool.
```
dotnet tool install --global R.Forge --prerelease
```

Watch your AppHost project. Note that `--no-hot-reload` is recommended for your projects so that new files
are generally picked up better, also .NET 9's dotnet watch is pretty broken.
```
forge fire --no-hot-reload
```

## Reasons

azd is great but contains a lot of non-customisable magic, such as creating its own Container Registry and magical environments that aren't controllable in any way. It also only supports ACA as a deployment target.

There's also no option for creating different environments (dev/test) and having different configurations for each.

This tool will create all of the Bicep necessary for creating the entire environment as well as mimicking the `azd up|deploy|provision` commands in something that's repeatable without magic, after reading the manifest.

## Proposed usage

- `forge fire|watch [--no-hot-reload] [--apphost-build-output]` - Fires up the app for local development
- `forge temper|prepare [-env {dev}]` - Generate the relevant configuration files
- `forge forge|deploy [-env {dev}]` - Meta af. Deploy to the cloud!

## Goals

- [ ] Create reusable Bicep files
- [ ] Support multiple environments for deployment that can be committed to CI
- [ ] Support ACA deployment target
- [ ] Support AppService deployment target
- [x] Add dotnet watch support back to individual projects

## Raising issues

The environment variable `FORGE_DEBUG=1` will enable logging, if you're posting an issue please make sure that you post the complete output (redacted project names is fine).
