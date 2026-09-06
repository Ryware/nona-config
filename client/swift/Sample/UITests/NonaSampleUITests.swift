import XCTest

final class NonaSampleUITests: XCTestCase {
    @MainActor
    func testConnectFetchActivateAndRestore() throws {
        let key = try XCTUnwrap(ProcessInfo.processInfo.environment["NONA_FRONTEND_A"])
        let server = try XCTUnwrap(ProcessInfo.processInfo.environment["NONA_BASE_URL"])
        let app = XCUIApplication()
        app.launch()
        let serverField = app.textFields["serverURL"]
        XCTAssertTrue(serverField.waitForExistence(timeout: 5))
        replace(serverField, with: server)
        replace(app.textFields["frontendKey"], with: key)
        app.buttons["Connect & restore"].tap()
        let reset = app.buttons["Reset cache"]
        app.swipeUp()
        XCTAssertTrue(reset.waitForExistence(timeout: 5))
        reset.tap()
        XCTAssertTrue(app.staticTexts["Cache cleared"].waitForExistence(timeout: 5))
        app.buttons["Fetch"].tap()
        XCTAssertTrue(app.staticTexts["Fetch: success"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.staticTexts["remote"].exists)
        app.buttons["Activate"].tap()
        XCTAssertTrue(app.staticTexts["remote"].waitForExistence(timeout: 5))
        app.terminate()
        app.launch()
        XCTAssertTrue(app.staticTexts["remote"].waitForExistence(timeout: 5))
        XCTAssertTrue(app.staticTexts["A"].exists)
    }

    @MainActor
    func testInvalidConnectionShowsErrorWithoutCrashing() {
        let app = XCUIApplication()
        app.launch()
        let field = app.textFields["serverURL"]
        XCTAssertTrue(field.waitForExistence(timeout: 5))
        replace(field, with: "file:///tmp/config")
        app.buttons["Connect & restore"].tap()
        app.swipeUp()
        XCTAssertTrue(app.staticTexts["baseURL must be an absolute HTTP(S) URL without credentials, query or fragment."].waitForExistence(timeout: 5))
    }

    @MainActor
    private func replace(_ field: XCUIElement, with text: String) {
        field.tap()
        let existing = field.value as? String ?? ""
        if !existing.isEmpty { field.typeText(String(repeating: XCUIKeyboardKey.delete.rawValue, count: existing.count)) }
        field.typeText(text + "\n")
    }
}
