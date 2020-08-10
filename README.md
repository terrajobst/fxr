# HarvestPlatformSupport

This tool produces a list of .NET APIs that are platform specific. It does that
by looking at the `[SupportedOSPlatform]` and `[UnsupportedOSPlatform]`
attributes.

## Usage

You point the tool at the reference assemblies, such as

```text
$ ./fxp /runtime/artifacts/bin/ref/net5.0 -o apis.csv
```

The -o variable is optional. If omitted, it will show the results in Excel.

## Report

See [apis.csv](apis.csv) for the current state.

Column    | Description
----------|--------------------------------------------------------------
Level     | The type of API that was annotated (`Assembly`, `Type`, or `Member`)
Assembly  | The assembly name of the annotated API
Namespace | The namespace of the annotated API
Type      | The type name of the annotated API
Member    | The member name of the annotated API
Kind      | Indicates the type of annotation that was used: `platform-specific` means only specific OS are supported, `platform-restricted` means the API works on all platforms except the ones listed.
Implicit  | Indicates whether the API was directly annotated (`No`) or whether its containing type or assembly was annotated (`Yes`).
`<OS>`    | Indicates which OS versions the API is support on.

## Differences from spec

These annotations were mentioned in [the spec] but not present in .NET 5:

* System.Security.Cryptography
    - CspParameters
* System.ServiceModel
* System.ServiceModel.Channels
* System.ServiceModel.Security

[the spec]: https://github.com/dotnet/designs/blob/master/accepted/2020/windows-specific-apis/windows-specific-apis.md