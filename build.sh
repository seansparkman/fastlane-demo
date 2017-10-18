#!/bin/sh

# GENERATE KEYSTORE
#  keytool -genkeypair -v -keystore ./cert.keystore -alias com.seansparkman.HelloWorld -keyalg RSA -keysize 2048 -validity 10000 -keypass PASSWORD -storepass PASSWORD -dname "CN=Sean Sparkman, OU=Mobile, L=Dallas, S=TX, C=USA"

mono tools/nuget/nuget.exe update -self
mono tools/nuget/nuget.exe install xunit.runner.console -OutputDirectory tools -ExcludeVersion
mono tools/nuget/nuget.exe install Cake -OutputDirectory tools -ExcludeVersion

export ANDROID_KEYSTORE=../cert.keystore
export ANDROID_KEYSTORE_ALIAS=com.seansparkman.HelloWorld
export ANDROID_KEYSTORE_PASSWORD=PASSWORD

mono tools/Cake/Cake.exe build.cake "--settings_skipverification=true"