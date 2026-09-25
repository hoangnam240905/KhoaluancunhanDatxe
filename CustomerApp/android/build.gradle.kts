allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

val newBuildDir: Directory = rootProject.layout.buildDirectory.dir("../../build").get()
rootProject.layout.buildDirectory.value(newBuildDir)

subprojects {
    val newSubprojectBuildDir: Directory = newBuildDir.dir(project.name)
    project.layout.buildDirectory.value(newSubprojectBuildDir)
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
