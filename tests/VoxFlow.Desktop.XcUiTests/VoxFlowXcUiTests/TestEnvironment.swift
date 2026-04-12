import Foundation
import CoreGraphics
import Darwin

// MARK: - Path Resolution & Config

/// Manages paths, configuration overrides, and test artifacts.
/// Mirrors the conventions in the .NET UI tests (RepositoryLayout, DesktopUiTestSession, etc.).
enum TestEnvironment {

    // MARK: Real Home Directory

    /// The actual user home directory, bypassing any App Sandbox container redirection.
    /// The XCUITest runner is sandboxed, so NSHomeDirectory() / FileManager.homeDirectoryForCurrentUser
    /// return the container path. We use getpwuid() to get the real /Users/{username} path,
    /// because the VoxFlow.Desktop app reads config from the real ~/Library/Application Support/.
    static let realHomeDir: String = {
        guard let pw = getpwuid(getuid()) else {
            return FileManager.default.homeDirectoryForCurrentUser.path
        }
        return String(cString: pw.pointee.pw_dir)
    }()

    // MARK: Repository Root

    /// Repository root, resolved from `VOXFLOW_REPO_ROOT` env or this source file's compile-time location.
    static let repoRoot: String = {
        if let env = ProcessInfo.processInfo.environment["VOXFLOW_REPO_ROOT"] {
            return env
        }
        // This file: tests/VoxFlow.Desktop.XcUiTests/VoxFlowXcUiTests/TestEnvironment.swift
        // Repo root is 4 directory levels up.
        var url = URL(fileURLWithPath: #filePath)
        for _ in 0..<4 { url.deleteLastPathComponent() }
        return url.path
    }()

    // MARK: Input & Model Files

    static let inputFileOne = repoRoot + "/artifacts/Input/Test 1.m4a"
    static let inputFileTwo = repoRoot + "/artifacts/Input/Test 2.m4a"
    static let modelFile    = repoRoot + "/models/ggml-base.bin"

    /// Absolute path to ffmpeg, resolved at runtime.
    /// The Mac Catalyst app's process doesn't inherit the user's shell PATH,
    /// so the test must provide the full path.
    static let ffmpegPath: String = {
        // Common Homebrew locations first, then fall back to `which`
        for candidate in ["/usr/local/bin/ffmpeg", "/opt/homebrew/bin/ffmpeg"] {
            if FileManager.default.fileExists(atPath: candidate) {
                return candidate
            }
        }
        // Runtime lookup via shell
        let task = Process()
        task.executableURL = URL(fileURLWithPath: "/bin/bash")
        task.arguments = ["-lc", "which ffmpeg"]
        let pipe = Pipe()
        task.standardOutput = pipe
        try? task.run()
        task.waitUntilExit()
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        let result = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return result.isEmpty ? "ffmpeg" : result
    }()

    // MARK: App Bundle

    /// Resolves the path to VoxFlow.Desktop.app (env override or auto-discovery from build output).
    static func resolveAppPath() throws -> String {
        if let env = ProcessInfo.processInfo.environment["VOXFLOW_DESKTOP_UI_APP_PATH"] {
            guard FileManager.default.fileExists(atPath: env) else {
                throw SetupError.appNotFound(
                    "VOXFLOW_DESKTOP_UI_APP_PATH points to missing path: \(env)")
            }
            return env
        }

        let rids: [String] = {
            #if arch(arm64)
            return ["maccatalyst-arm64", "maccatalyst-x64"]
            #else
            return ["maccatalyst-x64", "maccatalyst-arm64"]
            #endif
        }()

        for config in ["Debug", "Release"] {
            for rid in rids {
                let path = "\(repoRoot)/src/VoxFlow.Desktop/bin/\(config)/net9.0-maccatalyst/\(rid)/VoxFlow.Desktop.app"
                if FileManager.default.fileExists(atPath: path) {
                    return path
                }
            }
        }

        throw SetupError.appNotFound(
            "VoxFlow.Desktop.app not found. Build with 'dotnet build src/VoxFlow.Desktop' or set VOXFLOW_DESKTOP_UI_APP_PATH.")
    }

    // MARK: Expected Output

    /// Computes the result file path the app produces for a given input.
    /// Mirrors `AppViewModel.TranscribeFileAsync`: ~/Documents/VoxFlow/output/{name}{ext}
    static func expectedResultPath(for inputFileName: String, ext: String = ".txt") -> String {
        let baseName = (inputFileName as NSString).deletingPathExtension
        return "\(realHomeDir)/Documents/VoxFlow/output/\(baseName)\(ext)"
    }

    // MARK: User Config Override

    private static let configDir  = realHomeDir + "/Library/Application Support/VoxFlow"
    private static let configPath = configDir + "/appsettings.json"

    private static var originalConfigData: Data?
    private static var configExistedBefore = false

    /// Writes the test config override (same location the .NET tests use).
    static func writeTestConfig(scenarioDir: String, resultFormat: String? = nil) throws {
        configExistedBefore = FileManager.default.fileExists(atPath: configPath)
        if configExistedBefore {
            originalConfigData = FileManager.default.contents(atPath: configPath)
        }

        try FileManager.default.createDirectory(atPath: configDir, withIntermediateDirectories: true)

        let workDir = scenarioDir + "/work"
        try FileManager.default.createDirectory(atPath: workDir, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            atPath: scenarioDir + "/diagnostics", withIntermediateDirectories: true)

        var transcription: [String: Any] = [
            "processingMode": "single",
            "wavFilePath":    workDir + "/transcription.wav",
            "resultFilePath": workDir + "/transcription.txt",
            "modelFilePath":  modelFile,
            "ffmpegExecutablePath": ffmpegPath,
            "supportedLanguages": [["code": "en", "displayName": "English"]],
            "startupValidation": [
                "enabled": true,
                "printDetailedReport": true,
                "checkInputFile": false,
                "checkOutputDirectories": true,
                "checkOutputWriteAccess": true,
                "checkFfmpegAvailability": true,
                "checkModelType": true,
                "checkModelDirectory": true,
                "checkModelLoadability": true,
                "checkLanguageSupport": false,
                "checkWhisperRuntime": false
            ]
        ]
        if let fmt = resultFormat { transcription["resultFormat"] = fmt }

        let config: [String: Any] = ["transcription": transcription]
        let data = try JSONSerialization.data(withJSONObject: config, options: [.prettyPrinted, .sortedKeys])
        try data.write(to: URL(fileURLWithPath: configPath))
    }

    /// Restores the original user config (or deletes it if none existed before the test).
    static func restoreConfig() {
        if configExistedBefore, let data = originalConfigData {
            try? data.write(to: URL(fileURLWithPath: configPath))
        } else if !configExistedBefore {
            try? FileManager.default.removeItem(atPath: configPath)
        }
    }

    // MARK: Scenario Artifacts

    /// Creates a timestamped artifact directory.
    /// Uses NSTemporaryDirectory() to avoid macOS TCC restrictions on Desktop/Documents.
    /// Prefixed with "xc-" to distinguish from .NET test artifacts.
    static func createScenarioDir(name: String) -> String {
        let fmt = DateFormatter()
        fmt.dateFormat = "yyyyMMdd-HHmmss"
        fmt.timeZone = TimeZone(identifier: "UTC")
        let slug = "\(fmt.string(from: Date()))-xc-\(name)"
        let dir = NSTemporaryDirectory() + "VoxFlowXcUiTests/" + slug
        try? FileManager.default.createDirectory(atPath: dir, withIntermediateDirectories: true)
        return dir
    }

    // MARK: Errors

    enum SetupError: Error, CustomStringConvertible {
        case appNotFound(String)
        var description: String {
            switch self { case .appNotFound(let msg): return msg }
        }
    }
}

// MARK: - Layout-Independent Keyboard Shortcuts

/// Hardware key codes (US QWERTY layout). These are physical key positions,
/// independent of the active keyboard input language.
enum KeyCode {
    static let g:         CGKeyCode = 5   // G key
    static let v:         CGKeyCode = 9   // V key
    static let returnKey: CGKeyCode = 36  // Return key
}

/// Posts a keyboard event using hardware key codes via CoreGraphics.
/// Works regardless of the active input language (Russian, English, etc.).
/// Requires macOS Accessibility access for the test runner process.
func postKeyboardShortcut(keyCode: CGKeyCode, modifiers: CGEventFlags = []) {
    let source = CGEventSource(stateID: .combinedSessionState)
    if let down = CGEvent(keyboardEventSource: source, virtualKey: keyCode, keyDown: true) {
        down.flags = modifiers
        down.post(tap: .cgSessionEventTap)
    }
    Thread.sleep(forTimeInterval: 0.05)
    if let up = CGEvent(keyboardEventSource: source, virtualKey: keyCode, keyDown: false) {
        up.flags = modifiers
        up.post(tap: .cgSessionEventTap)
    }
}

/// Copies text to the macOS clipboard using pbcopy.
func copyToClipboard(_ text: String) throws {
    let escaped = text.replacingOccurrences(of: "'", with: "'\\''")
    let task = Process()
    task.executableURL = URL(fileURLWithPath: "/bin/bash")
    task.arguments = ["-c", "echo -n '\(escaped)' | pbcopy"]
    try task.run()
    task.waitUntilExit()
    guard task.terminationStatus == 0 else {
        throw NSError(domain: "TestEnvironment", code: 1,
                      userInfo: [NSLocalizedDescriptionKey: "pbcopy failed with exit code \(task.terminationStatus)"])
    }
}
