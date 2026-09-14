plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
}

android {
    namespace = "com.nonaconfig.sample"
    compileSdk = 36
    defaultConfig {
        applicationId = "com.nonaconfig.sample"
        minSdk = 24
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }
    sourceSets.getByName("androidTest").assets.srcDir("../../qa/contracts")
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}
kotlin { jvmToolchain(17) }

dependencies {
    implementation(project(":nona-client"))
    implementation(libs.kotlinx.coroutines.android)
    androidTestImplementation(libs.test.runner)
    androidTestImplementation(libs.test.junit)
}
