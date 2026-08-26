allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

// Build outside Documents (Defender / Controlled Folder Access often locks CMake logs there).
val externalBuild = file("C:/Temp/carrental-customer-build")
externalBuild.mkdirs()
rootProject.layout.buildDirectory.set(externalBuild)

subprojects {
    project.layout.buildDirectory.set(File(externalBuild, project.name))
}

subprojects {
    project.evaluationDependsOn(":app")
}

// Plugins (shared_preferences_android, …) also schedule configureCMake per-ABI.
// On Windows those race and lock generate_cxx_metadata_*_timing.txt — keep only emulator ABI.
gradle.projectsEvaluated {
    allprojects {
        tasks.configureEach {
            val n = name
            val isCmake =
                n.startsWith("configureCMake") ||
                    n.startsWith("buildCMake") ||
                    n.startsWith("externalNativeBuild")
            if (isCmake && !n.contains("x86_64", ignoreCase = true)) {
                enabled = false
            }
        }
    }
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
