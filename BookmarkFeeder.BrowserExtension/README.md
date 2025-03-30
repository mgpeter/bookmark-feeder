# BookmarkFeeder Browser Extension

A Chrome/Edge extension that allows you to sync selected bookmark folders with your self-hosted BookmarkFeeder instance.

## Features

- Select specific bookmark folders to sync
- Manual sync trigger
- Sync status tracking
- Server URL configuration
- Clean, modern UI with Tailwind CSS

## Development Setup

1. Clone the repository
2. Navigate to the extension directory:
   ```bash
   cd BookmarkFeeder.BrowserExtension
   ```
3. Generate icons (requires ImageMagick):
   ```bash
   chmod +x generate-icons.sh
   ./generate-icons.sh
   ```

## Loading the Extension

1. Open Chrome/Edge and navigate to the extensions page:
   - Chrome: `chrome://extensions`
   - Edge: `edge://extensions`
2. Enable "Developer mode" in the top right
3. Click "Load unpacked" and select the `BookmarkFeeder.BrowserExtension` directory

## Configuration

1. Click the extension icon in your browser toolbar
2. Click the settings icon (gear) to configure your BookmarkFeeder server URL
3. Add bookmark folders you want to sync using the "Add Folder" button
4. Click "Sync Now" to manually trigger synchronization

## Development Notes

- The extension uses Manifest V3
- UI is built with Tailwind CSS (via CDN for simplicity)
- Chrome Storage Sync API is used for settings persistence
- Chrome Bookmarks API is used for bookmark access

## Project Structure

```
BookmarkFeeder.BrowserExtension/
├── manifest.json        # Extension manifest
├── popup.html          # Extension popup UI
├── js/
│   └── popup.js        # Popup functionality
├── icons/              # Extension icons
│   ├── icon16.png
│   ├── icon48.png
│   └── icon128.png
└── README.md           # This file
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the terms of the LICENSE file in the root directory. 