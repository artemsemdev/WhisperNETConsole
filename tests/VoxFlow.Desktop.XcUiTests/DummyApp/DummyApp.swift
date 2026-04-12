import SwiftUI

/// Minimal host app required by the Xcode UI Testing Bundle target.
/// The actual app under test is VoxFlow.Desktop, launched via XCUIApplication(url:).
@main
struct DummyApp: App {
    var body: some Scene {
        WindowGroup {
            Text("XCUITest Host")
        }
    }
}
