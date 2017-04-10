# Continuous Integration

We use AppVeyor for CI.

AppVeyor automatically builds the __master__ and __stable__ branches, and the stable NuGet package is manually deployed to [nuget.org](https://www.nuget.org/packages/Orleans.Activities).

All the settings are in the appveyor.yml file, only the build _number_ comes from the AppVeyor project settings.

The variables used for versioning in the appveyor.yml file are:
* {productversion} - is read from the `AssemblyInformationalVersion` attribute in GlobalAssemblyInfo.cs file
* {branch}
* {build} - is the build _number_ from the AppVeyor project settings, a continuously incremented number

The appveyor.yml script resets the AppVeyor build _version_ and the attributes in GlobalAssemblyInfo.cs file based on the branch:

||master|stable|
|---|---|---|
|AppVeyor build _version_|{productversion}-{branch}-{build}|{productversion}-{branch}-{build}|
|`AssemblyVersion`|{productversion}|{productversion}|
|`AssemblyFileVersion`|{productversion}|{productversion}|
|`AssemblyInformationalVersion`|{productversion}-{branch}-{build}|{productversion}|
