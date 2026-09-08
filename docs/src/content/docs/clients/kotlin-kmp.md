---
title: Kotlin Multiplatform client (Android & iOS)
description: Read Nona config values and feature flags from Android (Kotlin) and iOS (Swift) using the community NonaConfigKMP SDK, with in-app defaults and fetch/activation lifecycle.
---

Repository: [`rfaturriza/NonaConfigKMP`](https://github.com/rfaturriza/NonaConfigKMP)

Maven Central package: `io.github.rfaturriza:nona-config`

Swift Package Manager: `https://github.com/rfaturriza/NonaConfigKMP`

Targets:

- Android (API level 21+)
- iOS (iOS 13+)
- Kotlin Multiplatform (`commonMain`)

The Kotlin Multiplatform client is a good fit for:

- Native Android apps (Kotlin / Compose)
- Native iOS apps (Swift / SwiftUI)
- Kotlin Multiplatform shared modules
- Apps migrating from Firebase Remote Config requiring `fetchAndActivate()` semantics

## Install

### Gradle (Android / KMP)

Add the dependency to your `build.gradle.kts`:

```kotlin
dependencies {
    implementation("io.github.rfaturriza:nona-config:1.0.1")
}
```

### Swift Package Manager (iOS)

Add `https://github.com/rfaturriza/NonaConfigKMP` via Xcode **File > Add Package Dependencies...** or in your `Package.swift`:

```swift
dependencies: [
    .package(url: "https://github.com/rfaturriza/NonaConfigKMP", from: "1.0.1")
]
```

## Prepare the value in admin

Before wiring the mobile app:

1. open `Projects` in admin UI
2. create or select your project
3. select the target environment such as `production`
4. create parameters or feature flags
5. publish a release and set it active
6. create an API key in `API Keys` with `client` scope

## Basic Initialization

### Kotlin (Android / KMP)

```kotlin
import com.nonaconfig.NonaConfig

val nonaConfig = NonaConfig.instance
nonaConfig.initialize(
    apiKey = "your-api-key",
    environmentId = "production",
    baseUrl = "https://nona.example.com"
)
```

### Swift (iOS)

In Swift, the companion instance is exposed as `NonaConfigClient`:

```swift
import NonaConfig

let client = NonaConfigClient.companion.instance
client.initialize(
    apiKey: "your-api-key",
    environmentId: "production",
    baseUrl: "https://nona.example.com"
)
```

## Setting In-App Defaults

Define fallback values locally before fetching remote configuration:

### Kotlin

```kotlin
nonaConfig.setDefaults(mapOf(
    "welcome_message" to "Hello!",
    "feature_enabled" to true
))
```

### Swift

```swift
client.setDefaults(defaults: [
    "welcome_message": "Hello Swift!",
    "feature_enabled": true
])
```

## Fetching and Activating

The SDK decouples remote network fetching from applying values to the active in-memory state:

### Kotlin

```kotlin
coroutineScope.launch {
    val updated = nonaConfig.fetchAndActivate()
    if (updated) {
        println("New remote values fetched and activated!")
    }
}
```

### Swift

```swift
Task {
    do {
        let updated = try await client.fetchAndActivate()
        if updated {
            print("New remote values fetched and activated!")
        }
    } catch {
        print("Fetch failed: \(error)")
    }
}
```

## Retrieving Values

### Kotlin

```kotlin
val message = nonaConfig.getString("welcome_message")
val isEnabled = nonaConfig.getBoolean("feature_enabled")
val count = nonaConfig.getLong("max_items")
```

### Swift

```swift
let message = client.getString(key: "welcome_message")
let isEnabled = client.getBoolean(key: "feature_enabled")
```

## Advanced Settings

Customize minimum fetch intervals and release pinning:

```kotlin
val settings = NonaConfigSettings.Builder()
    .setBaseUrl("https://nona.example.com")
    .setMinimumFetchInterval(1.hours)
    .setReleaseVersion("1.1.x")
    .build()

nonaConfig.setConfigSettings(settings)
```

## Features & Architecture

- **ETag support:** Uses HTTP ETags so fetch requests only download changed payloads.
- **Local persistence:** Values are stored locally to ensure instant app startup.
- **Decoupled activation:** Allows deferred UI updates to avoid jarring mid-session layout shifts.

## When to use this client

Use `NonaConfigKMP` when:

- Building mobile applications targeting Android, iOS, or KMP.
- Migrating from Firebase Remote Config and wanting a familiar API.
- Needing in-app default fallbacks and ETag-backed bandwidth optimization.
