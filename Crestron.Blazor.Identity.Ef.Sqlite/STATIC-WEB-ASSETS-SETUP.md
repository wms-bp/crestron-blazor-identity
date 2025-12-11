# Static Web Assets Configuration for Crestron Processors

This guide documents the exact steps required to configure static web assets (especially NuGet package `_content` folders) to work correctly on Crestron processors.

## Problem

When building ASP.NET Core Blazor applications for Crestron processors, static web assets from NuGet packages (like Blazor.Bootstrap) stored in `_content` folders fail to resolve properly. This is because:

1. Crestron builds use `OutputType=Library` instead of `Exe`
2. Static web assets are not automatically copied for Library output types
3. The static web assets manifest needs to be loaded at runtime

## Solution Overview

The solution requires three key components:
1. MSBuild configuration to force static web asset copying
2. Custom MSBuild target to copy assets with proper `_content` structure
3. Runtime configuration to load the static web assets manifest

---

## Step 1: Configure .csproj File

Add these properties to your `.csproj` file:

### Base Static Web Assets Properties (for all configurations)
```xml
<PropertyGroup>
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>
    <GenerateStaticWebAssetsManifest>true</GenerateStaticWebAssetsManifest>
    <EnableDefaultContentItems>true</EnableDefaultContentItems>
</PropertyGroup>
```

### Library OutputType Configuration (if applicable)

If your Crestron project uses `OutputType=Library`, add these properties:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <OutputType>Library</OutputType>

    <!-- Force static web assets to be copied even for Library output type -->
    <StaticWebAssetsEnabled>true</StaticWebAssetsEnabled>
    <IncludeStaticWebAssetsInPublish>true</IncludeStaticWebAssetsInPublish>
    <CopyStaticWebAssetsToOutput>true</CopyStaticWebAssetsToOutput>
</PropertyGroup>
```

> **Note:** If you use custom configurations like `CRESTRONX86` or `CRESTRONARM`, apply these properties to those configurations instead.

### Ensure wwwroot Files Are Copied
```xml
<ItemGroup>
    <Content Update="wwwroot\**\*">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

---

## Step 2: Add Custom MSBuild Target

Add this MSBuild target to copy static web assets with the correct `_content` structure:

```xml
<!-- Custom target to copy static web assets -->
<!-- Must run AFTER static web assets are resolved but BEFORE final build -->
<Target Name="CopyStaticWebAssetsForCrestron"
        AfterTargets="ResolveStaticWebAssetsInputs;GenerateStaticWebAssetsManifest"
        BeforeTargets="CoreBuild">

    <!-- Get all static web assets from the manifest -->
    <ItemGroup>
        <StaticWebAssetsToPublish Include="@(StaticWebAsset)"
                                   Condition="'%(StaticWebAsset.AssetKind)' == 'All' OR '%(StaticWebAsset.AssetKind)' == 'Publish'" />
    </ItemGroup>

    <!-- Copy each static web asset to the output directory with proper _content structure -->
    <Copy SourceFiles="%(StaticWebAssetsToPublish.Identity)"
          DestinationFiles="$(OutputPath)wwwroot\%(StaticWebAssetsToPublish.RelativePath)"
          SkipUnchangedFiles="true"
          Condition="'%(StaticWebAssetsToPublish.SourceType)' != 'Package'" />

    <!-- Copy package assets to _content directory structure -->
    <Copy SourceFiles="%(StaticWebAssetsToPublish.Identity)"
          DestinationFiles="$(OutputPath)wwwroot\_content\%(StaticWebAssetsToPublish.SourceId)\%(StaticWebAssetsToPublish.RelativePath)"
          SkipUnchangedFiles="true"
          Condition="'%(StaticWebAssetsToPublish.SourceType)' == 'Package'" />

    <Message Text="Copied static web asset: %(StaticWebAssetsToPublish.RelativePath) from %(StaticWebAssetsToPublish.SourceType)"
             Importance="high" />
</Target>
```

**Key Points:**
- Runs after `ResolveStaticWebAssetsInputs` and `GenerateStaticWebAssetsManifest` to ensure assets are discovered
- Runs before `CoreBuild` to ensure files are in place before final output
- Non-package assets go to `wwwroot/[RelativePath]`
- Package assets go to `wwwroot/_content/[PackageName]/[RelativePath]`
- Remove the `Condition` attribute or customize it for your build configurations

---

## Step 3: Configure Runtime

Add these using statements to your startup class:
```csharp
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.FileProviders;
```

### After creating WebApplicationBuilder:

```csharp
var builder = WebApplication.CreateBuilder();

// Load static web assets manifest
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
```

### Configure middleware pipeline:

```csharp
// ... after app is built ...

// Get the application's base directory and wwwroot path
var baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
var wwwrootPath = Path.Combine(baseDir ?? "", "wwwroot");

// First, add the physical wwwroot directory
if (Directory.Exists(wwwrootPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(wwwrootPath),
        RequestPath = ""
    });
}

// Then add the default static files middleware to handle _content and other virtual paths
app.UseStaticFiles();

app.UseRouting();
// ... rest of configuration ...
```

**Important:**
- Call `UseStaticFiles()` twice - once with explicit physical provider, once for default handling
- The default `UseStaticFiles()` call uses the static web assets manifest loaded earlier
- This allows both physical files and virtual `_content` paths to resolve correctly

---

## Step 4: Verify Build Output

After building, verify the output directory structure:

```
bin/Release/net8.0/
├── wwwroot/
│   ├── _content/
│   │   ├── Blazor.Bootstrap/      <- NuGet package assets
│   │   │   ├── blazor.bootstrap.css
│   │   │   ├── blazor.bootstrap.js
│   │   │   └── ... other files
│   │   └── [OtherPackages]/
│   ├── css/                        <- Your project files
│   ├── js/
│   └── ... other static files
└── YourProject.dll (or .cpz for Crestron)
```

---

## Complete Checklist

- [ ] Add base static web assets properties to `.csproj`
- [ ] Add Library output type properties (if applicable)
- [ ] Add `wwwroot` content copy configuration
- [ ] Add `CopyStaticWebAssetsForCrestron` MSBuild target
- [ ] Add `using Microsoft.AspNetCore.Hosting.StaticWebAssets;` to startup class
- [ ] Add `using Microsoft.Extensions.FileProviders;` to startup class
- [ ] Call `StaticWebAssetsLoader.UseStaticWebAssets()` after creating builder
- [ ] Configure dual `UseStaticFiles()` calls in middleware pipeline
- [ ] Build and verify `_content` folder structure in output
- [ ] Test that NuGet package assets load correctly in browser (check Network tab for 404s)

---

## Troubleshooting

### _content files return 404
- Verify `StaticWebAssetsLoader.UseStaticWebAssets()` is called before building the app
- Verify both `UseStaticFiles()` calls are present in middleware pipeline
- Check build output for `_content` folder structure

### Build doesn't copy _content files
- Ensure MSBuild target runs: check build output for "Copied static web asset" messages
- Verify `StaticWebAssetsEnabled`, `IncludeStaticWebAssetsInPublish`, and `CopyStaticWebAssetsToOutput` are `true`
- Check that target runs `AfterTargets="ResolveStaticWebAssetsInputs;GenerateStaticWebAssetsManifest"`
- If using Library OutputType, ensure static web asset properties are set for your configuration

### Files copied but still not serving
- Verify physical wwwroot path is correct in middleware setup
- Check that `Directory.Exists(wwwrootPath)` returns true
- Ensure middleware order: static files before routing
- Confirm base directory path resolves correctly for your deployment scenario
