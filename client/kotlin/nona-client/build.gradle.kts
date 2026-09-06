plugins {
    alias(libs.plugins.android.library)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.maven.publish)
}

// The release workflow passes -PnonaVersion=<tag>, matching the JS and .NET packages.
version = providers.gradleProperty("nonaVersion").getOrElse("0.1.0-SNAPSHOT")

android {
    namespace = "com.nonaconfig.client"
    compileSdk = 36

    defaultConfig {
        minSdk = 24
        consumerProguardFiles("consumer-rules.pro")
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    testOptions {
        unitTests.isReturnDefaultValues = true
    }
}

kotlin {
    jvmToolchain(17)
}

mavenPublishing {
    publishToMavenCentral()
    coordinates("com.nonaconfig", "nona-client", version.toString())

    // Only sign when a key is configured, so local builds work without one.
    if (providers.gradleProperty("signingInMemoryKey").isPresent) {
        signAllPublications()
    }

    pom {
        name.set("Nona Android client")
        description.set(
            "Read Nona self-hosted remote config and feature flags on Android, with in-app " +
                "defaults, offline caching, and a fetch/activate flow.",
        )
        inceptionYear.set("2026")
        url.set("https://nonaconfig.com")

        licenses {
            license {
                name.set("The Apache License, Version 2.0")
                url.set("https://www.apache.org/licenses/LICENSE-2.0.txt")
                distribution.set("https://www.apache.org/licenses/LICENSE-2.0.txt")
            }
        }

        developers {
            developer {
                id.set("ryware")
                name.set("Ryware")
                url.set("https://github.com/Ryware")
            }
        }

        scm {
            url.set("https://github.com/Ryware/nona-config")
            connection.set("scm:git:git://github.com/Ryware/nona-config.git")
            developerConnection.set("scm:git:ssh://git@github.com/Ryware/nona-config.git")
        }
    }
}

dependencies {
    implementation(libs.kotlinx.coroutines.android)

    testImplementation(kotlin("test"))
    testImplementation(libs.json)
    testImplementation(libs.kotlinx.coroutines.test)
}
