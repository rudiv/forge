# forge
Forge is an alternative deployment tool for [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview).

## Reasons

azd is great but contains a lot of non-customisable magic, such as creating its own Container Registry and magical environments that aren't controllable in any way. It also only supports ACA as a deployment target.

There's also no option for creating different environments (dev/test) and having different configurations for each.

This tool will create all of the Bicep necessary for creating the entire environment as well as mimicking the `azd up|deploy|provision` commands in something that's repeatable without magic, after reading the manifest.

## Proposed usage

- `forge temper [-env {dev}]` - Generate the relevant configuration files
- `forge ignite [-env {dev}]` - Deploy

- ## Goals

- [ ] Create reusable Bicep files
- [ ] Support multiple environments for deployment that can be committed to CI
- [ ] Support ACA deployment target
- [ ] Support AppService deployment target
