import XCTest

/// XCUITest equivalent of `HappyPath_UserSelectsFile_SeesRunningState_AndGetsResult`
/// from the .NET Desktop UI test suite.
///
/// Uses a hybrid approach:
///   - `open -n` for app lifecycle (launch) and `pkill` for termination
///   - File-based bridge for WebView interaction (same protocol as the .NET tests)
///   - `select-file` bridge command to trigger transcription directly via AppViewModel,
///     bypassing the native file picker (which can't be automated from the sandboxed
///     XCUITest runner — AppleScript is blocked by the App Sandbox)
///
/// WKWebView content is not visible in the macOS accessibility tree for Mac Catalyst apps,
/// so pure XCUIElement queries cannot reach Blazor UI elements. The bridge provides reliable
/// access to the DOM snapshot and direct ViewModel actions.
final class VoxFlowHappyPathTests: XCTestCase {

    private var app: XCUIApplication!
    private var bridge: BridgeClient!
    private var scenarioDir: String!

    // MARK: - Lifecycle

    override func setUpWithError() throws {
        // This test project is inherently opt-in: it only runs when explicitly invoked
        // via `xcodebuild test -project ... -scheme VoxFlowXcUiTests`.
        // As an additional safety net, skip if the app hasn't been built.
        let appPath: String
        do {
            appPath = try TestEnvironment.resolveAppPath()
        } catch {
            throw XCTSkip(
                "VoxFlow.Desktop.app not found — build it first to enable this test. \(error)")
        }

        let fm = FileManager.default
        try XCTSkipUnless(fm.fileExists(atPath: TestEnvironment.inputFileOne),
            "Sample input file missing: \(TestEnvironment.inputFileOne)")
        try XCTSkipUnless(fm.fileExists(atPath: TestEnvironment.modelFile),
            "Whisper model missing: \(TestEnvironment.modelFile)")

        continueAfterFailure = false

        // 1. Prepare the bridge (must happen before app launch)
        bridge = BridgeClient()
        try bridge.prepare()

        // 2. Prepare scenario artifacts and inject test config
        scenarioDir = TestEnvironment.createScenarioDir(name: "happy-path")
        try TestEnvironment.writeTestConfig(scenarioDir: scenarioDir)

        // 3. Ensure the GUI session has PATH entries for dotnet/ffmpeg.
        // Mac Catalyst apps launched via `open -n` don't inherit the shell PATH.
        // Use `launchctl setenv` to inject the needed paths into the GUI environment.
        try injectGuiPathEntries()

        // 4. Launch VoxFlow.Desktop via `open -n`, matching the .NET tests.
        // XCUIApplication.launch() can restrict the app's GUI capabilities
        // (e.g., file picker presentation). `open -n` gives the app full
        // user-interaction context.
        let openTask = Process()
        openTask.executableURL = URL(fileURLWithPath: "/usr/bin/open")
        openTask.arguments = ["-n", appPath]
        try openTask.run()
        openTask.waitUntilExit()

        // Do NOT create XCUIApplication here — connecting to the app injects
        // XCUITest accessibility framework which interferes with WKWebView events.
        // We'll create it lazily in tearDown for screenshots only.

        // 5. Wait for the bridge to become ready (app polls and signals back)
        try bridge.waitForReady(timeout: 30)
    }

    override func tearDownWithError() throws {
        // Capture screenshot on failure (connect to app lazily for this)
        if let run = testRun, run.failureCount > 0 {
            let liveApp = app ?? XCUIApplication(bundleIdentifier: "com.voxflow.app")
            let screenshot = liveApp.screenshot()
            let attachment = XCTAttachment(screenshot: screenshot)
            attachment.name = "failure-screenshot"
            attachment.lifetime = .keepAlways
            add(attachment)
        }

        // Terminate the app via pkill (avoiding XCUIApplication injection)
        let killTask = Process()
        killTask.executableURL = URL(fileURLWithPath: "/usr/bin/pkill")
        killTask.arguments = ["-x", "VoxFlow.Desktop"]
        try? killTask.run()
        killTask.waitUntilExit()

        Thread.sleep(forTimeInterval: 2.0)
        bridge?.cleanup()
        TestEnvironment.restoreConfig()
    }

    // MARK: - Happy-Path Test

