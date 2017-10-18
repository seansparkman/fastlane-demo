#addin "Cake.Xamarin"
#addin "Cake.FileHelpers"
#addin "Cake.AndroidAppManifest"
#addin "Cake.Plist"
#addin "Cake.Raygun"

var assemblyInfoFile = File("./CommonAssemblyInfo.cs");

var solutionFile = File("./HelloWorld.sln");

var appleProjectFile = File("./iOS/HelloWorld.iOS.csproj");
var plistFile = File("./iOS/Info.plist");

var androidProjectFile = File("./Droid/HelloWorld.Droid.csproj");
var manifestFile = File("./Droid/Properties/AndroidManifest.xml");

// should MSBuild treat any errors as warnings.
var treatWarningsAsErrors = "false";

// Parse release notes
var releaseNotes = ParseReleaseNotes("./RELEASENOTES.md");

// Get version
var version = releaseNotes.Version.ToString();
var epoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
var semVersion = string.Format("{0}.{1}", version, epoch);
Task("Package")
    .IsDependentOn("Build")
    .Does (() =>
{
    var androidRelease = "./artifacts/android/release/";
    EnsureDirectoryExists(androidRelease);
    CopyFiles("./Droid/bin/Release/*.apk", androidRelease);

    var androidDebug = "./artifacts/android/debug/";
    EnsureDirectoryExists(androidDebug);
    CopyFiles("./Droid/bin/Debug/*.apk", androidDebug);

    var appleRelease = "./artifacts/apple/release/";
    EnsureDirectoryExists(appleRelease);
    CopyFiles("./iOS/bin/Release/**/*.ipa", appleRelease);
    Zip("./iOS/bin/Release/HelloWorld.iOS.app", appleRelease + semVersion + ".zip");

    // note: Xamarin does not generate .IPA on debug builds.
    var appleDebug = "./artifacts/apple/debug/";
    EnsureDirectoryExists(appleDebug);
    Zip("./iOS/bin/Simulator/HelloWorld.iOS.app", appleDebug + semVersion + ".zip");
});    

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("BuildApple")
    .IsDependentOn("BuildAndroid")
    .Does (() =>
{

});    

Task("Clean")
    .Does (() =>
{
    CleanDirectories("./artifacts/**");
    CleanDirectories("./src/**/bin/**");
});    

Task("BuildApple")
    .IsDependentOn("RestorePackages")
    .IsDependentOn("UpdateAssemblyInfo")
    .IsDependentOn("UpdateApplePlist")
    .Does (() =>
{
    // debug build used for Xamarin UI Test
    MSBuild(appleProjectFile, settings =>
      settings.SetConfiguration("Debug")
          .WithTarget("Build")
          .WithProperty("Platform", "iPhoneSimulator")
          .WithProperty("OutputPath", "bin/Simulator/")
          .WithProperty("BuildIpa", "true")
          .WithProperty("TreatWarningsAsErrors", treatWarningsAsErrors));

    // release build used for app store
    MSBuild(appleProjectFile, settings =>
      settings.SetConfiguration("Release")
          .WithTarget("Build")
          .WithProperty("Platform", "iPhone")
          .WithProperty("BuildIpa", "true")
          .WithProperty("OutputPath", "bin/Release/")
          .WithProperty("TreatWarningsAsErrors", treatWarningsAsErrors));
});

Task("UpdateApplePlist")
    .Does (() =>
{
    dynamic plist = DeserializePlist(plistFile);

    plist["CFBundleShortVersionString"] = version;
    plist["CFBundleVersion"] = semVersion;

    SerializePlist(plistFile, plist);
});

Task("UpdateAssemblyInfo")
    .Does (() =>
{
    CreateAssemblyInfo(assemblyInfoFile, new AssemblyInfoSettings() {
        Product = "Geoffrey Huntley",
        Version = version,
        FileVersion = version,
        InformationalVersion = semVersion,
        Copyright = "Copyright (c) Geoffrey Huntley"
    });
});


Task("BuildAndroid")
    .IsDependentOn("RestorePackages")
    .IsDependentOn("UpdateAssemblyInfo")
    .IsDependentOn("UpdateAndroidManifest")
    .Does (() =>
{
    var keyStore = EnvironmentVariable("ANDROID_KEYSTORE");
    if (string.IsNullOrEmpty(keyStore))
    {
        throw new Exception("The ANDROID_KEYSTORE environment variable is not defined.");
    }

    var keyStoreAlias = EnvironmentVariable("ANDROID_KEYSTORE_ALIAS");
    if (string.IsNullOrEmpty(keyStoreAlias))
    {
        throw new Exception("The ANDROID_KEYSTORE_ALIAS environment variable is not defined.");
    }
    
    var keyStorePassword = EnvironmentVariable("ANDROID_KEYSTORE_PASSWORD");
    if (string.IsNullOrEmpty(keyStorePassword))
    {
        throw new Exception("The ANDROID_KEYSTORE_PASSWORD environment variable is not defined.");
    }

    MSBuild(androidProjectFile, settings =>
        settings.SetConfiguration("Debug")
            .WithTarget("SignAndroidPackage")
            .WithProperty("DebugSymbols", "true")
            .WithProperty("DebugType", "Full")
            .WithProperty("OutputPath", "bin/Debug/")
            .WithProperty("TreatWarningsAsErrors", treatWarningsAsErrors));

    // For more information about MSBuild properties and how they function, read:
    // https://developer.xamarin.com/guides/android/under_the_hood/build_process/
    MSBuild(androidProjectFile, settings =>
        settings.SetConfiguration("Release")
            .WithTarget("SignAndroidPackage")
            .WithProperty("AndroidKeyStore", "true")
            .WithProperty("AndroidSigningStorePass", keyStorePassword)
            .WithProperty("AndroidSigningKeyStore", keyStore)
            .WithProperty("AndroidSigningKeyAlias", keyStoreAlias)
            .WithProperty("AndroidSigningKeyPass", keyStorePassword)
            .WithProperty("DebugSymbols", "false")
            .WithProperty("OutputPath", "bin/Release/")
            .WithProperty("TreatWarningsAsErrors", treatWarningsAsErrors));
});

Task("UpdateAndroidManifest")
    .Does (() =>
{
    var manifest = DeserializeAppManifest(manifestFile);
    manifest.VersionName = semVersion;
    manifest.VersionCode = Int32.Parse(version.Replace(".", string.Empty) + epoch.ToString().Substring(epoch.ToString().Length - 6));

    SerializeAppManifest(manifestFile, manifest);
});

Task("RestorePackages")
    .Does (() =>
{
    NuGetRestore(solutionFile);
});

RunTarget("Package");