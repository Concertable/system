using Concertable.Customer.E2ETests.Mobile.Hooks;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Concertable.Customer.E2ETests.Mobile.Support;

public sealed class MobileFixture : IAsyncLifetime
{
    private Process? emulatorProcess;
    private Process? appiumProcess;
    private bool weStartedEmulator;
    private bool weStartedAppium;

    public Uri AppiumServerUri { get; private set; } = null!;
    public string ApkPath { get; private set; } = null!;
    public string AppPackage { get; private set; } = null!;
    public string AppActivity { get; private set; } = null!;
    public string AvdName { get; private set; } = null!;

    public AppFixture App { get; } = new();

    public async Task InitializeAsync()
    {
        EmulatorHooks.Fixture = this;

        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.E2E.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        AppiumServerUri = new Uri(config["Mobile:AppiumServer"] ?? "http://127.0.0.1:4723/");
        AppPackage = config["Mobile:AppPackage"] ?? "com.concertable.app";
        AppActivity = config["Mobile:AppActivity"] ?? ".MainActivity";
        AvdName = config["Mobile:AvdName"] ?? "ConcertableTest";

        var apkRelative = config["Mobile:ApkPath"] ?? "TestAssets/concertable-debug.apk";
        ApkPath = Path.IsPathRooted(apkRelative)
            ? apkRelative
            : Path.Combine(AppContext.BaseDirectory, apkRelative);

        if (!File.Exists(ApkPath))
            throw new FileNotFoundException(
                $"APK not found at {ApkPath}. Build it via 'cd app/mobile && npx expo prebuild --platform android && cd android && ./gradlew assembleDebug', " +
                $"then copy app/build/outputs/apk/debug/app-debug.apk to TestAssets/concertable-debug.apk. See Concertable.Customer.E2ETests.Mobile/SETUP.md.");

        await EnsureEmulatorRunningAsync(AvdName);
        await EnsureAppiumServerAsync(AppiumServerUri);
        await App.InitializeAsync();
        InstallApk(ApkPath);
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();

        if (weStartedAppium && appiumProcess is { HasExited: false })
        {
            try { appiumProcess.Kill(entireProcessTree: true); }
            catch { }
            appiumProcess = null;
        }

        if (weStartedEmulator)
        {
            try { RunAdb("emu kill"); }
            catch { }
            emulatorProcess = null;
        }
    }

    private async Task EnsureEmulatorRunningAsync(string avdName)
    {
        if (HasRunningEmulator()) return;

        var emulatorBin = ResolveEmulatorBinary();
        emulatorProcess = Process.Start(new ProcessStartInfo
        {
            FileName = emulatorBin,
            Arguments = $"-avd {avdName} -no-snapshot-load -no-boot-anim",
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        weStartedEmulator = true;

        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            if (HasRunningEmulator() && BootCompleted()) return;
            await Task.Delay(2_000);
        }
        throw new TimeoutException($"Emulator {avdName} did not become ready within 3 minutes.");
    }

    private async Task EnsureAppiumServerAsync(Uri serverUri)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            var status = await http.GetAsync(new Uri(serverUri, "status"));
            if (status.IsSuccessStatusCode) return;
        }
        catch { }

        var appiumCmd = OperatingSystem.IsWindows() ? "appium.cmd" : "appium";
        appiumProcess = Process.Start(new ProcessStartInfo
        {
            FileName = appiumCmd,
            Arguments = $"--port {serverUri.Port} --base-path {serverUri.AbsolutePath.TrimEnd('/')}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        weStartedAppium = true;

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var status = await http.GetAsync(new Uri(serverUri, "status"));
                if (status.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(1_000);
        }
        throw new TimeoutException($"Appium server at {serverUri} did not become ready within 60 seconds.");
    }

    private void InstallApk(string apkPath) => RunAdb($"install -r \"{apkPath}\"");

    private bool HasRunningEmulator()
    {
        var output = RunAdb("devices");
        return output.Split('\n').Any(line => line.StartsWith("emulator-") && line.Contains("device"));
    }

    private bool BootCompleted()
    {
        try
        {
            var prop = RunAdb("shell getprop sys.boot_completed").Trim();
            return prop == "1";
        }
        catch
        {
            return false;
        }
    }

    private string RunAdb(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveAdbBinary(),
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(30_000);
        return stdout;
    }

    private static string ResolveAdbBinary() =>
        ResolveAndroidBinary("platform-tools", OperatingSystem.IsWindows() ? "adb.exe" : "adb");

    private static string ResolveEmulatorBinary() =>
        ResolveAndroidBinary("emulator", OperatingSystem.IsWindows() ? "emulator.exe" : "emulator");

    private static string ResolveAndroidBinary(string folder, string binary)
    {
        var sdkRoot = Environment.GetEnvironmentVariable("ANDROID_HOME")
                   ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        if (sdkRoot is not null)
        {
            var candidate = Path.Combine(sdkRoot, folder, binary);
            if (File.Exists(candidate)) return candidate;
        }
        return binary;
    }

}
