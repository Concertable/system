# Mobile E2E setup (one-off)

This project drives the Concertable mobile app via Appium against an Android
emulator. The first run on a machine needs the toolchain below; after that it's
"press Run in Test Explorer".

## Prerequisites

1. **JDK 17** — Android Gradle needs it. `winget install Microsoft.OpenJDK.17`.
2. **Node 20+** — for the Appium server. `winget install OpenJS.NodeJS.LTS`.
3. **Android Studio** — installs the SDK + emulator + system image in one shot.
   Download from <https://developer.android.com/studio>. After install:
   - Open SDK Manager and ensure these are installed:
     - **Android 14 (API 34)** — Google APIs x86_64 image
     - **Android SDK Platform-Tools**
     - **Android Emulator**
   - Set the env var `ANDROID_HOME` to the SDK path (e.g.
     `%LOCALAPPDATA%\Android\Sdk`). Restart your shell.
4. **AVD** named `ConcertableTest`:
   - Android Studio → Device Manager → Create Device
   - Pixel 6, system image: Android 14 (API 34) Google APIs x86_64
   - Name: `ConcertableTest`
5. **Appium 2** with the UiAutomator2 driver:
   ```
   npm install -g appium
   appium driver install uiautomator2
   ```

## Build the app's APK

The dev-client APK is what the test installs and drives. Build it once and copy
it into `TestAssets/`. Rebuild when mobile-app code changes that you want
covered by E2E.

```powershell
cd app/mobile
npx expo prebuild --platform android
cd android
./gradlew assembleDebug
```

Then copy `app/mobile/android/app/build/outputs/apk/debug/app-debug.apk` to
`api/Concertable.Customer/tests/E2ETests/Concertable.Customer.E2ETests.Mobile/TestAssets/concertable-debug.apk`.

> The APK is gitignored. Each developer builds locally; CI builds on its own
> runner.

## Run the tests

Press Run in Visual Studio Test Explorer on any scenario under
`Concertable.Customer.E2ETests.Mobile`. The `BeforeTestRun` hook will:

1. Boot the `ConcertableTest` emulator if not already running
2. Start the Appium server on `127.0.0.1:4723` if not already running
3. Install the APK via `adb install -r`
4. Start the Aspire app + reseed (same as the web suite's `AppFixture`)

After tests finish, the emulator and Appium server it started will be killed.
Pre-existing emulator/Appium processes are left alone.

## Troubleshooting

- **"APK not found at TestAssets/concertable-debug.apk"** — Build it. See above.
- **"adb is not recognised"** — `ANDROID_HOME` not set, or
  `%ANDROID_HOME%\platform-tools` not on PATH.
- **Emulator boots but tests can't see elements** — Make sure the mobile app
  components on the purchase flow have `testID` props set. Convention:
  `kebab-case-purpose-name`, no type prefixes. RN's `testID` lands on Android
  UiAutomator2 as `content-desc`, queried via `MobileBy.AccessibilityId`.
- **WebView login times out** — Appium's UiAutomator2 driver bundles a
  chromedriver. If the emulator's Chrome version is newer than the bundled
  chromedriver, install a matching one:
  `appium driver run uiautomator2 install-chromedriver --chromedriver-version <version>`.
- **Stripe payment sheet not visible** — The mobile app uses
  `@stripe/stripe-react-native` (a native module). It does **not** run in Expo
  Go — you must use a dev-client APK built via `expo prebuild`. Don't try
  Expo Go for E2E.
