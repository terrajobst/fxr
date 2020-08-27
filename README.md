# Roslyn-based metadata analzyer

This repo contains a Roslyn-based metadata analyzer. The root command is `fxr`.

## fxr nullablestats

### Usage

You point the tool at a directory with assemblies you want to analyze, such as

```text
$ ./fxr nullablestats /mystuff -o stats.csv
```

The -o variable is optional. If omitted, it will show the results in Excel.

## fxr platform-compat

This tool produces a list of .NET APIs that are platform specific. It does that
by looking at the `[SupportedOSPlatform]` and `[UnsupportedOSPlatform]`
attributes.

### Usage

You point the tool at the reference assemblies, such as

```text
$ ./fxr platform-compat /runtime/artifacts/bin/ref/net5.0 -o apis.csv
```

The -o variable is optional. If omitted, it will show the results in Excel.

### Report

The resulting CSV/Excel report has the following shape:

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
