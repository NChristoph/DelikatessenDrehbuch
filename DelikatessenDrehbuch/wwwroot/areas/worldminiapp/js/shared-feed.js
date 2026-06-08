// Shared Feed - Avocado Design
(function() {
    'use strict';

    // Tab Switching
    window.switchTab = function(tabName) {
        // Update tabs
        document.querySelectorAll('.s-tab').forEach(tab => {
            tab.classList.remove('active');
        });
        document.querySelector(`[data-tab="${tabName}"]`).classList.add('active');

        // Update bodies
        document.querySelectorAll('.s-tab-body').forEach(body => {
            body.classList.remove('active');
        });
        document.querySelector(`[data-tab-body="${tabName}"]`).classList.add('active');

        // If switching to chat, load messages
        if (tabName === 'chat' && !window.chatInitialized) {
            initChat();
        }
    };

    // Group Switching
    window.switchGroup = function(feedId) {
        const currentUrl = new URL(window.location.href);
        const userHash = currentUrl.searchParams.get('userHash') || '';
        window.location.href = `/WorldMiniApp/Shared/FeedLight?feedId=${feedId}&userHash=${encodeURIComponent(userHash)}`;
    };

    // Simple toast fallback if showToast doesn't exist
    function simpleToast(message, type = 'success') {
        const toast = document.createElement('div');
        toast.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: ${type === 'success' ? '#5fa052' : '#ff5e62'};
            color: white;
            padding: 12px 20px;
            border-radius: 12px;
            box-shadow: 0 8px 24px rgba(0,0,0,0.2);
            z-index: 10000;
            font-size: 14px;
            font-weight: 600;
            animation: slideIn 0.3s ease-out;
        `;
        toast.textContent = message;
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.style.animation = 'slideOut 0.3s ease-out';
            setTimeout(() => toast.remove(), 300);
        }, 2000);
    }

    // Copy Invite URL
    window.copyInviteUrl = function(url) {
        navigator.clipboard.writeText(url).then(() => {
            if (window.showToast) {
                window.showToast('Einladungslink kopiert', 'success');
            } else {
                simpleToast('Einladungslink kopiert');
            }
        }).catch(() => {
            // Fallback
            const textarea = document.createElement('textarea');
            textarea.value = url;
            document.body.appendChild(textarea);
            textarea.select();
            document.execCommand('copy');
            document.body.removeChild(textarea);
            if (window.showToast) {
                window.showToast('Einladungslink kopiert', 'success');
            } else {
                simpleToast('Einladungslink kopiert');
            }
        });
    };

    // Create Group Modal
    window.showCreateGroupModal = function() {
        document.getElementById('createGroupModal').style.display = 'flex';
    };

    window.closeCreateGroupModal = function() {
        document.getElementById('createGroupModal').style.display = 'none';
    };

    // Import Meal Plan Modal
    window.showImportMealPlanModal = function() {
        const modal = document.getElementById('importMealPlanModal');
        modal.style.display = 'flex';
        loadMyMealPlans();
    };

    window.closeImportMealPlanModal = function() {
        document.getElementById('importMealPlanModal').style.display = 'none';
    };

    // Load My Meal Plans
    function loadMyMealPlans() {
        const container = document.getElementById('importMealPlanList');
        const feedIdEl = document.querySelector('[data-feed-id]');
        const userHash = feedIdEl ? feedIdEl.dataset.userHash : '';

        container.innerHTML = `
            <div class="s-modal-loading">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 12a9 9 0 1 1-6.219-8.56"/>
                </svg>
                Lade deine Essenspläne...
            </div>
        `;

        fetch(`/WorldMiniApp/Shared/GetMyMealPlans?userHash=${encodeURIComponent(userHash)}`)
            .then(res => {
                if (!res.ok) throw new Error('Failed to load meal plans');
                return res.json();
            })
            .then(plans => {
                if (plans.length === 0) {
                    container.innerHTML = `
                        <div class="s-empty">
                            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>
                                <line x1="9" y1="9" x2="15" y2="15"/>
                                <line x1="15" y1="9" x2="9" y2="15"/>
                            </svg>
                            <p>Du hast noch keine Essenspläne erstellt.</p>
                        </div>
                    `;
                    return;
                }

                const html = plans.map(plan => {
                    const createdDate = new Date(plan.createdAt).toLocaleDateString('de-DE');
                    return `
                        <div class="s-import-item" onclick="importMealPlan(${plan.id}, '${escapeHtml(plan.title)}')">
                            <div class="s-import-title">${escapeHtml(plan.title)}</div>
                            <div class="s-import-meta">
                                <span><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/></svg> ${plan.personCount} Pers.</span>
                                <span><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg> ${plan.recipeCount} Rezepte</span>
                                <span><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg> ${createdDate}</span>
                            </div>
                        </div>
                    `;
                }).join('');

                container.innerHTML = html;
            })
            .catch(err => {
                console.error('Error loading meal plans:', err);
                container.innerHTML = `
                    <div class="s-empty" style="color: #d9534f;">
                        <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <circle cx="12" cy="12" r="10"/>
                            <line x1="12" y1="8" x2="12" y2="12"/>
                            <line x1="12" y1="16" x2="12.01" y2="16"/>
                        </svg>
                        <p>Fehler beim Laden der Essenspläne.</p>
                    </div>
                `;
            });
    }

    // Import Meal Plan
    window.importMealPlan = function(mealPlanId, title) {
        const container = document.getElementById('importMealPlanList');
        const feedIdEl = document.querySelector('[data-feed-id]');
        const feedId = feedIdEl ? parseInt(feedIdEl.dataset.feedId) : 0;
        const userHash = feedIdEl ? feedIdEl.dataset.userHash : '';

        container.innerHTML = `
            <div class="s-modal-loading">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M21 12a9 9 0 1 1-6.219-8.56"/>
                </svg>
                Importiere Essensplan...
            </div>
        `;

        fetch('/WorldMiniApp/Shared/ImportMealPlan', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({
                feedId: feedId,
                mealPlanId: mealPlanId,
                userHash: userHash
            })
        })
        .then(res => {
            if (!res.ok) throw new Error('Failed to import meal plan');
            return res.json();
        })
        .then(result => {
            container.innerHTML = `
                <div style="text-align: center; padding: 40px 20px;">
                    <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#5fa052" stroke-width="2">
                        <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/>
                        <polyline points="22 4 12 14.01 9 11.01"/>
                    </svg>
                    <h4 style="margin: 16px 0 8px; color: #14391f; font-weight: 700;">Erfolgreich importiert!</h4>
                    <p style="color: #7c8a7f;">Der Essensplan "${escapeHtml(result.title)}" mit ${result.recipeCount} Rezepten wurde zum Feed hinzugefügt.</p>
                    <button type="button" onclick="window.location.reload()" class="s-btn s-btn-primary" style="margin-top: 16px;">
                        Aktualisieren
                    </button>
                </div>
            `;
        })
        .catch(err => {
            console.error('Error importing meal plan:', err);
            container.innerHTML = `
                <div style="text-align: center; padding: 40px 20px; color: #d9534f;">
                    <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#d9534f" stroke-width="2">
                        <circle cx="12" cy="12" r="10"/>
                        <line x1="12" y1="8" x2="12" y2="12"/>
                        <line x1="12" y1="16" x2="12.01" y2="16"/>
                    </svg>
                    <p style="margin: 16px 0;">Fehler beim Importieren. Möglicherweise ist der Plan bereits im Feed.</p>
                    <button type="button" onclick="loadMyMealPlans()" class="s-btn s-btn-secondary">
                        Zurück
                    </button>
                </div>
            `;
        });
    };

    // Escape HTML
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Modal Close on Backdrop Click
    document.addEventListener('click', function(e) {
        if (e.target.classList.contains('s-modal')) {
            e.target.style.display = 'none';
        }
    });

    // Todo Functionality
    (function() {
        const todoList = document.getElementById('todoList');
        if (!todoList) return;

        const feedId = parseInt(todoList.dataset.feedId);
        const userHash = todoList.dataset.userHash;
        const isOwner = todoList.dataset.isOwner === 'True';
        const todoInput = document.getElementById('todoInput');
        const todoAddBtn = document.getElementById('todoAddBtn');

        // Add todo on button click
        todoAddBtn?.addEventListener('click', addTodo);

        // Add todo on Enter key
        todoInput?.addEventListener('keypress', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                addTodo();
            }
        });

        function addTodo() {
            const title = todoInput.value.trim();
            if (!title) return;

            todoAddBtn.disabled = true;
            todoInput.disabled = true;

            fetch('/WorldMiniApp/Shared/AddTodo', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    feedId: feedId,
                    userHash: userHash,
                    title: title
                })
            })
            .then(res => {
                if (!res.ok) throw new Error('Failed to add todo');
                return res.json();
            })
            .then(todo => {
                todoInput.value = '';

                // Remove empty message if exists
                const emptyMsg = todoList.querySelector('.s-empty');
                if (emptyMsg) emptyMsg.remove();

                // Add new todo to list
                const todoItem = createTodoElement(todo, isOwner);
                todoList.appendChild(todoItem);

                // Update progress card
                updateProgressCard();
            })
            .catch(err => {
                console.error('Error adding todo:', err);
                if (window.showToast) {
                    window.showToast('Fehler beim Hinzufügen der Aufgabe.', 'error');
                }
            })
            .finally(() => {
                todoAddBtn.disabled = false;
                todoInput.disabled = false;
                todoInput.focus();
            });
        }

        window.toggleTodo = function(todoId) {
            fetch('/WorldMiniApp/Shared/ToggleTodo', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    todoId: todoId,
                    userHash: userHash
                })
            })
            .then(res => {
                if (!res.ok) throw new Error('Failed to toggle todo');
                return res.json();
            })
            .then(result => {
                const todoItem = todoList.querySelector(`[data-todo-id="${todoId}"]`);
                if (!todoItem) return;

                if (result.isCompleted) {
                    todoItem.classList.add('is-done');
                    todoItem.querySelector('.s-task-checkbox').innerHTML = `
                        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/>
                            <polyline points="22 4 12 14.01 9 11.01"/>
                        </svg>
                    `;
                    if (result.completedByName) {
                        todoItem.querySelector('.s-task-meta span').textContent = `${result.completedByName} · Erledigt`;
                    }
                } else {
                    todoItem.classList.remove('is-done');
                    todoItem.querySelector('.s-task-checkbox').innerHTML = `
                        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <circle cx="12" cy="12" r="10"/>
                        </svg>
                    `;
                }

                // Update progress card
                updateProgressCard();
            })
            .catch(err => {
                console.error('Error toggling todo:', err);
            });
        };

        function createTodoElement(todo, isOwner) {
            const div = document.createElement('div');
            div.className = `s-task-row ${todo.isCompleted ? 'is-done' : ''}`;
            div.dataset.todoId = todo.id;

            const metaText = todo.isCompleted && todo.completedByName
                ? `${todo.completedByName} · Erledigt`
                : todo.createdByName;

            const checkIcon = todo.isCompleted
                ? '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>'
                : '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/></svg>';

            div.innerHTML = `
                <div class="s-task-checkbox" onclick="toggleTodo(${todo.id})">
                    ${checkIcon}
                </div>
                <div class="s-task-content">
                    <div class="s-task-text">${escapeHtml(todo.title)}</div>
                    <div class="s-task-meta"><span>${metaText}</span></div>
                </div>
            `;

            return div;
        }

        function updateProgressCard() {
            const todos = todoList.querySelectorAll('.s-task-row');
            const completed = todoList.querySelectorAll('.s-task-row.is-done').length;
            const total = todos.length;
            const percentage = total > 0 ? Math.round((completed / total) * 100) : 0;

            const progressCard = document.querySelector('.s-progress-card');
            if (!progressCard && total > 0) {
                // Create progress card
                const card = document.createElement('div');
                card.className = 's-progress-card';
                card.innerHTML = `
                    <div class="s-progress-row">
                        <div class="s-progress-left">
                            <div class="s-progress-eyebrow">DIESE WOCHE</div>
                            <div class="s-progress-num">${completed}<span class="s-progress-total">/${total} erledigt</span></div>
                        </div>
                        <div class="s-progress-percent">${percentage}%</div>
                    </div>
                    <div class="s-progress-bar">
                        <div class="s-progress-fill" style="width: ${percentage}%"></div>
                    </div>
                `;
                todoList.parentElement.insertBefore(card, todoList);
            } else if (progressCard) {
                // Update existing card
                progressCard.querySelector('.s-progress-num').innerHTML = `${completed}<span class="s-progress-total">/${total} erledigt</span>`;
                progressCard.querySelector('.s-progress-percent').textContent = `${percentage}%`;
                progressCard.querySelector('.s-progress-fill').style.width = `${percentage}%`;
            }
        }
    })();

    // Chat Functionality
    window.chatInitialized = false;

    function initChat() {
        const chatMessages = document.getElementById('chatMessages');
        if (!chatMessages) return;

        const feedId = parseInt(chatMessages.dataset.feedId);
        const userHash = chatMessages.dataset.userHash;
        const chatInput = document.getElementById('chatInput');
        const chatSendBtn = document.getElementById('chatSendBtn');
        const emojiBtn = document.querySelector('.s-chat-emoji');
        const fileInput = document.getElementById('chatFileInput');
        const filePreview = document.getElementById('chatFilePreview');
        let lastMessageId = 0;
        let isLoadingMessages = false;
        let selectedFile = null;

        window.chatInitialized = true;

        // Emoji picker
        const emojis = ['😊', '😂', '❤️', '👍', '🎉', '🔥', '✨', '🙏', '💯', '🎯', '🍀', '🌟', '💚', '🥑', '🍕', '🍔', '🌮', '🥗', '🍝', '🍰', '☕', '🍷'];

        emojiBtn?.addEventListener('click', (e) => {
            e.stopPropagation();
            let picker = document.querySelector('.emoji-picker');

            if (picker) {
                picker.remove();
                return;
            }

            picker = document.createElement('div');
            picker.className = 'emoji-picker';
            picker.innerHTML = emojis.map(emoji =>
                `<span class="emoji-item" onclick="insertEmoji('${emoji}')">${emoji}</span>`
            ).join('');

            emojiBtn.parentElement.appendChild(picker);

            // Close on outside click
            setTimeout(() => {
                document.addEventListener('click', function closeEmojiPicker() {
                    picker?.remove();
                    document.removeEventListener('click', closeEmojiPicker);
                }, 10);
            });
        });

        // Insert emoji function
        window.insertEmoji = function(emoji) {
            const currentValue = chatInput.value;
            const cursorPos = chatInput.selectionStart;
            chatInput.value = currentValue.substring(0, cursorPos) + emoji + currentValue.substring(cursorPos);
            chatInput.focus();
            chatInput.setSelectionRange(cursorPos + emoji.length, cursorPos + emoji.length);
        };

        // File upload handling
        fileInput?.addEventListener('change', (e) => {
            const file = e.target.files[0];
            if (!file) return;

            // Max 10MB
            if (file.size > 10 * 1024 * 1024) {
                simpleToast('Datei ist zu groß (max. 10 MB)', 'error');
                fileInput.value = '';
                return;
            }

            selectedFile = file;
            showFilePreview(file);
        });

        function showFilePreview(file) {
            const isImage = file.type.startsWith('image/');
            filePreview.style.display = 'flex';

            if (isImage) {
                const reader = new FileReader();
                reader.onload = (e) => {
                    filePreview.innerHTML = `
                        <img src="${e.target.result}" alt="Preview" style="max-height: 60px; border-radius: 8px;" />
                        <span>${file.name} (${formatFileSize(file.size)})</span>
                        <button type="button" onclick="clearFileSelection()" style="margin-left: auto; background: none; border: none; color: #d9534f; cursor: pointer; font-size: 16px;">✕</button>
                    `;
                };
                reader.readAsDataURL(file);
            } else {
                const icon = getFileIcon(file.type);
                filePreview.innerHTML = `
                    <span style="font-size: 24px;">${icon}</span>
                    <span>${file.name} (${formatFileSize(file.size)})</span>
                    <button type="button" onclick="clearFileSelection()" style="margin-left: auto; background: none; border: none; color: #d9534f; cursor: pointer; font-size: 16px;">✕</button>
                `;
            }
        }

        window.clearFileSelection = function() {
            selectedFile = null;
            fileInput.value = '';
            filePreview.style.display = 'none';
            filePreview.innerHTML = '';
        };

        function formatFileSize(bytes) {
            if (bytes < 1024) return bytes + ' B';
            if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
            return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
        }

        function getFileIcon(type) {
            if (type.includes('pdf')) return '📄';
            if (type.includes('word') || type.includes('doc')) return '📝';
            if (type.includes('excel') || type.includes('sheet')) return '📊';
            return '📎';
        }

        // Load initial messages
        loadMessages();

        // Poll for new messages every 3 seconds
        setInterval(() => {
            if (!isLoadingMessages && lastMessageId > 0) {
                loadMessages(lastMessageId);
            }
        }, 3000);

        // Send message on button click
        chatSendBtn?.addEventListener('click', sendMessage);

        // Send message on Enter key
        chatInput?.addEventListener('keypress', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });

        function loadMessages(sinceId = null) {
            isLoadingMessages = true;
            const url = sinceId
                ? `/WorldMiniApp/Shared/GetMessages?feedId=${feedId}&userHash=${encodeURIComponent(userHash)}&sinceId=${sinceId}`
                : `/WorldMiniApp/Shared/GetMessages?feedId=${feedId}&userHash=${encodeURIComponent(userHash)}`;

            fetch(url)
                .then(res => res.json())
                .then(messages => {
                    if (messages.length > 0) {
                        const wasAtBottom = isScrolledToBottom();

                        if (sinceId === null) {
                            // Initial load
                            chatMessages.innerHTML = '';
                        }

                        messages.forEach(msg => {
                            if (msg.id > lastMessageId) {
                                lastMessageId = msg.id;
                                appendMessage(msg);
                            }
                        });

                        if (wasAtBottom || sinceId === null) {
                            scrollToBottom();
                        }
                    } else if (sinceId === null) {
                        chatMessages.innerHTML = '<div class="s-chat-empty">Noch keine Nachrichten. Starte die Unterhaltung!</div>';
                    }
                })
                .catch(err => {
                    console.error('Error loading messages:', err);
                    if (sinceId === null) {
                        chatMessages.innerHTML = '<div class="s-chat-empty" style="color: #d9534f;">Fehler beim Laden der Nachrichten.</div>';
                    }
                })
                .finally(() => {
                    isLoadingMessages = false;
                });
        }

        function sendMessage() {
            const message = chatInput.value.trim();
            if (!message && !selectedFile) return;

            chatSendBtn.disabled = true;
            chatInput.disabled = true;

            if (selectedFile) {
                // Send with file
                const formData = new FormData();
                formData.append('feedId', feedId);
                formData.append('userHash', userHash);
                formData.append('message', message || '');
                formData.append('file', selectedFile);

                fetch('/WorldMiniApp/Shared/SendMessageWithFile', {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: formData
                })
                .then(res => {
                    if (!res.ok) throw new Error('Failed to send message');
                    return res.json();
                })
                .then(msg => {
                    chatInput.value = '';
                    clearFileSelection();
                    if (msg.id > lastMessageId) {
                        lastMessageId = msg.id;
                        appendMessage(msg);
                        scrollToBottom();
                    }
                })
                .catch(err => {
                    console.error('Error sending message:', err);
                    simpleToast('Fehler beim Senden. Datei zu groß?', 'error');
                })
                .finally(() => {
                    chatSendBtn.disabled = false;
                    chatInput.disabled = false;
                    chatInput.focus();
                });
            } else {
                // Send text only
                fetch('/WorldMiniApp/Shared/SendMessage', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify({
                        feedId: feedId,
                        userHash: userHash,
                        message: message
                    })
                })
                .then(res => {
                    if (!res.ok) throw new Error('Failed to send message');
                    return res.json();
                })
                .then(msg => {
                    chatInput.value = '';
                    if (msg.id > lastMessageId) {
                        lastMessageId = msg.id;
                        appendMessage(msg);
                        scrollToBottom();
                    }
                })
                .catch(err => {
                    console.error('Error sending message:', err);
                    if (window.showToast) {
                        window.showToast('Fehler beim Senden der Nachricht.', 'error');
                    }
                })
                .finally(() => {
                    chatSendBtn.disabled = false;
                    chatInput.disabled = false;
                    chatInput.focus();
                });
            }
        }

        function appendMessage(msg) {
            const messageDiv = document.createElement('div');
            messageDiv.className = 's-chat-msg';
            if (msg.isOwn) {
                messageDiv.classList.add('is-own');
            }

            const timeStr = new Date(msg.createdAtUtc).toLocaleTimeString('de-DE', {
                hour: '2-digit',
                minute: '2-digit'
            });

            messageDiv.innerHTML = `
                <div class="s-chat-msg-header">
                    <strong>${escapeHtml(msg.userName)}</strong>
                    <span class="s-chat-msg-time">${timeStr}</span>
                </div>
                <div class="s-chat-msg-bubble">${escapeHtml(msg.message)}</div>
            `;

            chatMessages.appendChild(messageDiv);
        }

        function isScrolledToBottom() {
            const threshold = 100;
            return chatMessages.scrollHeight - chatMessages.scrollTop - chatMessages.clientHeight < threshold;
        }

        function scrollToBottom() {
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }
    }

    // Add chat message styles dynamically
    const chatStyles = `
        .s-chat-file-preview {
            position: absolute;
            bottom: 100%;
            left: 0;
            right: 0;
            background: white;
            border: 1px solid rgba(20,57,31,0.12);
            border-radius: 12px;
            padding: 8px 12px;
            margin-bottom: 8px;
            display: flex;
            align-items: center;
            gap: 10px;
            box-shadow: 0 4px 12px rgba(20,57,31,0.1);
            font-size: 12px;
            color: var(--ink-deep);
        }
        .emoji-picker {
            position: absolute;
            bottom: 100%;
            right: 0;
            background: white;
            border: 1px solid rgba(20,57,31,0.12);
            border-radius: 12px;
            padding: 10px;
            margin-bottom: 8px;
            display: grid;
            grid-template-columns: repeat(6, 1fr);
            gap: 4px;
            box-shadow: 0 8px 24px rgba(20,57,31,0.15);
            z-index: 100;
            max-width: 240px;
        }
        .emoji-item, .char-item {
            width: 32px;
            height: 32px;
            display: flex;
            align-items: center;
            justify-content: center;
            cursor: pointer;
            border-radius: 6px;
            transition: all 0.15s;
            font-size: 18px;
        }
        .char-item {
            font-size: 14px;
            font-weight: 600;
            color: var(--ink-deep);
        }
        .emoji-item:hover, .char-item:hover {
            background: rgba(95,160,82,0.15);
            transform: scale(1.15);
        }
        .s-chat-composer {
            position: relative;
        }
        .s-chat-input-pill {
            position: relative;
        }
        .s-chat-msg {
            display: flex;
            flex-direction: column;
            gap: 4px;
            max-width: 70%;
            align-self: flex-start;
        }
        .s-chat-msg.is-own {
            align-self: flex-end;
        }
        .s-chat-msg-header {
            display: flex;
            align-items: center;
            gap: 8px;
            font-size: 12px;
        }
        .s-chat-msg.is-own .s-chat-msg-header {
            flex-direction: row-reverse;
        }
        .s-chat-msg-header strong {
            color: var(--ink-deep);
            font-weight: 600;
        }
        .s-chat-msg-time {
            color: var(--ink-mute);
            font-size: 11px;
        }
        .s-chat-msg-bubble {
            background: white;
            border: 1px solid rgba(20,57,31,0.08);
            padding: 10px 14px;
            border-radius: 18px 18px 18px 4px;
            color: var(--ink-text);
            line-height: 1.5;
            word-wrap: break-word;
            font-size: 13.5px;
        }
        .s-chat-msg.is-own .s-chat-msg-bubble {
            background: var(--grad-bubble);
            color: white;
            border-color: transparent;
            border-radius: 18px 18px 4px 18px;
        }
        .s-import-item {
            background: var(--card-soft);
            border: 2px solid rgba(20,57,31,0.08);
            border-radius: 16px;
            padding: 14px;
            margin-bottom: 10px;
            cursor: pointer;
            transition: all 0.2s;
        }
        .s-import-item:hover {
            border-color: var(--avocado);
            background: #f0f5ee;
            transform: translateX(4px);
        }
        .s-import-title {
            font-size: 16px;
            font-weight: 700;
            color: var(--ink-deep);
            margin-bottom: 8px;
        }
        .s-import-meta {
            display: flex;
            gap: 12px;
            font-size: 12px;
            color: var(--ink-mute);
        }
        .s-import-meta span {
            display: flex;
            align-items: center;
            gap: 4px;
        }
    `;

    const styleEl = document.createElement('style');
    styleEl.textContent = chatStyles;
    document.head.appendChild(styleEl);

})();
