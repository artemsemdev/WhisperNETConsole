import Foundation

/// Swift port of the file-based UI automation bridge client.
/// The same protocol is used by the .NET UI tests (DesktopUiAutomationBridgeClient).
///
/// The bridge works via JSON files:
///   1. Test writes a session file → app discovers it on startup
///   2. App writes a ready signal file
///   3. Test writes request files, app writes response files
final class BridgeClient {

    let sessionId: String

    private static let baseDir: String = {
        TestEnvironment.realHomeDir
            + "/Library/Application Support/VoxFlow/ui-automation"
    }()

    private let requestsDir: String
    private let responsesDir: String

    init() {
        sessionId = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
        requestsDir  = Self.baseDir + "/requests"
        responsesDir = Self.baseDir + "/responses"
    }

    // MARK: - Setup

    /// Creates directories and writes the active-session.json file.
    /// Must be called BEFORE the app launches.
    func prepare() throws {
        let fm = FileManager.default
        try fm.createDirectory(atPath: Self.baseDir, withIntermediateDirectories: true)
        try fm.createDirectory(atPath: requestsDir, withIntermediateDirectories: true)
        try fm.createDirectory(atPath: responsesDir, withIntermediateDirectories: true)

        let session: [String: String] = [
            "sessionId":    sessionId,
            "createdAtUtc": ISO8601DateFormatter().string(from: Date())
        ]
        let data = try JSONSerialization.data(withJSONObject: session, options: [.prettyPrinted])
        let sessionPath = Self.baseDir + "/active-session.json"
        try data.write(to: URL(fileURLWithPath: sessionPath), options: .atomic)
    }

    /// Waits for the app to signal that the bridge is ready.
    func waitForReady(timeout: TimeInterval = 30) throws {
        let readyPath = Self.baseDir + "/ready-\(sessionId).json"
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if FileManager.default.fileExists(atPath: readyPath) { return }
            Thread.sleep(forTimeInterval: 0.1)
        }
        throw BridgeError.timeout("Bridge ready signal not received within \(timeout)s. "
                                  + "Expected: \(readyPath)")
    }

    // MARK: - Commands

    /// Gets a DOM snapshot from the WebView.
    func getSnapshot() throws -> DomSnapshot {
        let response = try sendCommand(kind: "snapshot")
        guard response.success, let payload = response.payload else {
            throw BridgeError.commandFailed(response.error ?? "snapshot failed with no error message")
        }
        guard let payloadData = payload.data(using: .utf8) else {
            throw BridgeError.commandFailed("Could not decode snapshot payload as UTF-8")
        }
        return try JSONDecoder().decode(DomSnapshot.self, from: payloadData)
    }

    /// Clicks a DOM element by CSS selector.
    func click(selector: String) throws {
        let response = try sendCommand(kind: "click", selector: selector)
        guard response.success else {
            throw BridgeError.commandFailed(response.error ?? "click '\(selector)' failed")
        }
    }

    /// Selects a file for transcription, bypassing the native file picker.
    /// The bridge command directly calls AppViewModel.TranscribeFileAsync(filePath)
    /// on the app's main thread.
    func selectFile(path: String) throws {
        let response = try sendCommand(kind: "select-file", selector: path)
        guard response.success else {
            throw BridgeError.commandFailed(response.error ?? "select-file '\(path)' failed")
        }
    }

    // MARK: - Cleanup

    func cleanup() {
        let fm = FileManager.default
        // Remove session and ready files
        try? fm.removeItem(atPath: Self.baseDir + "/active-session.json")
        try? fm.removeItem(atPath: Self.baseDir + "/ready-\(sessionId).json")
        // Clean request/response files for this session
        cleanDirectory(requestsDir, prefix: "request-\(sessionId)")
        cleanDirectory(responsesDir, prefix: "response-\(sessionId)")
    }

    // MARK: - Protocol

    private func sendCommand(kind: String, selector: String? = nil) throws -> BridgeResponse {
        let commandId = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()

        var request: [String: String] = [
            "sessionId": sessionId,
            "commandId": commandId,
            "kind":      kind
        ]
        if let sel = selector { request["selector"] = sel }

        let data = try JSONSerialization.data(withJSONObject: request, options: [])

        // Atomic write: .tmp → rename to .json
        let tmpPath  = requestsDir + "/request-\(sessionId)-\(commandId).tmp"
        let jsonPath = requestsDir + "/request-\(sessionId)-\(commandId).json"
        try data.write(to: URL(fileURLWithPath: tmpPath), options: .atomic)
        try FileManager.default.moveItem(atPath: tmpPath, toPath: jsonPath)

        // Poll for response (20s timeout)
        let responsePath = responsesDir + "/response-\(sessionId)-\(commandId).json"
        let deadline = Date().addingTimeInterval(20)
        while Date() < deadline {
            if FileManager.default.fileExists(atPath: responsePath) {
                let responseData = try Data(contentsOf: URL(fileURLWithPath: responsePath))
                return try JSONDecoder().decode(BridgeResponse.self, from: responseData)
            }
            Thread.sleep(forTimeInterval: 0.1)
        }
        throw BridgeError.timeout("No response for \(kind) command within 20s")
    }

    private func cleanDirectory(_ dir: String, prefix: String) {
        guard let files = try? FileManager.default.contentsOfDirectory(atPath: dir) else { return }
        for file in files where file.hasPrefix(prefix) {
            try? FileManager.default.removeItem(atPath: dir + "/" + file)
        }
    }
}

// MARK: - Models

struct DomSnapshot: Decodable {
    let activeScreenId: String?
    let bodyText: String
    let visibleElementIds: [String]
}

struct BridgeResponse: Decodable {
    let sessionId: String
    let commandId: String
    let success: Bool
    let payload: String?
    let error: String?
}

enum BridgeError: Error, CustomStringConvertible {
    case timeout(String)
    case commandFailed(String)

    var description: String {
        switch self {
        case .timeout(let msg):        return msg
        case .commandFailed(let msg):  return msg
        }
    }
}
