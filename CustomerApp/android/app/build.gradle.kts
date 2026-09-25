plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

android {
    namespace = "com.carrental.customer_app"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        applicationId = "com.carrental.customer_app"
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        // Emulator only: clear Flutter's default multi-ABI list first
        ndk {
            abiFilters.clear()
            abiFilters.add("x86_64")
        }
    }

    buildTypes {
        release {
            // TODO: Add your own signing config for the release build.
            // Signing with the debug keys for now, so `flutter run --release` works.
            signingConfig = signingConfigs.getByName("debug")
        }
    }
}

flutter {
    source = "../.."
}

// Emulator-only ABI + serialize CMake to avoid Windows file-lock races on timing logs.
afterEvaluate {
    android.defaultConfig.ndk.abiFilters.clear()
    android.defaultConfig.ndk.abiFilters.add("x86_64")

    val cmakeRelated = tasks.matching {
        val n = it.name
        n.startsWith("configureCMake") || n.startsWith("buildCMake") || n.startsWith("externalNativeBuild")
    }

    cmakeRelated.configureEach {
        val n = name
        if (!n.contains("x86_64", ignoreCase = true)) {
            enabled = false
            return@configureEach
        }
        // Soften lock races: ensure log folder exists before CMake metadata runs
        doFirst {
            val cxxLogs = file("${project.layout.buildDirectory.get().asFile}/intermediates/cxx")
            cxxLogs.mkdirs()
        }
    }

    // Force strict ordering among remaining CMake tasks
    val enabledCmake = cmakeRelated.filter { it.enabled }.toList()
    for (i in 1 until enabledCmake.size) {
        enabledCmake[i].mustRunAfter(enabledCmake[i - 1])
    }
}
