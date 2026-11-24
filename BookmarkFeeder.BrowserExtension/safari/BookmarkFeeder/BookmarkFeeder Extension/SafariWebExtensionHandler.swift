//
//  SafariWebExtensionHandler.swift
//  BookmarkFeeder Extension
//
//  Answers `browser.runtime.sendNativeMessage` from the popup. The only message is
//  `{ type: "getTree" }`, which returns `{ tree: BookmarkTreeNode }` or `{ error, message }`.
//

import SafariServices
import os.log

class SafariWebExtensionHandler: NSObject, NSExtensionRequestHandling {

    func beginRequest(with context: NSExtensionContext) {
        let request = context.inputItems.first as? NSExtensionItem
        let message = request?.userInfo?[SFExtensionMessageKey] as? [String: Any]

        let reply: [String: Any]
        switch message?["type"] as? String {
        case "getTree":
            do {
                reply = ["tree": try SafariBookmarks.tree()]
            } catch let error as SafariBookmarksError {
                os_log(.error, "Reading Safari bookmarks failed: %{public}@", error.message)
                reply = ["error": error.code, "message": error.message]
            } catch {
                reply = ["error": "unreadable", "message": error.localizedDescription]
            }
        default:
            reply = ["error": "unknown-message", "message": "Unsupported native message."]
        }

        let response = NSExtensionItem()
        response.userInfo = [SFExtensionMessageKey: reply]
        context.completeRequest(returningItems: [response], completionHandler: nil)
    }
}
