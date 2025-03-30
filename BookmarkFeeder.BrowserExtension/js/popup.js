document.addEventListener('DOMContentLoaded', () => {
    // DOM Elements
    const addFolderBtn = document.getElementById('addFolderBtn');
    const syncNowBtn = document.getElementById('syncNowBtn');
    const settingsBtn = document.getElementById('settingsBtn');
    const closeSettingsBtn = document.getElementById('closeSettingsBtn');
    const saveSettingsBtn = document.getElementById('saveSettingsBtn');
    const selectedFoldersContainer = document.getElementById('selectedFolders');
    const lastSyncTime = document.getElementById('lastSyncTime');
    const mainContent = document.getElementById('mainContent');
    const settingsContent = document.getElementById('settingsContent');
    const serverUrlInput = document.getElementById('serverUrl');

    // State
    let selectedFolders = [];

    // Load saved settings
    chrome.storage.sync.get(['selectedFolders', 'lastSync', 'serverUrl'], (result) => {
        if (result.selectedFolders) {
            selectedFolders = result.selectedFolders;
            renderSelectedFolders();
        }
        if (result.lastSync) {
            updateLastSyncTime(new Date(result.lastSync));
        }
        if (result.serverUrl) {
            serverUrlInput.value = result.serverUrl;
        }
    });

    // Event Listeners
    addFolderBtn.addEventListener('click', async () => {
        const bookmarkTree = await chrome.bookmarks.getTree();
        showFolderSelector(bookmarkTree[0]);
    });

    syncNowBtn.addEventListener('click', async () => {
        try {
            syncNowBtn.disabled = true;
            syncNowBtn.textContent = 'Syncing...';
            await syncBookmarks();
            updateLastSyncTime(new Date());
            syncNowBtn.textContent = 'Sync Complete!';
            setTimeout(() => {
                syncNowBtn.textContent = 'Sync Now';
                syncNowBtn.disabled = false;
            }, 2000);
        } catch (error) {
            console.error('Sync failed:', error);
            syncNowBtn.textContent = 'Sync Failed';
            setTimeout(() => {
                syncNowBtn.textContent = 'Sync Now';
                syncNowBtn.disabled = false;
            }, 2000);
        }
    });

    // Settings handlers
    settingsBtn.addEventListener('click', () => {
        mainContent.classList.add('hidden');
        settingsContent.classList.remove('hidden');
    });

    closeSettingsBtn.addEventListener('click', () => {
        settingsContent.classList.add('hidden');
        mainContent.classList.remove('hidden');
    });

    saveSettingsBtn.addEventListener('click', async () => {
        const serverUrl = serverUrlInput.value.trim();
        if (!serverUrl) {
            alert('Please enter a valid server URL');
            return;
        }

        try {
            // Try to validate URL format
            new URL(serverUrl);

            // Save the URL
            await chrome.storage.sync.set({ serverUrl });
            
            // Show success message
            saveSettingsBtn.textContent = 'Saved!';
            saveSettingsBtn.disabled = true;
            setTimeout(() => {
                saveSettingsBtn.textContent = 'Save Settings';
                saveSettingsBtn.disabled = false;
                settingsContent.classList.add('hidden');
                mainContent.classList.remove('hidden');
            }, 1500);
        } catch (error) {
            console.error('Invalid URL:', error);
            alert('Please enter a valid URL (e.g., http://localhost:5000/api)');
        }
    });

    // Helper Functions
    function renderSelectedFolders() {
        selectedFoldersContainer.innerHTML = '';
        selectedFolders.forEach(folder => {
            const folderElement = createFolderElement(folder);
            selectedFoldersContainer.appendChild(folderElement);
        });
    }

    function createFolderElement(folder) {
        const div = document.createElement('div');
        div.className = 'flex items-center justify-between p-2 bg-slate-800 rounded-md shadow-sm border border-slate-700';
        
        const nameSpan = document.createElement('span');
        nameSpan.textContent = folder.title;
        nameSpan.className = 'text-sm text-slate-200';
        
        const removeBtn = document.createElement('button');
        removeBtn.innerHTML = '&times;';
        removeBtn.className = 'text-fuchsia-400 hover:text-fuchsia-300 font-bold transition-colors';
        removeBtn.addEventListener('click', () => removeFolder(folder.id));
        
        div.appendChild(nameSpan);
        div.appendChild(removeBtn);
        return div;
    }

    function removeFolder(folderId) {
        selectedFolders = selectedFolders.filter(f => f.id !== folderId);
        chrome.storage.sync.set({ selectedFolders });
        renderSelectedFolders();
    }

    async function showFolderSelector(root) {
        // Simple folder selection UI
        const folders = await getFolderList(root);
        const options = folders.map(f => 
            `<option value="${f.id}" class="bg-slate-700">${'—'.repeat(f.depth)} ${f.title}</option>`
        ).join('');

        const modal = document.createElement('div');
        modal.className = 'fixed inset-0 bg-slate-900/75 flex items-center justify-center backdrop-blur-sm';
        modal.innerHTML = `
            <div class="bg-slate-800 p-4 rounded-lg shadow-lg w-80 space-y-4 border border-slate-700">
                <h3 class="text-lg font-medium text-white">Select Folder</h3>
                <select class="w-full p-2 bg-slate-700 border border-slate-600 rounded-md text-white focus:outline-none focus:ring-2 focus:ring-fuchsia-500 appearance-none">
                    <option value="" disabled selected class="bg-slate-700">Select a folder...</option>
                    ${options}
                </select>
                <div class="flex justify-end space-x-2">
                    <button class="px-4 py-2 text-slate-300 hover:text-white transition-colors" id="cancelSelect">Cancel</button>
                    <button class="px-4 py-2 bg-fuchsia-600 hover:bg-fuchsia-500 text-white rounded-md transition-colors" id="confirmSelect">Add</button>
                </div>
            </div>
        `;

        // Add custom styles to fix dropdown appearance
        const style = document.createElement('style');
        style.textContent = `
            select option {
                background-color: rgb(51 65 85); /* bg-slate-700 */
                color: white;
                padding: 8px;
            }
            select:focus option:checked {
                background: linear-gradient(0deg, rgb(217 70 239), rgb(217 70 239)); /* bg-fuchsia-500 */
            }
        `;
        document.head.appendChild(style);

        document.body.appendChild(modal);

        const select = modal.querySelector('select');
        modal.querySelector('#cancelSelect').onclick = () => {
            modal.remove();
            style.remove();
        };
        modal.querySelector('#confirmSelect').onclick = () => {
            if (!select.value) {
                return; // Don't do anything if no folder is selected
            }
            const folder = folders.find(f => f.id === select.value);
            if (folder && !selectedFolders.find(f => f.id === folder.id)) {
                selectedFolders.push(folder);
                chrome.storage.sync.set({ selectedFolders });
                renderSelectedFolders();
            }
            modal.remove();
            style.remove();
        };
    }

    async function getFolderList(node, depth = 0) {
        let folders = [];
        if (node.children) {
            if (depth > 0) {  // Skip root folder
                folders.push({
                    id: node.id,
                    title: node.title,
                    depth
                });
            }
            for (const child of node.children) {
                if (child.children) {  // Only include folders
                    folders = folders.concat(await getFolderList(child, depth + 1));
                }
            }
        }
        return folders;
    }

    async function syncBookmarks() {
        const bookmarks = [];
        for (const folder of selectedFolders) {
            const folderBookmarks = await chrome.bookmarks.getChildren(folder.id);
            bookmarks.push(...folderBookmarks.filter(b => b.url));  // Only include actual bookmarks
        }

        // Get server URL from storage
        const { serverUrl } = await chrome.storage.sync.get(['serverUrl']);
        if (!serverUrl) {
            throw new Error('Server URL not configured');
        }

        // Send bookmarks to server
        const response = await fetch(`${serverUrl}/api/bookmarks`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(bookmarks)
        });

        if (!response.ok) {
            throw new Error(`Server responded with ${response.status}`);
        }

        // Update last sync time
        const now = new Date();
        chrome.storage.sync.set({ lastSync: now.toISOString() });
        updateLastSyncTime(now);
    }

    function updateLastSyncTime(date) {
        lastSyncTime.textContent = `Last synced: ${date.toLocaleString()}`;
    }
}); 