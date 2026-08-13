window.chatScrollBottom = function () {
    const el = document.getElementById('chat-window');
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
};
