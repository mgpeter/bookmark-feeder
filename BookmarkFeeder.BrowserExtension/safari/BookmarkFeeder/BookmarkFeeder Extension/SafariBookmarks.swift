//
//  SafariBookmarks.swift
//  BookmarkFeeder Extension
//
//  Safari does not expose `browser.bookmarks` to web extensions, so the popup asks this native
//  handler instead. It reads Safari's own Bookmarks.plist and returns the tree in the shape of
//  `chrome.bookmarks.BookmarkTreeNode`, so the TypeScript sync code stays browser-agnostic.
//

import CryptoKit
import Foundation

enum SafariBookmarksError: Error {
    /// The file exists but macOS refused to open it: Full Disk Access has not been granted.
    case noAccess
    case notFound
    case unreadable(String)

    var code: String {
        switch self {
        case .noAccess: return "no-access"
        case .notFound: return "not-found"
        case .unreadable: return "unreadable"
        }
    }

    var message: String {
        switch self {
        case .noAccess: return "macOS blocked access to Safari's bookmarks."
        case .notFound: return "Safari's bookmarks file was not found."
        case .unreadable(let detail): return "Safari's bookmarks file could not be read: \(detail)"
        }
    }
}

enum SafariBookmarks {
    /// The real home directory. Inside the sandbox `NSHomeDirectory()` is the extension's container,
    /// while the sandbox exception for Bookmarks.plist is relative to the user's real home.
    static var bookmarksURL: URL {
        let home = getpwuid(getuid()).flatMap { String(validatingCString: $0.pointee.pw_dir) }
            ?? NSHomeDirectory()
        return URL(fileURLWithPath: home).appendingPathComponent("Library/Safari/Bookmarks.plist")
    }

    /// Reads Bookmarks.plist and converts it to a single root node, like `chrome.bookmarks.getTree()`.
    static func tree() throws -> [String: Any] {
        let data: Data
        do {
            data = try Data(contentsOf: bookmarksURL)
        } catch let error as CocoaError where error.code == .fileReadNoPermission {
            throw SafariBookmarksError.noAccess
        } catch let error as CocoaError where error.code == .fileReadNoSuchFile {
            throw SafariBookmarksError.notFound
        } catch let error as NSError where error.domain == NSPOSIXErrorDomain
            && (error.code == Int(EPERM) || error.code == Int(EACCES)) {
            throw SafariBookmarksError.noAccess
        } catch {
            throw SafariBookmarksError.unreadable(error.localizedDescription)
        }

        guard let plist = try? PropertyListSerialization.propertyList(from: data, format: nil),
              let root = plist as? [String: Any]
        else {
            throw SafariBookmarksError.unreadable("unexpected format")
        }

        // The root is always a list. Anything else means the format changed under us - say so,
        // rather than returning an empty tree that reads as "you have no bookmark folders".
        guard var node = convert(root, path: []) else {
            throw SafariBookmarksError.unreadable("unexpected format")
        }
        node["id"] = "root"
        node["title"] = ""
        return node
    }

    /// Converts one plist entry. Returns nil for entries that are not bookmarks or folders
    /// (the History proxy, for example).
    private static func convert(_ entry: [String: Any], path: [String]) -> [String: Any]? {
        let type = entry["WebBookmarkType"] as? String
        let title = entry["Title"] as? String ?? ""
        // The id must be the same on every read: the popup stores selected folder ids and looks
        // them up again at sync time. Safari normally supplies a UUID; when it does not, derive one
        // from the folder path, never a random one - that would make a selected folder silently
        // vanish from every later sync.
        let id = entry["WebBookmarkUUID"] as? String
            ?? stableId(path + [title, entry["URLString"] as? String ?? ""])

        switch type {
        case "WebBookmarkTypeLeaf":
            guard let url = entry["URLString"] as? String, !url.isEmpty else { return nil }
            let uri = entry["URIDictionary"] as? [String: Any]
            var node: [String: Any] = [
                "id": id,
                "title": uri?["title"] as? String ?? "",
                "url": url,
            ]
            // Only Reading List items carry a date; plain bookmarks have none in the plist.
            if let readingList = entry["ReadingList"] as? [String: Any],
               let added = readingList["DateAdded"] as? Date {
                node["dateAdded"] = (added.timeIntervalSince1970 * 1000).rounded()
            }
            return node

        case "WebBookmarkTypeList":
            let children = (entry["Children"] as? [[String: Any]] ?? [])
                .compactMap { convert($0, path: path + [title]) }
            return ["id": id, "title": displayTitle(title), "children": children]

        default:
            return nil
        }
    }

    private static func stableId(_ parts: [String]) -> String {
        let digest = SHA256.hash(data: Data(parts.joined(separator: "\u{1F}").utf8))
        return "path-" + digest.prefix(16).map { String(format: "%02x", $0) }.joined()
    }

    /// Safari stores its built-in folders under internal names; show what the Safari UI shows.
    private static func displayTitle(_ title: String) -> String {
        switch title {
        case "BookmarksBar": return "Favourites"
        case "BookmarksMenu": return "Bookmarks Menu"
        case "com.apple.ReadingList": return "Reading List"
        default: return title
        }
    }
}