    func testHappyPath_UserSelectsFile_SeesRunningState_AndGetsResult() throws {
        // ── 1. Wait for the Ready screen ──
        try waitForScreen("ready-screen", timeout: 45,
            message: "Ready screen should appear within 45 seconds")
        try waitForElement("browse-files-button", timeout: 10,
            message: "'Browse Files' button should appear on the Ready screen")

        // Diagnostic: capture the ready screen state to detect validation errors
        let readySnapshot = try bridge.getSnapshot()
        if readySnapshot.visibleElementIds.contains("startup-validation-message") {
            XCTFail("Startup validation error detected. Body text:\n\(readySnapshot.bodyText)")
            return
        }

        // ── 2. Select the sample audio file via bridge command ──
        // The select-file bridge command directly calls AppViewModel.TranscribeFileAsync
        // on the app's main thread, bypassing both the JS click limitation in WKWebView
        // and the native file picker dialog (which can't be automated from the
        // sandboxed XCUITest runner — AppleScript is blocked by the sandbox).
        try bridge.selectFile(path: TestEnvironment.inputFileOne)

        // ── 3. Wait for Running screen ──
        // The app may transition through Running very quickly if it fails early.
        // Wait for either running-screen or complete-screen (or failed-screen for diagnostics).
        try waitForScreen("running-screen", timeout: 45,
            message: "Running screen should appear after file selection",
            acceptAlternatives: ["complete-screen", "failed-screen"])

        // If we landed on the failed screen, capture the error for diagnostics
        let postSelectSnapshot = try bridge.getSnapshot()
        if postSelectSnapshot.activeScreenId == "failed-screen" {
            let diagPath = NSTemporaryDirectory() + "voxflow-xcuitest-failed-snapshot.txt"
            let diagContent = "ActiveScreen: \(postSelectSnapshot.activeScreenId ?? "nil")\n"
                + "VisibleIDs: \(postSelectSnapshot.visibleElementIds.joined(separator: ", "))\n"
                + "BodyText:\n\(postSelectSnapshot.bodyText)"
            try? diagContent.write(toFile: diagPath, atomically: true, encoding: .utf8)
            NSLog("XCUITest diagnostic written to: \(diagPath)")
            XCTFail("Transcription failed immediately. See \(diagPath) for details. "
                  + "IDs: \(postSelectSnapshot.visibleElementIds.joined(separator: ", "))")
            return
        }

        if postSelectSnapshot.activeScreenId != "complete-screen" {
            try waitForElement("cancel-transcription-button", timeout: 10,
                message: "'Cancel Transcription' button should appear on the Running screen")
        }

        // ── 4. Wait for Complete screen ──
        try waitForScreen("complete-screen", timeout: 180,
            message: "Complete screen should appear within 3 minutes (transcription)")
        try waitForElement("copy-text-button", timeout: 15,
            message: "'Copy Transcript' button should appear on the Complete screen")
        try waitForElement("open-folder-button", timeout: 15,
            message: "'Open Result Folder' button should appear on the Complete screen")

        // ── 5. Verify the result file ──
        let inputFileName = (TestEnvironment.inputFileOne as NSString).lastPathComponent
        let resultPath = TestEnvironment.expectedResultPath(for: inputFileName)
        XCTAssertTrue(FileManager.default.fileExists(atPath: resultPath),
            "Expected result file at: \(resultPath)")

        let content = try String(contentsOfFile: resultPath, encoding: .utf8)
        XCTAssertFalse(content.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
            "Result file should contain transcribed text")
    }

    // MARK: - Environment Setup

    /// Injects PATH entries for dotnet and ffmpeg into the GUI session environment
    /// via `launchctl setenv`. Mac Catalyst apps launched via `open -n` don't inherit
    /// the user's shell PATH, so `Process.Start("dotnet")` fails with ENOENT.
    private func injectGuiPathEntries() throws {
        // Collect directories that contain dotnet and ffmpeg
        var extraDirs: [String] = []
        for tool in ["dotnet", "ffmpeg"] {
            let which = Process()
            which.executableURL = URL(fileURLWithPath: "/bin/bash")
            which.arguments = ["-lc", "which \(tool)"]
            let pipe = Pipe()
            which.standardOutput = pipe
            which.standardError = FileHandle.nullDevice
            try which.run()
            which.waitUntilExit()
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            if let path = String(data: data, encoding: .utf8)?
                .trimmingCharacters(in: .whitespacesAndNewlines),
               !path.isEmpty {
                let dir = (path as NSString).deletingLastPathComponent
                if !extraDirs.contains(dir) { extraDirs.append(dir) }
            }
        }

        guard !extraDirs.isEmpty else { return }

        // Get current GUI PATH (may be minimal)
        let getCurrent = Process()
        getCurrent.executableURL = URL(fileURLWithPath: "/bin/launchctl")
        getCurrent.arguments = ["getenv", "PATH"]
        let currentPipe = Pipe()
        getCurrent.standardOutput = currentPipe
        getCurrent.standardError = FileHandle.nullDevice
        try? getCurrent.run()
        getCurrent.waitUntilExit()
        let currentPath = String(
            data: currentPipe.fileHandleForReading.readDataToEndOfFile(),
            encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""

        let existingDirs = currentPath.split(separator: ":").map(String.init)
        let newDirs = extraDirs.filter { !existingDirs.contains($0) }
        guard !newDirs.isEmpty else { return }

        let newPath = (newDirs + existingDirs).joined(separator: ":")

        let setenv = Process()
        setenv.executableURL = URL(fileURLWithPath: "/bin/launchctl")
        setenv.arguments = ["setenv", "PATH", newPath]
        try setenv.run()
        setenv.waitUntilExit()
    }

    // MARK: - Bridge Helpers

    /// Polls the bridge for the expected active screen.
    /// When `acceptAlternatives` is provided, any of those screen IDs also satisfy the wait.
    private func waitForScreen(_ screenId: String, timeout: TimeInterval, message: String,
                               acceptAlternatives: [String] = []) throws {
        let accepted = Set([screenId] + acceptAlternatives)
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let snapshot = try bridge.getSnapshot()
            if let active = snapshot.activeScreenId, accepted.contains(active) { return }
            Thread.sleep(forTimeInterval: 0.25)
        }
        // Final check with diagnostic info
        let snapshot = try bridge.getSnapshot()
        XCTFail("\(message) (current screen: '\(snapshot.activeScreenId ?? "nil")', "
              + "visible IDs: \(snapshot.visibleElementIds.joined(separator: ",")))")
    }

    /// Polls the bridge for a visible DOM element.
    private func waitForElement(_ elementId: String, timeout: TimeInterval, message: String) throws {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let snapshot = try bridge.getSnapshot()
            if snapshot.visibleElementIds.contains(elementId) { return }
            Thread.sleep(forTimeInterval: 0.25)
        }
        let snapshot = try bridge.getSnapshot()
        XCTFail("\(message) (visible IDs: \(snapshot.visibleElementIds.joined(separator: ",")))")
    }

}
